using System.Collections.Generic;
using RayFire;
using UnityEngine;

/// <summary>
/// Chess-pawn-style enemy: chases the King, wherever he is on the board, one
/// tile per turn. Under the old End Turn system every pawn moves at once
/// when the player ends their turn; under an InitiativeTurnManager each pawn
/// moves on its own turn instead (see PlanInitiativeTurn and
/// TakeInitiativeTurn). Moves one tile up, down, left or right, onto empty
/// tiles only (walking around Blockers), and takes diagonally, like a real
/// pawn. The enemy Rook, Knight and Bishop (EnemyRookController and co.) are
/// this same pawn with a different way of moving - see ChooseMove and
/// GetAttackTiles.
///
/// What it goes for (see PlanNextMove): any of the player's pieces it can
/// take with its very next move, it takes. Otherwise it heads for the King -
/// unless one of the player's other pieces draws it away: one whose Threat
/// is at least this pawn's Threat Response, while this pawn is within that
/// piece's Threat Range. It never goes hunting for a piece with less Threat
/// than that.
///
/// Attacks work the same both ways, and go by HP. When this pawn's move
/// takes it onto one of the player's pieces (the King included), it deals
/// its Damage the moment it touches it: if that kills the piece, this pawn
/// carries on onto the tile; if not, it slides back to where it came from.
/// When one of the player's pieces moves onto this pawn, it deals its own
/// Capture Damage: if this pawn survives, that piece slides back instead.
///
/// Grass hides both ways. Standing in it, this pawn can't be seen by the
/// player, and neither can the tile it's about to move to (see
/// UpdateHiding). One of the player's pieces standing in it can't be seen by
/// this pawn, which neither heads for it nor takes it, and can't step onto
/// its tile - so with the King in Grass, this pawn stays where it is (see
/// PlanNextMove).
/// </summary>
[RequireComponent(typeof(Health))]
public class EnemyPawnController : MonoBehaviour, ISerializationCallbackReceiver, IDeployablePiece
{
    [Tooltip("World units per second this pawn slides at when stepping onto a tile.")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("Hit points. Starts out as whatever the Health component was set to.")]
    [SerializeField, Min(0)] private int hp;

    [Tooltip("HP taken from one of the player's pieces (the King included) when this pawn moves onto it.")]
    [SerializeField] private int damage = 1;

    [Tooltip("Distance from the player at which an attack move lands, so the pawn stops and resolves the hit right as it touches the player's model instead of sliding into it first. Tune to your models' sizes.")]
    [SerializeField] private float attackContactDistance = 1f;

    [Tooltip("Turn order in battles with an Initiative Turn Manager: the higher, the earlier this pawn acts each round. Player pieces go before enemy pawns on the same Initiative.")]
    [SerializeField] private int initiative = 1;

    public int Initiative => initiative;

    [Tooltip("The lowest Threat one of the player's pieces needs for this pawn to go after it instead of the King, while this pawn is within that piece's Threat Range. A piece this pawn can take with its very next move is taken whatever its Threat.")]
    [SerializeField, Min(0)] private int threatResponse = 1;

    [Header("Fire Warning")]
    [Tooltip("Roots of the fire effects nested under this piece (e.g. Purple-FireWood) - some models have more than one. Lit while this piece still has its turn to come, and always pointed at the King.")]
    [SerializeField] private Transform[] fireEffectRoots = new Transform[0];

    // The single fire slot this used to have. Whatever's still in it from
    // prefabs and scenes set up before is moved into fireEffectRoots as
    // they load (see OnAfterDeserialize).
    [SerializeField, HideInInspector] private Transform fireEffectRoot;

    [Header("Death")]
    [Tooltip("Force applied to fragments, pointing away from the player, when this pawn dies.")]
    [SerializeField] private float explosionForce = 8f;

    [Tooltip("Extra upward force added on top of the away-from-player push.")]
    [SerializeField] private float explosionUpwardForce = 3f;

    public static readonly List<EnemyPawnController> All = new();

    // Still set by TutorialController, but no longer changes anything:
    // every enemy pawn now chases the King regardless of distance anyway.
    public static bool AlwaysTargetPlayer { get; set; }

    private PlayerController player;
    private Health health;
    private RayfireRigid rigid;
    private PieceMoveSound moveSound;

    private Tile currentTile;
    public Tile CurrentTile => currentTile;

    private Tile previousTile;

    // Decided a full turn ahead of when it's actually carried out, so the
    // player can see the threat coming while they're still choosing their
    // own move.
    private Tile plannedDestination;

    // Whether plannedDestination is currently lit up by this pawn. Under End
    // Turn every pawn's is; under initiative turns only the pawns the turn
    // manager picks out (see SetPlanVisible) show theirs.
    private bool isPlanLit;
    private bool isPlanVisible;

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

    // One of the player's pieces standing on this move's destination - hit
    // the moment this pawn's slide gets close enough to it. Set in
    // BeginMoveTo and cleared the instant it's hit, so it's only hit once.
    private ISelectablePiece pendingTarget;

    // Set right before Health.Kill() so HandleDeath (its synchronous
    // OnDeath reaction) knows which way to push the fragments.
    private Vector3 pendingKnockbackDirection = Vector3.forward;

    // The InitiativeTurnManager's "this pawn's turn is over" callback, held
    // from TakeInitiativeTurn until the move ends (see EndMove).
    private System.Action onInitiativeTurnDone;

    // Under initiative turns: whether this pawn has had its turn this round.
    private bool hasTakenTurnThisRound;

    // Out of sight in Grass, along with exactly what was switched off to get
    // it there - so only that is switched back on afterwards.
    private bool isHidden;
    private readonly List<Renderer> hiddenRenderers = new();
    private readonly List<Light> hiddenLights = new();

    // Hidden while standing in Grass - not while moving into or out of it.
    private bool ShouldBeHidden =>
        !isSliding && Grass.Hides(currentTile) && (health == null || health.CurrentHP > 0);

    // An HP of 0 means it was never set here, so Health keeps its own.
    private void Awake()
    {
        if (hp > 0 && TryGetComponent(out Health ownHealth))
        {
            ownHealth.SetHP(hp);
        }
    }

    // Starts the HP field out at whatever Health was already set to, so
    // nothing changes until it's edited.
    private void OnValidate()
    {
        if (hp <= 0 && TryGetComponent(out Health ownHealth))
        {
            hp = ownHealth.StartingHP;
        }
    }

    private void OnEnable()
    {
        All.Add(this);
        PlayerController.OnMoveCompleted += HandlePieceMoved;
        TurnManager.EnemyTurnStarted += HandleEnemyTurnStarted;
        TurnManager.EnemyTurnEnded += HandleTurnEnded;
        BattleSetup.Finished += PlanNextMove;

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
        TurnManager.EnemyTurnStarted -= HandleEnemyTurnStarted;
        TurnManager.EnemyTurnEnded -= HandleTurnEnded;
        BattleSetup.Finished -= PlanNextMove;

        // A pawn that's gone shouldn't leave its planned tile lit behind it.
        ClearPlan();

        // Gone mid-move: still let go of the enemy-turn lock and the turn
        // manager, or both would wait for this pawn forever.
        if (isSliding)
        {
            isSliding = false;
            EndMove();
        }

        if (health != null)
        {
            health.OnDeath -= HandleDeath;
        }
    }

    private void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        rigid = GetComponent<RayfireRigid>();
        moveSound = GetComponent<PieceMoveSound>();

        currentTile = TileGrid.FindNearest(transform.position);

        if (currentTile != null)
        {
            tileOffset = transform.position - currentTile.transform.position;
            tileOffset.y = 0f;
            heightAboveTile = transform.position.y - currentTile.BaseWorldPosition.y;
        }

        UpdateFireWarning();
        PlanNextMove();
    }

    // Off the board, to wait on a Pawn Holder until it's deployed: on no
    // tile, this pawn plans nothing and nothing counts it as in the way.
    public void LeaveBoard()
    {
        ClearPlan();
        currentTile = null;
        previousTile = null;
    }

    // Onto the board from its Pawn Holder: stands this pawn on a tile,
    // centred, as if it had started there.
    public void PlaceOn(Tile tile)
    {
        PiecePivotUtility.CenterOnTile(transform, tile);

        currentTile = tile;
        previousTile = null;
        tileOffset = transform.position - tile.transform.position;
        tileOffset.y = 0f;

        // Against the tile as it stands right now, lifted or not - which is
        // what FollowTileHeight goes by.
        heightAboveTile = transform.position.y - tile.transform.position.y;

        PlanNextMove();
    }

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        // Compared as plain objects: Unity's own null check isn't allowed
        // while loading.
        if ((object)fireEffectRoot == null)
        {
            return;
        }

        if (fireEffectRoots == null || fireEffectRoots.Length == 0)
        {
            fireEffectRoots = new[] { fireEffectRoot };
        }

        fireEffectRoot = null;
    }

    private void Update()
    {
        FollowTileHeight();
        AimFireAtPlayer();

        if (isSliding)
        {
            Slide();
        }

        UpdateHiding();
    }

    // Takes this pawn out of sight when it comes to rest in Grass - model,
    // fires and their light - and brings it back when it leaves. The
    // tile it's about to move to is hidden along with it (see
    // RefreshPlanHighlight).
    private void UpdateHiding()
    {
        bool shouldBeHidden = ShouldBeHidden;

        if (shouldBeHidden == isHidden)
        {
            return;
        }

        isHidden = shouldBeHidden;

        if (isHidden)
        {
            // Inactive children too: a fire that's out right now would
            // otherwise show the moment it's lit again.
            foreach (Renderer pieceRenderer in GetComponentsInChildren<Renderer>(true))
            {
                if (pieceRenderer.enabled)
                {
                    pieceRenderer.enabled = false;
                    hiddenRenderers.Add(pieceRenderer);
                }
            }

            foreach (Light pieceLight in GetComponentsInChildren<Light>(true))
            {
                if (pieceLight.enabled)
                {
                    pieceLight.enabled = false;
                    hiddenLights.Add(pieceLight);
                }
            }
        }
        else
        {
            foreach (Renderer pieceRenderer in hiddenRenderers)
            {
                if (pieceRenderer != null)
                {
                    pieceRenderer.enabled = true;
                }
            }

            foreach (Light pieceLight in hiddenLights)
            {
                if (pieceLight != null)
                {
                    pieceLight.enabled = true;
                }
            }

            hiddenRenderers.Clear();
            hiddenLights.Clear();
        }

        RefreshPlanHighlight();
    }

    // Keeps every lit fire pointed at the player, live, for as long as it's
    // active - not just a one-time snap when it first lights up. While the
    // King's out of sight in Grass they stay pointed where he was last seen.
    private void AimFireAtPlayer()
    {
        if (player == null || fireEffectRoots == null || Grass.Hides(player.CurrentTile))
        {
            return;
        }

        foreach (Transform fire in fireEffectRoots)
        {
            if (fire == null || !fire.gameObject.activeSelf)
            {
                continue;
            }

            Vector3 lookPoint = player.transform.position;
            lookPoint.y = fire.position.y;

            if ((lookPoint - fire.position).sqrMagnitude > 0.0001f)
            {
                fire.LookAt(lookPoint);
            }
        }
    }

    // The fires are lit while this piece still has its turn to come - this
    // round under initiative turns, or this turn under End Turn - and go out
    // once that turn's been used.
    private void UpdateFireWarning()
    {
        if (fireEffectRoots == null)
        {
            return;
        }

        foreach (Transform fire in fireEffectRoots)
        {
            if (fire != null)
            {
                fire.gameObject.SetActive(!hasTakenTurnThisRound);
            }
        }
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
    // a Knight or a Bishop) - an attack resolves the instant one of the
    // player's pieces finishes a move onto this pawn's tile, rather than
    // waiting for End Turn. It deals its Capture Damage; if this pawn
    // survives that, the piece slides back to where it came from.
    private void HandlePieceMoved()
    {
        if (currentTile == null)
        {
            return;
        }

        ISelectablePiece attacker = BoardPieces.FindPlayerPieceOn(currentTile);

        if (attacker != null)
        {
            Vector3 attackerPosition = ((MonoBehaviour)attacker).transform.position;

            if (TakeDamage(attacker.CaptureDamage, attackerPosition))
            {
                return;
            }

            attacker.BounceBack();
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
    private void HandleEnemyTurnStarted()
    {
        // Moving or not, this piece's turn is used up.
        hasTakenTurnThisRound = true;
        UpdateFireWarning();
        ExecutePlannedMove();
    }

    private void ExecutePlannedMove()
    {
        if (currentTile == null || isSliding)
        {
            return;
        }

        Tile destination = plannedDestination;
        bool wasLit = isPlanLit;
        plannedDestination = null;
        isPlanLit = false;

        // Whatever was planned last turn might no longer be valid - another
        // pawn could have claimed that tile in the meantime, or nothing was
        // reachable when it was planned. Either way, clear any stale
        // highlight and try planning fresh for the turn after this one.
        if (destination == null || IsOccupiedByAnotherPawn(destination))
        {
            if (destination != null && wasLit)
            {
                Unlight(destination);
            }

            PlanNextMove();
            return;
        }

        BeginMoveTo(destination);
    }

    // Under initiative turns, whether this pawn shows the tile it's about to
    // move to - the turn manager switches it on for the pawn whose turn it
    // is and the next one due, and off for the rest. Switching it on plans
    // afresh, since the board may have changed since this pawn last looked.
    public void SetPlanVisible(bool visible)
    {
        isPlanVisible = visible;

        if (visible && !isSliding)
        {
            PlanNextMove();
        }
        else
        {
            RefreshPlanHighlight();
        }
    }

    // Lights the planned tile up, or switches it off, to match whether this
    // pawn should be showing it right now. A pawn out of sight in Grass
    // never shows it.
    private void RefreshPlanHighlight()
    {
        bool shouldBeLit = plannedDestination != null && !ShouldBeHidden &&
            (!InitiativeTurnManager.IsRunning || isPlanVisible);

        if (shouldBeLit == isPlanLit)
        {
            return;
        }

        isPlanLit = shouldBeLit;

        if (shouldBeLit)
        {
            plannedDestination.SetEnemyDestinationHighlighted(true);
        }
        else if (plannedDestination != null)
        {
            Unlight(plannedDestination);
        }
    }

    private void ClearPlan()
    {
        if (plannedDestination == null)
        {
            return;
        }

        Tile tile = plannedDestination;
        bool wasLit = isPlanLit;
        plannedDestination = null;
        isPlanLit = false;

        if (wasLit)
        {
            Unlight(tile);
        }
    }

    // Switches a tile's warning off - unless another pawn is showing a move
    // onto that same tile.
    private void Unlight(Tile tile)
    {
        foreach (EnemyPawnController other in All)
        {
            if (other != this && other.isPlanLit && other.plannedDestination == tile)
            {
                return;
            }
        }

        tile.SetEnemyDestinationHighlighted(false);
    }

    // Initiative turns, step one: plans this pawn's move from the board as
    // it is right now, lighting up the tile it's about to move to. False if
    // it has nowhere to go this turn.
    public bool PlanInitiativeTurn()
    {
        if (isSliding)
        {
            return false;
        }

        PlanNextMove();
        return plannedDestination != null;
    }

    // Initiative turns, step two: carries out the move PlanInitiativeTurn
    // planned, calling onDone once it's over - however it ends (arriving,
    // attacking the King, or dying in the attempt) - or straight away if
    // there turns out to be no move to make after all.
    public void TakeInitiativeTurn(System.Action onDone)
    {
        onInitiativeTurnDone = onDone;
        ExecutePlannedMove();

        if (!isSliding)
        {
            FinishInitiativeTurn();
        }
    }

    // Called by InitiativeTurnManager once this pawn's turn is over, whether
    // it moved or had nowhere to go: its fire goes out until the round ends.
    public void EndInitiativeTurn()
    {
        hasTakenTurnThisRound = true;
        UpdateFireWarning();
    }

    // The end of a round (under initiative turns) or of the enemies' turn
    // (under End Turn) - either way, a fresh turn for this pawn.
    private void HandleTurnEnded()
    {
        hasTakenTurnThisRound = false;
        UpdateFireWarning();
    }

    private void FinishInitiativeTurn()
    {
        // Its turn's over, so the move it plans next isn't shown until the
        // turn manager says it's coming up again.
        isPlanVisible = false;

        System.Action done = onInitiativeTurnDone;
        onInitiativeTurnDone = null;
        done?.Invoke();
    }

    // Every way a move can end goes through here.
    private void EndMove()
    {
        TurnManager.EndEnemyMove();
        FinishInitiativeTurn();
    }

    // Decides the tile this pawn will move to next time, lighting it up if
    // this pawn is showing its plan (see RefreshPlanHighlight). Called once
    // right after this pawn's own move concludes, and again every time any
    // friendly piece moves during the player's turn (see HandlePieceMoved)
    // - safe to call repeatedly, since any previously planned tile's
    // highlight is cleared first. Takes whatever it can reach; failing that,
    // heads for its target (see FindTargetTile), and does nothing if it has
    // none or there's no step that gets closer.
    private void PlanNextMove()
    {
        ClearPlan();

        // No turns (the Main Menu), or a battle still in its setup phase:
        // enemies stand still and don't plan.
        if (currentTile == null || !TurnManager.TurnsEnabled || BattleSetup.IsActive)
        {
            return;
        }

        // Anything of the player's this pawn can take with this very move,
        // it takes - whatever that piece's Threat.
        Tile destination = ChooseCapture();

        if (destination == null)
        {
            Tile target = FindTargetTile();

            // Nothing to head for: nothing draws it, and the King's gone, or
            // standing in Grass, where this pawn can't see him.
            if (target == null)
            {
                return;
            }

            destination = ChooseMove(target);
        }

        if (destination == null || destination == currentTile || IsOccupiedByAnotherPawn(destination))
        {
            return;
        }

        plannedDestination = destination;
        RefreshPlanHighlight();
    }

    // The tile this pawn heads for. One of the player's pieces draws it away
    // from the King if that piece's Threat is at least this pawn's Threat
    // Response and this pawn is within its Threat Range (see
    // BoardPieces.FindThreatTo). Otherwise it's the King's tile - or
    // nothing, if he's gone or out of sight in Grass. Worked out afresh
    // every time this pawn plans, so a pawn that ends up out of a piece's
    // range goes back to the King.
    private Tile FindTargetTile()
    {
        ISelectablePiece threat = BoardPieces.FindThreatTo(currentTile, threatResponse);

        if (threat != null)
        {
            return threat.CurrentTile;
        }

        Tile kingTile = player != null && player.isActiveAndEnabled ? player.CurrentTile : null;
        return Grass.Hides(kingTile) ? null : kingTile;
    }

    // One of the player's pieces this pawn can take with its very next move,
    // or null if there's none in reach. Threat doesn't come into whether -
    // only into which: the King first, then the biggest Threat, then the
    // nearest. A piece it can't see, in Grass, can't be taken.
    private Tile ChooseCapture()
    {
        Tile best = null;
        int bestThreat = int.MinValue;
        int bestDistance = int.MaxValue;

        foreach (Tile tile in GetAttackTiles())
        {
            if (Grass.Hides(tile))
            {
                continue;
            }

            ISelectablePiece piece = BoardPieces.FindPlayerPieceOn(tile);

            if (piece == null)
            {
                continue;
            }

            if (piece is PlayerController)
            {
                return tile;
            }

            int distance = TileGrid.GetDistance(currentTile, tile);

            if (piece.Threat > bestThreat || (piece.Threat == bestThreat && distance < bestDistance))
            {
                best = tile;
                bestThreat = piece.Threat;
                bestDistance = distance;
            }
        }

        return best;
    }

    // Every tile this piece could take one of the player's pieces on with
    // its next move. For a pawn that's its four diagonal neighbors - it
    // never takes straight on. The enemy Rook, Knight and Bishop override
    // this with their own reach.
    protected virtual List<Tile> GetAttackTiles()
    {
        List<Tile> tiles = new();

        if (!TileGrid.TryGetCoord(currentTile, out Vector2Int coord))
        {
            return tiles;
        }

        foreach (Vector2Int direction in DiagonalDirections)
        {
            Tile tile = TileGrid.GetTileAt(coord + direction);

            if (tile != null)
            {
                tiles.Add(tile);
            }
        }

        return tiles;
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

    // Picks the tile this piece moves to next, heading for target (the tile
    // of the King, or of a piece that's drawn it away from him), or null to
    // stay put. The enemy Rook, Knight and Bishop override this with their
    // own way of moving - everything else about them works exactly as it
    // does for this pawn.
    protected virtual Tile ChooseMove(Tile target)
    {
        if (!TileGrid.TryGetCoord(currentTile, out Vector2Int currentCoord) ||
            !TileGrid.TryGetCoord(target, out Vector2Int targetCoord))
        {
            return null;
        }

        return ChoosePawnMove(currentCoord, targetCoord);
    }

    protected static readonly Vector2Int[] StraightDirections =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    protected static readonly Vector2Int[] DiagonalDirections =
    {
        new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
    };

    // Two tiles in one direction, then one to either side.
    protected static readonly Vector2Int[] KnightJumps =
    {
        new(1, 2), new(-1, 2), new(1, -2), new(-1, -2),
        new(2, 1), new(-2, 1), new(2, -1), new(-2, -1)
    };

    // Every tile a sliding piece (a rook or bishop) can move to from a tile:
    // up to maxRange tiles along each direction, stopping before another
    // enemy, and at - but never past - one of the player's pieces, which it
    // can take. One it can't see, in Grass, stops it short like another
    // enemy would.
    protected List<Tile> GetSlideMoves(Tile from, Vector2Int[] directions, int maxRange)
    {
        List<Tile> moves = new();

        if (!TileGrid.TryGetCoord(from, out Vector2Int coord))
        {
            return moves;
        }

        foreach (Vector2Int direction in directions)
        {
            for (int step = 1; step <= maxRange; step++)
            {
                Tile tile = TileGrid.GetTileAt(coord + direction * step);

                if (tile == null || IsOccupiedByAnotherPawn(tile) || BoardPieces.IsHiddenPlayerPieceOn(tile))
                {
                    break;
                }

                moves.Add(tile);

                if (BoardPieces.IsPlayerPieceOn(tile))
                {
                    break;
                }
            }
        }

        return moves;
    }

    // Every tile a jumping piece (a knight) can land on from a tile. What's
    // in between doesn't matter - only that the landing tile holds no other
    // enemy, and none of the player's pieces it can't see, in Grass.
    protected List<Tile> GetJumpMoves(Tile from, Vector2Int[] offsets)
    {
        List<Tile> moves = new();

        if (!TileGrid.TryGetCoord(from, out Vector2Int coord))
        {
            return moves;
        }

        foreach (Vector2Int offset in offsets)
        {
            Tile tile = TileGrid.GetTileAt(coord + offset);

            if (tile != null && !IsOccupiedByAnotherPawn(tile) && !BoardPieces.IsHiddenPlayerPieceOn(tile))
            {
                moves.Add(tile);
            }
        }

        return moves;
    }

    // Heads for target by the fewest moves this piece's own way of moving
    // allows - a search outward over the board, move by move, where other
    // enemies block and the player's pieces can be taken but not passed -
    // and returns the first move along that route. With no route at all
    // (walled in by other enemies, say), takes whichever move ends up
    // closest to the target instead, as long as that's closer than staying
    // put. Null if nothing helps.
    protected Tile ChooseAlongShortestRoute(Tile target, System.Func<Tile, List<Tile>> getMoves)
    {
        Dictionary<Tile, Tile> cameFrom = new() { [currentTile] = null };
        Queue<Tile> frontier = new();
        frontier.Enqueue(currentTile);

        while (frontier.Count > 0)
        {
            Tile tile = frontier.Dequeue();

            if (tile == target)
            {
                Tile step = target;

                while (cameFrom[step] != currentTile)
                {
                    step = cameFrom[step];
                }

                return step;
            }

            foreach (Tile next in getMoves(tile))
            {
                if (!cameFrom.ContainsKey(next))
                {
                    cameFrom[next] = tile;
                    frontier.Enqueue(next);
                }
            }
        }

        return ChooseClosestMove(target, getMoves(currentTile));
    }

    private Tile ChooseClosestMove(Tile target, List<Tile> moves)
    {
        if (!TileGrid.TryGetCoord(target, out Vector2Int targetCoord) ||
            !TileGrid.TryGetCoord(currentTile, out Vector2Int ownCoord))
        {
            return null;
        }

        Tile best = null;
        int bestDistance = (ownCoord - targetCoord).sqrMagnitude;

        foreach (Tile move in moves)
        {
            if (!TileGrid.TryGetCoord(move, out Vector2Int moveCoord))
            {
                continue;
            }

            int distance = (moveCoord - targetCoord).sqrMagnitude;

            if (distance < bestDistance)
            {
                best = move;
                bestDistance = distance;
            }
        }

        return best;
    }

    // A pawn steps one tile up, down, left or right, and only onto an empty
    // tile - it can't take anything that way, same as a real pawn facing an
    // occupied square ahead of it. Taking is done diagonally, and has
    // already been tried by the time this is called (see ChooseCapture). It
    // walks around Blockers, but not around other pieces: with those in the
    // way it waits.
    private Tile ChoosePawnMove(Vector2Int currentCoord, Vector2Int targetCoord)
    {
        int deltaCol = targetCoord.x - currentCoord.x;
        int deltaRow = targetCoord.y - currentCoord.y;

        // Advance one tile orthogonally, onto an empty tile that leaves it a
        // shorter walk from its target. On open ground that's a step
        // straight at it; with a Blocker in the way, it's the way around.
        Dictionary<Vector2Int, int> walk = MeasureWalkTo(targetCoord);

        if (walk.TryGetValue(currentCoord, out int ownWalk))
        {
            // Closing whichever gap is bigger comes first, then the other
            // one, then the two directions that only ever help when going
            // around something. A gap of zero makes no step at all, which
            // is never a shorter walk, so it drops out by itself.
            Vector2Int columnStep = new(System.Math.Sign(deltaCol), 0);
            Vector2Int rowStep = new(0, System.Math.Sign(deltaRow));
            bool columnGapIsBigger = Mathf.Abs(deltaCol) >= Mathf.Abs(deltaRow);

            List<Vector2Int> steps = new()
            {
                columnGapIsBigger ? columnStep : rowStep,
                columnGapIsBigger ? rowStep : columnStep
            };

            steps.AddRange(StraightDirections);

            foreach (Vector2Int step in steps)
            {
                Vector2Int coord = currentCoord + step;

                if (!walk.TryGetValue(coord, out int stepWalk) || stepWalk >= ownWalk)
                {
                    continue;
                }

                Tile move = GetEmptyTile(coord);

                if (move != null)
                {
                    return move;
                }
            }
        }

        return null;
    }

    // How many single straight steps each tile is from the target's, going
    // around Blockers and gaps in the board. Pieces standing in the way
    // aren't counted - they may well have moved by the time it matters.
    // Tiles with no way through to the target aren't in it at all.
    private static Dictionary<Vector2Int, int> MeasureWalkTo(Vector2Int targetCoord)
    {
        Dictionary<Vector2Int, int> walk = new() { [targetCoord] = 0 };
        Queue<Vector2Int> frontier = new();
        frontier.Enqueue(targetCoord);

        while (frontier.Count > 0)
        {
            Vector2Int coord = frontier.Dequeue();
            int nextWalk = walk[coord] + 1;

            foreach (Vector2Int direction in StraightDirections)
            {
                Vector2Int neighbor = coord + direction;

                if (!walk.ContainsKey(neighbor) && TileGrid.GetTileAt(neighbor) != null)
                {
                    walk[neighbor] = nextWalk;
                    frontier.Enqueue(neighbor);
                }
            }
        }

        return walk;
    }

    // The tile at coord, if a pawn can step straight onto it: it exists, no
    // Blocker closes it off, and nothing - neither side's pieces - is
    // standing on it.
    private Tile GetEmptyTile(Vector2Int coord)
    {
        Tile tile = TileGrid.GetTileAt(coord);

        if (tile == null || BoardPieces.IsPlayerPieceOn(tile) || IsOccupiedByAnotherPawn(tile))
        {
            return null;
        }

        return tile;
    }

    private void BeginMoveTo(Tile target)
    {
        previousTile = currentTile;
        pendingTarget = BoardPieces.FindPlayerPieceOn(target);

        // Already highlighted from when this move was planned last turn;
        // this just keeps it lit (a no-op if already on) through the slide.
        target.SetEnemyDestinationHighlighted(true);

        SetSlideTarget(target);
        isSliding = true;

        currentTile = target;

        // Setting off out of Grass brings this pawn back into view.
        // Its renderer won't count as visible until a frame's been drawn, so
        // the sound goes by where it stands on screen instead.
        bool wasHidden = isHidden;
        UpdateHiding();

        if (wasHidden)
        {
            moveSound?.PlayIfOnScreen();
        }
        else
        {
            moveSound?.PlayIfVisible();
        }

        TurnManager.BeginEnemyMove();
    }

    private void SetSlideTarget(Tile tile)
    {
        slideTarget = transform.position;
        slideTarget.x = tile.transform.position.x + tileOffset.x;
        slideTarget.z = tile.transform.position.z + tileOffset.z;
    }

    private void Slide()
    {
        // The hit lands the moment this pawn's model reaches the piece,
        // instead of sliding all the way to the tile center first -
        // otherwise it would visibly phase into it.
        if (pendingTarget != null && pendingTarget is MonoBehaviour targetBehaviour &&
            Vector3.Distance(transform.position, targetBehaviour.transform.position) <= attackContactDistance)
        {
            ISelectablePiece target = pendingTarget;
            pendingTarget = null;

            // Killed: the tile's free, so this pawn carries on onto it. Not
            // killed: it slides back to where it came from.
            if (!target.TakeHit(damage, transform.position))
            {
                BounceBack();
            }
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
        EndMove();
        Unlight(currentTile);
        PlanNextMove();
    }

    // Turns this move around after a hit that didn't kill: back to the tile
    // it came from, where the move then ends as usual.
    private void BounceBack()
    {
        if (previousTile == null)
        {
            return;
        }

        Unlight(currentTile);
        currentTile = previousTile;
        SetSlideTarget(currentTile);
    }

    // A hit from one of the player's pieces moving onto this pawn. Returns
    // true if it destroyed this pawn.
    public bool TakeDamage(int amount, Vector3 sourcePosition)
    {
        if (health == null || health.CurrentHP <= 0)
        {
            return health != null && health.CurrentHP <= 0;
        }

        // health.TakeDamage fires OnDeath synchronously the instant it
        // brings HP to 0, so the knockback direction must already be set
        // before that call. The planned move is cleared either way - a hit
        // means the board just changed, and a dead pawn's highlight would
        // otherwise stay stuck on forever.
        ClearPlan();

        pendingKnockbackDirection = HorizontalDirection(sourcePosition, transform.position);
        health.TakeDamage(amount, sourcePosition);

        return health.CurrentHP <= 0;
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
        // Killed while out of sight in Grass: back into view first,
        // so it breaks apart like any other pawn.
        UpdateHiding();

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
