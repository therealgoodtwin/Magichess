using System.Collections.Generic;
using RayFire;
using UnityEngine;

/// <summary>
/// A player-controlled White Pawn: click it (or the tile it stands on) to
/// select it, same as the King. Moves like a chess pawn, not a king - one
/// tile straight up, down, left or right, except that advance is blocked if
/// an enemy pawn is standing there; it can only capture by stepping
/// diagonally onto a tile an enemy occupies. PieceSelectionManager owns
/// input and decides whether the King or a White Pawn is the active
/// selection.
/// </summary>
public class WhitePawnController : MonoBehaviour, ISelectablePiece, IDeployablePiece
{
    [Tooltip("World units per second this pawn slides at when stepping onto a tile.")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("Hit points. Starts out as whatever the Health component was set to.")]
    [SerializeField, Min(0)] private int hp;
    [Tooltip("HP dealt to an enemy when this pawn moves onto its tile. If the enemy survives, this pawn slides back to where it came from.")]
    [SerializeField] private int captureDamage = 3;

    public int CaptureDamage => captureDamage;

    [Tooltip("Turn order in battles with an Initiative Turn Manager: the higher, the earlier this piece acts each round. Player pieces go before enemy pawns on the same Initiative.")]
    [SerializeField] private int initiative = 1;

    public int Initiative => initiative;

    [Tooltip("How much of a threat this pawn is to the enemy. An enemy within Threat Range goes for this pawn instead of the King if this is at least that enemy's Threat Response.")]
    [SerializeField, Min(0)] private int threat = 1;

    [Tooltip("How close an enemy has to be, in tiles, for this pawn's Threat to draw it. A diagonal step counts as one tile. 0 draws nothing.")]
    [SerializeField, Min(0)] private int threatRange = 2;

    public int Threat => threat;
    public int ThreatRange => threatRange;

    [Tooltip("Root of this pawn's fire effect (e.g. Blue-FireWood). Switched off the first time this pawn is activated (clicked and moved).")]
    [SerializeField] private Transform fireEffectRoot;

    [Header("Death")]
    [Tooltip("Force applied to fragments, pointing away from whatever captured this pawn, when it dies.")]
    [SerializeField] private float explosionForce = 8f;

    [Tooltip("Extra upward force added on top of the away-from-attacker push.")]
    [SerializeField] private float explosionUpwardForce = 3f;

    public static readonly List<WhitePawnController> All = new();

    // Fired with the pawn that just finished a move, so something like a
    // turn/activation tracker can tell which piece was activated.
    public static event System.Action<WhitePawnController> PawnMoved;

    private Tile currentTile;
    public Tile CurrentTile => currentTile;

    private Tile previousTile;
    public Tile PreviousTile => previousTile;

    public bool IsMoving => isSliding;

    public bool HasBeenActivated { get; private set; }

    // Whether the last Select() found anywhere this piece can move.
    public bool HasReachableTiles => reachableTiles.Count > 0;

    private readonly List<Tile> reachableTiles = new();

    // Same pivot-offset fix as the King: keeps whatever X/Z offset this
    // piece was placed with on its starting tile, and re-applies it on
    // every tile it steps onto so it stays visually centered.
    private Vector3 tileOffset;

    // How far above a tile's resting surface this pawn's model sits. Tiles
    // rise while highlighted (reachable by the King), so this pawn's world Y
    // is re-derived from its current tile's live height every frame instead
    // of being fixed - otherwise it would clip into a tile that lifts while
    // this pawn is standing on it.
    private float heightAboveTile;

    private bool isSliding;
    private Vector3 slideTarget;
    // Sliding back to the tile it came from, after an attack that didn't kill.
    private bool isBouncing;

    private Health health;
    private RayfireRigid rigid;
    private PieceMoveSound moveSound;

    // Set right before a hit lands so HandleDeath (its synchronous
    // OnDeath reaction) knows which way to push the fragments.
    private Vector3 pendingKnockbackDirection = Vector3.forward;

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
        TurnManager.EnemyTurnEnded += HandleTurnEnded;

        PieceActivationLimit.Activated += HandleActivationLimitReached;

        health = GetComponent<Health>();

        if (health != null)
        {
            health.OnDeath += HandleDeath;
        }
    }

    private void OnDisable()
    {
        All.Remove(this);
        TurnManager.EnemyTurnEnded -= HandleTurnEnded;
        PieceActivationLimit.Activated -= HandleActivationLimitReached;
        Deselect();

        if (health != null)
        {
            health.OnDeath -= HandleDeath;
        }
    }

    private void Start()
    {
        rigid = GetComponent<RayfireRigid>();
        moveSound = GetComponent<PieceMoveSound>();

        currentTile = TileGrid.FindNearest(transform.position);

        if (currentTile != null)
        {
            tileOffset = transform.position - currentTile.transform.position;
            tileOffset.y = 0f;
            heightAboveTile = transform.position.y - currentTile.BaseWorldPosition.y;
        }
    }

    private void Update()
    {
        FollowTileHeight();

        if (isSliding)
        {
            Slide();
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

    // Off the board, to wait on a Pawn Holder until it's deployed: on no
    // tile, this pawn has nowhere to move and nothing counts it as in the
    // way.
    public void LeaveBoard()
    {
        Deselect();
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
    }

    public void Select()
    {
        RefreshReachableTiles();
    }

    public void Deselect()
    {
        SetReachableHighlighted(false);
        reachableTiles.Clear();
    }

    public bool TryMoveTo(Tile tile)
    {
        if (!reachableTiles.Contains(tile))
        {
            return false;
        }

        BeginMoveTo(tile);
        return true;
    }

    private void BeginMoveTo(Tile target)
    {
        SetReachableHighlighted(false);
        reachableTiles.Clear();

        previousTile = currentTile;

        slideTarget = transform.position;
        slideTarget.x = target.transform.position.x + tileOffset.x;
        slideTarget.z = target.transform.position.z + tileOffset.z;
        isSliding = true;

        currentTile = target;
        moveSound?.PlayIfVisible();
    }

    public bool TakeHit(int amount, Vector3 sourcePosition)
    {
        if (health == null)
        {
            return false;
        }

        // Set before the hit lands, since a fatal one runs HandleDeath
        // straight away.
        Vector3 direction = transform.position - sourcePosition;
        direction.y = 0f;
        pendingKnockbackDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

        health.TakeDamage(amount, sourcePosition);
        return health.CurrentHP <= 0;
    }
    public void BounceBack()
    {
        if (previousTile == null)
        {
            return;
        }

        currentTile = previousTile;
        slideTarget = transform.position;
        slideTarget.x = currentTile.transform.position.x + tileOffset.x;
        slideTarget.z = currentTile.transform.position.z + tileOffset.z;
        isSliding = true;
        isBouncing = true;
    }
    private void Slide()
    {
        // Only X/Z are interpolated here - Y is owned entirely by
        // FollowTileHeight, so this pawn keeps tracking its tile's live
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

        // Back from an attack that didn't kill - the move itself was
        // already counted when it first landed.
        if (isBouncing)
        {
            isBouncing = false;
            return;
        }

        HasBeenActivated = true;

        // Locks out every other Pawn and Rook for the rest of this turn too
        // (see PieceSelectionManager.SelectPiece), and switches all of their
        // fire off right along with this one's - not just this pawn's own.
        PieceActivationLimit.MarkActivated();

        Deselect();
        PawnMoved?.Invoke(this);
        PlayerController.RaiseMoveCompleted();
    }

    private void SetFireActive(bool active)
    {
        if (fireEffectRoot != null)
        {
            fireEffectRoot.gameObject.SetActive(active);
        }
    }

    private void HandleActivationLimitReached()
    {
        SetFireActive(false);
    }

    // Called by InitiativeTurnManager once this piece's turn is over, moved
    // or passed: its fire goes out until the round ends.
    public void EndInitiativeTurn()
    {
        SetFireActive(false);
    }

    // The new turn starting back up is the same moment TurnActivationController
    // resets who's been activated, so bring this pawn's fire back on and let
    // it be selected again then too - it'll switch off (and lock again) the
    // next time this pawn is actually activated.
    private void HandleTurnEnded()
    {
        HasBeenActivated = false;
        PieceActivationLimit.ResetForNewTurn();
        SetFireActive(true);
    }

    private void HandleDeath()
    {
        enabled = false;

        if (rigid == null)
        {
            gameObject.SetActive(false);
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

    // Same chess-pawn rule the enemy pawns already use: advances straight
    // (up/down/left/right), but that advance is blocked if an enemy is
    // standing there, since a pawn can't capture head-on - it can only
    // capture by stepping diagonally onto a tile an enemy occupies. Only
    // enemies the player can see count: one hidden in Grass leaves its tile
    // looking free, and this pawn attacks it by stepping into it.
    private void RefreshReachableTiles()
    {
        SetReachableHighlighted(false);
        reachableTiles.Clear();

        if (currentTile == null)
        {
            return;
        }

        // A tile another of the player's pieces is on blocks the advance too.
        foreach (Tile tile in TileGrid.GetOrthogonalNeighbors(currentTile))
        {
            if (!BoardPieces.IsVisibleEnemyOn(tile) && !BoardPieces.IsPlayerPieceOn(tile, this))
            {
                reachableTiles.Add(tile);
            }
        }

        foreach (Tile tile in TileGrid.GetDiagonalNeighbors(currentTile))
        {
            if (BoardPieces.IsVisibleEnemyOn(tile))
            {
                reachableTiles.Add(tile);
            }
        }

        SetReachableHighlighted(true);
    }

    private void SetReachableHighlighted(bool highlighted)
    {
        foreach (Tile tile in reachableTiles)
        {
            tile.SetHighlighted(highlighted);
        }
    }
}
