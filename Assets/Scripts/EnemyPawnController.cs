using System.Collections.Generic;
using RayFire;
using UnityEngine;

/// <summary>
/// Chess-pawn-style enemy: once the King or a White Pawn (or a Rook, Knight
/// or Bishop) comes within fireDetectionRange tiles, chases whichever is
/// nearest, one tile per turn, each time the player finishes a move (only
/// after that move has fully concluded). Moves orthogonally, like a pawn's
/// advance - except when the target sits on a diagonal neighbor, in which
/// case it steps diagonally onto its tile, matching how a real pawn
/// captures. Does nothing (no target, no move planned) while nothing
/// friendly is close enough to chase.
///
/// Contact with the King is always lethal for this pawn, from either side:
/// if this pawn's path takes it onto the King's tile it deals damage and
/// then dies, and if the King moves onto this pawn's own tile this pawn
/// dies outright. A White Pawn works the same way as the King on the
/// "friendly piece walks onto this pawn" side (this pawn just dies), but
/// the reverse is asymmetric: if this pawn's own path takes it onto a White
/// Pawn's tile, the White Pawn dies and this pawn survives, unlike the
/// mutual-destruction King attack.
/// </summary>
[RequireComponent(typeof(Health))]
public class EnemyPawnController : MonoBehaviour
{
    [Tooltip("World units per second this pawn slides at when stepping onto a tile.")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("HP taken from the player when this pawn enters the player's tile.")]
    [SerializeField] private int damage = 1;

    [Tooltip("Distance from the player at which an attack move lands, so the pawn stops and resolves the hit right as it touches the player's model instead of sliding into it first. Tune to your models' sizes.")]
    [SerializeField] private float attackContactDistance = 1f;

    [Header("Fire Warning")]
    [Tooltip("Root of the Purple-FireWood effect nested under this pawn. Hidden by default, lights up and points at the player once they're close.")]
    [SerializeField] private Transform fireEffectRoot;

    [Tooltip("Chebyshev tile distance from the player at which the fire effect activates. Also used to decide when this pawn is close enough to a friendly piece to chase it.")]
    [SerializeField] private int fireDetectionRange = 4;

    [Header("Death")]
    [Tooltip("Force applied to fragments, pointing away from the player, when this pawn dies.")]
    [SerializeField] private float explosionForce = 8f;

    [Tooltip("Extra upward force added on top of the away-from-player push.")]
    [SerializeField] private float explosionUpwardForce = 3f;

    [Header("XP")]
    [Tooltip("Spawned on this pawn's tile when it dies.")]
    [SerializeField] private GameObject xpPickupPrefab;

    [Tooltip("Height above the tile's surface the XP pickup spawns at.")]
    [SerializeField] private float xpSpawnHeight = 0.3f;

    public static readonly List<EnemyPawnController> All = new();

    // Set by TutorialController for the whole Tutorial scene: every enemy
    // pawn treats the King as a valid chase target regardless of distance,
    // instead of only within fireDetectionRange like normal. Whatever sets
    // this true is responsible for resetting it back to false again (e.g.
    // from its own OnDestroy), so it doesn't leak into a later scene.
    public static bool AlwaysTargetPlayer { get; set; }

    private PlayerController player;
    private Health playerHealth;
    private Health health;
    private RayfireRigid rigid;
    private PieceMoveSound moveSound;

    private Tile currentTile;
    public Tile CurrentTile => currentTile;

    private Tile previousTile;

    // Decided (and highlighted) a full turn ahead of when it's actually
    // carried out, so the player can see the threat coming while they're
    // still choosing their own move.
    private Tile plannedDestination;

    // Same pivot-offset fix as the King: keeps whatever X/Z offset this
    // piece was placed with on its starting tile, and re-applies it on
    // every tile it steps onto so it stays visually centered.
    private Vector3 tileOffset;

    // How far above a tile's resting surface this pawn's model sits. Tiles
    // themselves rise while highlighted (reachable by the player), so the
    // pawn's world Y is re-derived from its current tile's live height
    // every frame instead of being fixed - otherwise a pawn standing on a
    // tile that becomes highlighted would clip into it as the tile rises.
    private float heightAboveTile;

    private bool isSliding;
    private Vector3 slideTarget;

    // A White Pawn occupying this move's destination, captured the moment
    // this pawn's slide gets close enough - set in BeginMoveTo, consumed
    // (and cleared) the instant it's captured, so it only happens once.
    private WhitePawnController pendingCapturedPawn;

    // A Rook occupying this move's destination, attacked the moment this
    // pawn's slide gets close enough - set in BeginMoveTo, consumed (and
    // cleared) the instant that attack resolves. See ResolveRookAttack.
    private RookController pendingAttackedRook;

    // A Knight occupying this move's destination, captured the moment this
    // pawn's slide gets close enough - same one-hit-kill rule as a White
    // Pawn, set in BeginMoveTo and consumed (and cleared) the instant it's
    // captured, so it only happens once.
    private KnightController pendingCapturedKnight;

    // A Bishop occupying this move's destination, captured the moment this
    // pawn's slide gets close enough - same one-hit-kill rule as a White
    // Pawn or Knight (the reduced-damage rule only applies when a Bishop is
    // the one attacking, not when one is attacked). Set in BeginMoveTo and
    // consumed (and cleared) the instant it's captured, so it only happens
    // once.
    private BishopController pendingCapturedBishop;

    // Set right before Health.Kill() so HandleDeath (its synchronous
    // OnDeath reaction) knows which way to push the fragments.
    private Vector3 pendingKnockbackDirection = Vector3.forward;

    private void OnEnable()
    {
        All.Add(this);
        PlayerController.OnMoveCompleted += HandlePieceMoved;
        TurnManager.EnemyTurnStarted += ExecutePlannedMove;

        health = GetComponent<Health>();

        if (health != null)
        {
            health.OnDeath += HandleDeath;
        }
    }

    private void OnDisable()
    {
        All.Remove(this);
        PlayerController.OnMoveCompleted -= HandlePieceMoved;
        TurnManager.EnemyTurnStarted -= ExecutePlannedMove;

        if (health != null)
        {
            health.OnDeath -= HandleDeath;
        }
    }

    private void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        playerHealth = player != null ? player.GetComponent<Health>() : null;
        rigid = GetComponent<RayfireRigid>();
        moveSound = GetComponent<PieceMoveSound>();

        currentTile = TileGrid.FindNearest(transform.position);

        if (currentTile != null)
        {
            tileOffset = transform.position - currentTile.transform.position;
            tileOffset.y = 0f;
            heightAboveTile = transform.position.y - currentTile.BaseWorldPosition.y;
        }

        if (fireEffectRoot != null)
        {
            fireEffectRoot.gameObject.SetActive(false);
        }

        UpdateFireWarning();
        PlanNextMove();
    }

    private void Update()
    {
        FollowTileHeight();
        AimFireAtPlayer();

        if (isSliding)
        {
            Slide();
        }
    }

    // Keeps the fire pointed at the player, live, for as long as it's
    // active - not just a one-time snap when they first come into range.
    private void AimFireAtPlayer()
    {
        if (fireEffectRoot == null || player == null || !fireEffectRoot.gameObject.activeSelf)
        {
            return;
        }

        Vector3 lookPoint = player.transform.position;
        lookPoint.y = fireEffectRoot.position.y;

        if ((lookPoint - fireEffectRoot.position).sqrMagnitude > 0.0001f)
        {
            fireEffectRoot.LookAt(lookPoint);
        }
    }

    // Shows/hides the fire warning based on how many tiles away the player
    // currently is, using the same Chebyshev (king-move) distance the rest
    // of the board already measures "tiles away" with.
    private void UpdateFireWarning()
    {
        if (fireEffectRoot == null || player == null || currentTile == null)
        {
            return;
        }

        Tile playerTile = player.CurrentTile;

        if (playerTile == null || !TryGetTileDistance(currentTile, playerTile, out int distance))
        {
            fireEffectRoot.gameObject.SetActive(false);
            return;
        }

        fireEffectRoot.gameObject.SetActive(distance <= fireDetectionRange);
    }

    private static bool TryGetTileDistance(Tile from, Tile to, out int distance)
    {
        distance = 0;

        if (!TileGrid.TryGetCoord(from, out Vector2Int fromCoord) ||
            !TileGrid.TryGetCoord(to, out Vector2Int toCoord))
        {
            return false;
        }

        distance = Mathf.Max(Mathf.Abs(fromCoord.x - toCoord.x), Mathf.Abs(fromCoord.y - toCoord.y));
        return true;
    }

    private void FollowTileHeight()
    {
        if (currentTile == null)
        {
            return;
        }

        Vector3 position = transform.position;
        position.y = currentTile.transform.position.y + heightAboveTile;
        transform.position = position;
    }

    // Reacts to every individual piece move (the King, a White Pawn, a Rook,
    // a Knight or a Bishop), regardless of whether the player has ended
    // their turn yet - a capture resolves the instant a piece steps onto
    // this pawn's tile, rather than waiting for End Turn. Every friendly
    // piece deals its own CaptureDamage rather than an unconditional kill.
    // Every enemy pawn today has only 1 HP, so this still always finishes it
    // off, but it goes through the same damage path a tougher enemy would
    // need to actually survive it. See TakeDamage.
    private void HandlePieceMoved()
    {
        if (currentTile == null)
        {
            return;
        }

        if (player != null)
        {
            UpdateFireWarning();
        }

        if (player != null && player.CurrentTile == currentTile)
        {
            TakeDamage(player.CaptureDamage, player.transform.position);
            return;
        }

        foreach (WhitePawnController pawn in WhitePawnController.All)
        {
            if (pawn.CurrentTile != currentTile)
            {
                continue;
            }

            TakeDamage(pawn.CaptureDamage, pawn.transform.position);
            return;
        }

        // Unlike this pawn attacking a Rook (ResolveRookAttack), there's no
        // return-damage rule the other way around - the Rook's own capture
        // just deals its CaptureDamage like every other piece here.
        foreach (RookController rook in RookController.All)
        {
            if (rook.CurrentTile != currentTile)
            {
                continue;
            }

            TakeDamage(rook.CaptureDamage, rook.transform.position);
            return;
        }

        foreach (KnightController knight in KnightController.All)
        {
            if (knight.CurrentTile != currentTile)
            {
                continue;
            }

            TakeDamage(knight.CaptureDamage, knight.transform.position);
            return;
        }

        foreach (BishopController bishop in BishopController.All)
        {
            if (bishop.CurrentTile != currentTile)
            {
                continue;
            }

            TakeDamage(bishop.CaptureDamage, bishop.transform.position);
            return;
        }

        // The board just changed under this pawn's plan (a threat may have
        // just come into or left range, or the tile it was heading for may
        // now be blocked) - re-plan immediately rather than waiting for the
        // once-a-turn planning pass, so its highlighted destination always
        // reflects the current board.
        PlanNextMove();
    }

    // Carries out the move that was planned last turn. Every pawn's
    // TurnManager.EnemyTurnStarted subscription fires from the same
    // End Turn click, so they all set off together instead of reacting one
    // at a time to each of the player's individual piece moves.
    private void ExecutePlannedMove()
    {
        if (currentTile == null || isSliding)
        {
            return;
        }

        Tile destination = plannedDestination;
        plannedDestination = null;

        // Whatever was planned last turn might no longer be valid - another
        // pawn could have claimed that tile in the meantime, or nothing was
        // reachable when it was planned. Either way, clear any stale
        // highlight and try planning fresh for the turn after this one.
        if (destination == null || IsOccupiedByAnotherPawn(destination))
        {
            if (destination != null)
            {
                destination.SetEnemyDestinationHighlighted(false);
            }

            PlanNextMove();
            return;
        }

        BeginMoveTo(destination);
    }

    // Decides (and highlights) the tile this pawn will move to next time.
    // Called once right after this pawn's own move concludes, and again
    // every time any friendly piece moves during the player's turn (see
    // HandlePieceMoved) - safe to call repeatedly, since any previously
    // planned tile's highlight is cleared first. Targets whichever is
    // nearest, if one is within fireDetectionRange tiles right now (close
    // enough to actually chase down and, if adjacent, capture); does
    // nothing otherwise.
    private void PlanNextMove()
    {
        if (plannedDestination != null)
        {
            plannedDestination.SetEnemyDestinationHighlighted(false);
            plannedDestination = null;
        }

        // No turns (the Main Menu): enemies stand still and don't plan.
        if (currentTile == null || !TurnManager.TurnsEnabled)
        {
            return;
        }

        Tile target = FindNearestThreatTile();

        if (target == null)
        {
            return;
        }

        if (!TileGrid.TryGetCoord(currentTile, out Vector2Int currentCoord) ||
            !TileGrid.TryGetCoord(target, out Vector2Int targetCoord))
        {
            return;
        }

        Tile destination = ChooseMove(currentCoord, targetCoord);

        if (destination == null || destination == currentTile || IsOccupiedByAnotherPawn(destination))
        {
            return;
        }

        plannedDestination = destination;
        destination.SetEnemyDestinationHighlighted(true);
    }

    // The nearest of the King and every White Pawn/Rook/Knight/Bishop
    // that's within fireDetectionRange tiles of this pawn right now (or
    // always, for the King specifically, if AlwaysTargetPlayer is on), or
    // null if none of them are close enough to bother chasing this turn.
    private Tile FindNearestThreatTile()
    {
        if (currentTile == null)
        {
            return null;
        }

        Tile nearestTile = null;
        int nearestDistance = int.MaxValue;

        if (player != null && player.CurrentTile != null &&
            TryGetTileDistance(currentTile, player.CurrentTile, out int playerDistance) &&
            (AlwaysTargetPlayer || playerDistance <= fireDetectionRange))
        {
            nearestTile = player.CurrentTile;
            nearestDistance = playerDistance;
        }

        foreach (WhitePawnController pawn in WhitePawnController.All)
        {
            if (pawn.CurrentTile == null ||
                !TryGetTileDistance(currentTile, pawn.CurrentTile, out int pawnDistance) ||
                pawnDistance > fireDetectionRange ||
                pawnDistance >= nearestDistance)
            {
                continue;
            }

            nearestTile = pawn.CurrentTile;
            nearestDistance = pawnDistance;
        }

        foreach (RookController rook in RookController.All)
        {
            if (rook.CurrentTile == null ||
                !TryGetTileDistance(currentTile, rook.CurrentTile, out int rookDistance) ||
                rookDistance > fireDetectionRange ||
                rookDistance >= nearestDistance)
            {
                continue;
            }

            nearestTile = rook.CurrentTile;
            nearestDistance = rookDistance;
        }

        foreach (KnightController knight in KnightController.All)
        {
            if (knight.CurrentTile == null ||
                !TryGetTileDistance(currentTile, knight.CurrentTile, out int knightDistance) ||
                knightDistance > fireDetectionRange ||
                knightDistance >= nearestDistance)
            {
                continue;
            }

            nearestTile = knight.CurrentTile;
            nearestDistance = knightDistance;
        }

        foreach (BishopController bishop in BishopController.All)
        {
            if (bishop.CurrentTile == null ||
                !TryGetTileDistance(currentTile, bishop.CurrentTile, out int bishopDistance) ||
                bishopDistance > fireDetectionRange ||
                bishopDistance >= nearestDistance)
            {
                continue;
            }

            nearestTile = bishop.CurrentTile;
            nearestDistance = bishopDistance;
        }

        return nearestTile;
    }

    // A pawn can't move onto a tile another pawn already occupies. If that
    // other pawn doesn't move away this turn either, this pawn just stays
    // put - there's no pathing around it.
    private bool IsOccupiedByAnotherPawn(Tile tile)
    {
        foreach (EnemyPawnController pawn in All)
        {
            if (pawn != this && pawn.CurrentTile == tile)
            {
                return true;
            }
        }

        return false;
    }

    private static WhitePawnController FindWhitePawnOnTile(Tile tile)
    {
        foreach (WhitePawnController pawn in WhitePawnController.All)
        {
            if (pawn.CurrentTile == tile)
            {
                return pawn;
            }
        }

        return null;
    }

    private static RookController FindRookOnTile(Tile tile)
    {
        foreach (RookController rook in RookController.All)
        {
            if (rook.CurrentTile == tile)
            {
                return rook;
            }
        }

        return null;
    }

    private static KnightController FindKnightOnTile(Tile tile)
    {
        foreach (KnightController knight in KnightController.All)
        {
            if (knight.CurrentTile == tile)
            {
                return knight;
            }
        }

        return null;
    }

    private static BishopController FindBishopOnTile(Tile tile)
    {
        foreach (BishopController bishop in BishopController.All)
        {
            if (bishop.CurrentTile == tile)
            {
                return bishop;
            }
        }

        return null;
    }

    // A pawn only ever steps diagonally onto its target's own tile - a real
    // capture - so an orthogonal step that would land it exactly there is
    // blocked rather than taken, same as a real pawn facing an occupied
    // square ahead of it.
    private static Tile ChooseMove(Vector2Int currentCoord, Vector2Int targetCoord)
    {
        int deltaCol = targetCoord.x - currentCoord.x;
        int deltaRow = targetCoord.y - currentCoord.y;

        // The target is exactly one tile away on both axes: step diagonally.
        if (Mathf.Abs(deltaCol) == 1 && Mathf.Abs(deltaRow) == 1)
        {
            return TileGrid.GetTileAt(targetCoord);
        }

        // Otherwise advance one tile orthogonally, closing whichever gap is bigger.
        Vector2Int? columnStep = deltaCol != 0
            ? new Vector2Int(currentCoord.x + System.Math.Sign(deltaCol), currentCoord.y)
            : null;

        Vector2Int? rowStep = deltaRow != 0
            ? new Vector2Int(currentCoord.x, currentCoord.y + System.Math.Sign(deltaRow))
            : null;

        bool columnGapIsBigger = Mathf.Abs(deltaCol) >= Mathf.Abs(deltaRow);
        Vector2Int? preferred = columnGapIsBigger ? columnStep : rowStep;
        Vector2Int? fallback = columnGapIsBigger ? rowStep : columnStep;

        Tile move = GetValidAdvance(preferred, targetCoord);
        return move != null ? move : GetValidAdvance(fallback, targetCoord);
    }

    // An orthogonal step that would land exactly on the target's tile is
    // blocked rather than taken - a straight-line "capture" - since this
    // pawn only ever captures diagonally, same as a real chess pawn.
    private static Tile GetValidAdvance(Vector2Int? coord, Vector2Int targetCoord)
    {
        if (!coord.HasValue || coord.Value == targetCoord)
        {
            return null;
        }

        return TileGrid.GetTileAt(coord.Value);
    }

    private void BeginMoveTo(Tile target)
    {
        previousTile = currentTile;
        pendingCapturedPawn = FindWhitePawnOnTile(target);
        pendingAttackedRook = FindRookOnTile(target);
        pendingCapturedKnight = FindKnightOnTile(target);
        pendingCapturedBishop = FindBishopOnTile(target);

        // Already highlighted from when this move was planned last turn;
        // this just keeps it lit (a no-op if already on) through the slide.
        target.SetEnemyDestinationHighlighted(true);

        slideTarget = transform.position;
        slideTarget.x = target.transform.position.x + tileOffset.x;
        slideTarget.z = target.transform.position.z + tileOffset.z;
        isSliding = true;

        currentTile = target;
        moveSound?.PlayIfVisible();

        TurnManager.BeginEnemyMove();
    }

    private void Slide()
    {
        bool isAttackMove = player != null && currentTile == player.CurrentTile;

        // Resolve the attack the moment the pawn's model reaches the
        // player, instead of sliding all the way to the tile center first -
        // otherwise it visibly phases into the player before dying.
        if (isAttackMove && Vector3.Distance(transform.position, player.transform.position) <= attackContactDistance)
        {
            isSliding = false;
            TurnManager.EndEnemyMove();
            currentTile.SetEnemyDestinationHighlighted(false);
            ResolveAttack();
            return;
        }

        // Same early-contact idea as the King attack above, but this pawn
        // doesn't die and doesn't stop here - it captures the White Pawn in
        // its path and keeps sliding the rest of the way onto that tile.
        if (pendingCapturedPawn != null &&
            Vector3.Distance(transform.position, pendingCapturedPawn.transform.position) <= attackContactDistance)
        {
            CaptureWhitePawn();
        }

        // Same idea again for a Knight in this pawn's path - a 1-HP piece,
        // so it's captured outright just like a White Pawn.
        if (pendingCapturedKnight != null &&
            Vector3.Distance(transform.position, pendingCapturedKnight.transform.position) <= attackContactDistance)
        {
            CaptureKnight();
        }

        // Same idea again for a Bishop in this pawn's path - a 1-HP piece,
        // so it's captured outright just like a White Pawn or Knight.
        if (pendingCapturedBishop != null &&
            Vector3.Distance(transform.position, pendingCapturedBishop.transform.position) <= attackContactDistance)
        {
            CaptureBishop();
        }

        // Attacking a Rook plays out one of two ways, decided the instant
        // the hit lands: if it doesn't finish the Rook off, this pawn dies
        // right here (same as attacking the King) without reaching the
        // tile; if it does, this pawn survives and keeps sliding onto the
        // now-empty tile (same as capturing a White Pawn).
        if (pendingAttackedRook != null &&
            Vector3.Distance(transform.position, pendingAttackedRook.transform.position) <= attackContactDistance &&
            !ResolveRookAttack())
        {
            return;
        }

        // Only X/Z are interpolated here - Y is owned entirely by
        // FollowTileHeight, so the pawn keeps tracking its tile's live
        // height (elevated or not) throughout the slide, not just at rest.
        Vector3 position = transform.position;
        Vector2 horizontal = Vector2.MoveTowards(
            new Vector2(position.x, position.z),
            new Vector2(slideTarget.x, slideTarget.z),
            moveSpeed * Time.deltaTime);

        position.x = horizontal.x;
        position.z = horizontal.y;
        transform.position = position;

        if (position.x != slideTarget.x || position.z != slideTarget.z)
        {
            return;
        }

        isSliding = false;
        TurnManager.EndEnemyMove();
        currentTile.SetEnemyDestinationHighlighted(false);

        if (isAttackMove)
        {
            ResolveAttack();
        }
        else
        {
            PlanNextMove();
        }
    }

    // This pawn captured a White Pawn standing in its path - the White Pawn
    // dies, knocked along this pawn's direction of travel; this pawn is
    // unaffected and finishes its move onto that tile as normal.
    private void CaptureWhitePawn()
    {
        WhitePawnController target = pendingCapturedPawn;
        pendingCapturedPawn = null;

        Vector3 direction = previousTile != null
            ? HorizontalDirection(previousTile.transform.position, currentTile.transform.position)
            : HorizontalDirection(target.transform.position, transform.position);

        target.Kill(direction);
    }

    // Applies this pawn's damage to the Rook it just reached. Returns true
    // if the Rook survived the hit (this pawn then dies in return, ending
    // its move here), or false if the hit finished the Rook off (this pawn
    // survives and its Slide() call keeps going, onto the Rook's now-empty
    // tile, exactly like capturing a White Pawn).
    private bool ResolveRookAttack()
    {
        RookController rook = pendingAttackedRook;
        pendingAttackedRook = null;

        bool rookDied = rook.TakeDamage(damage, transform.position);

        if (rookDied)
        {
            return true;
        }

        isSliding = false;
        TurnManager.EndEnemyMove();
        currentTile.SetEnemyDestinationHighlighted(false);

        Vector3 direction = previousTile != null
            ? HorizontalDirection(currentTile.transform.position, previousTile.transform.position)
            : HorizontalDirection(rook.transform.position, transform.position);

        Die(direction);
        return false;
    }

    // This pawn captured a Knight standing in its path - the Knight dies,
    // knocked along this pawn's direction of travel; this pawn is
    // unaffected and finishes its move onto that tile as normal.
    private void CaptureKnight()
    {
        KnightController target = pendingCapturedKnight;
        pendingCapturedKnight = null;

        Vector3 direction = previousTile != null
            ? HorizontalDirection(previousTile.transform.position, currentTile.transform.position)
            : HorizontalDirection(target.transform.position, transform.position);

        target.Kill(direction);
    }

    // This pawn captured a Bishop standing in its path - the Bishop dies,
    // knocked along this pawn's direction of travel; this pawn is
    // unaffected and finishes its move onto that tile as normal.
    private void CaptureBishop()
    {
        BishopController target = pendingCapturedBishop;
        pendingCapturedBishop = null;

        Vector3 direction = previousTile != null
            ? HorizontalDirection(previousTile.transform.position, currentTile.transform.position)
            : HorizontalDirection(target.transform.position, transform.position);

        target.Kill(direction);
    }

    private void ResolveAttack()
    {
        playerHealth?.TakeDamage(damage, transform.position);

        Vector3 direction = previousTile != null
            ? HorizontalDirection(currentTile.transform.position, previousTile.transform.position)
            : HorizontalDirection(player.transform.position, transform.position);

        Die(direction);
    }

    // Called by a friendly piece dealing an actual amount of damage on
    // capture (currently only the Bishop) rather than an unconditional kill.
    // Returns true if this hit destroyed this pawn. Every enemy pawn today
    // has only 1 HP, so any positive amount is always lethal, but this path
    // still checks properly for whenever that stops being true.
    public bool TakeDamage(int amount, Vector3 sourcePosition)
    {
        if (health == null || health.CurrentHP <= 0)
        {
            return health != null && health.CurrentHP <= 0;
        }

        // health.TakeDamage fires OnDeath synchronously the instant it
        // brings HP to 0, so the knockback direction (and the stale planned-
        // move highlight) must already be set before that call, exactly
        // like Die() sets them before calling health.Kill().
        if (plannedDestination != null)
        {
            plannedDestination.SetEnemyDestinationHighlighted(false);
            plannedDestination = null;
        }

        pendingKnockbackDirection = HorizontalDirection(sourcePosition, transform.position);
        health.TakeDamage(amount, sourcePosition);

        return health.CurrentHP <= 0;
    }

    private void Die(Vector3 knockbackDirection)
    {
        // The contact-death path (player moves onto this pawn) never gets
        // to consume plannedDestination the way ExecutePlannedMove normally
        // does, so its highlight would otherwise stay stuck on forever.
        if (plannedDestination != null)
        {
            plannedDestination.SetEnemyDestinationHighlighted(false);
            plannedDestination = null;
        }

        pendingKnockbackDirection = knockbackDirection;
        health?.Kill();
    }

    // Computed from stable, pre-collision tile centers rather than the live
    // transform positions at the moment of death - by the time contact
    // happens the pawn and player are standing on (almost) the same tile, so
    // a "current position minus current position" vector is near zero and
    // its direction is just noise from their small per-piece pivot offsets.
    private static Vector3 HorizontalDirection(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        direction.y = 0f;

        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }

    private void HandleDeath()
    {
        if (xpPickupPrefab != null && currentTile != null && TurnManager.TurnsEnabled)
        {
            Vector3 spawnPosition = currentTile.BaseWorldPosition + Vector3.up * xpSpawnHeight;
            Instantiate(xpPickupPrefab, spawnPosition, xpPickupPrefab.transform.rotation);
        }

        if (rigid == null)
        {
            Destroy(gameObject);
            return;
        }

        rigid.Demolish();

        if (!rigid.HasFragments)
        {
            return;
        }

        Vector3 force = pendingKnockbackDirection * explosionForce + Vector3.up * explosionUpwardForce;

        foreach (RayfireRigid fragment in rigid.fragments)
        {
            if (fragment != null && fragment.physics.rb != null)
            {
                fragment.physics.rb.AddForce(force, ForceMode.Impulse);
            }
        }
    }
}
