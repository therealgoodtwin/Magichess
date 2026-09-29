using System.Collections.Generic;
using RayFire;
using UnityEngine;

/// <summary>
/// A player-controlled Bishop: click it (or the tile it stands on) to select
/// it, same as the King, White Pawns, the Rook and the Knight. Moves
/// diagonally, up to 10 tiles, in one of the four diagonal directions - the
/// first occupied tile in each direction blocks it: a friendly piece blocks
/// that whole direction (can't land on or pass it), an enemy pawn can be
/// landed on but blocks anything beyond it. Counts against the same
/// one-extra-piece-per-turn limit as White Pawns, the Rook and the Knight
/// (see PieceActivationLimit), and its fire switches off the same way. Has 1
/// HP, same as a White Pawn or Knight - any hit destroys it outright.
///
/// Unlike every other piece, moving onto an enemy pawn's tile doesn't kill it
/// outright - it deals CaptureDamage (3 by default) to that pawn's Health
/// instead. Every enemy pawn today only has 1 HP, so this is still always
/// lethal, but a tougher enemy could survive it. See
/// EnemyPawnController.HandlePieceMoved, which reads CaptureDamage.
/// </summary>
public class BishopController : MonoBehaviour, ISelectablePiece
{
    [Tooltip("World units per second this bishop slides at when stepping onto a tile.")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("Furthest a bishop may travel in one move, in tiles.")]
    [SerializeField] private int maxRange = 10;

    [Tooltip("HP dealt to an enemy pawn's Health when this bishop moves onto its tile, instead of an unconditional kill.")]
    [SerializeField] private int captureDamage = 3;

    public int CaptureDamage => captureDamage;

    [Tooltip("Root of this bishop's fire effect (e.g. Blue-FireWood). Switched off the first time this bishop - or any other piece sharing the one-extra-piece limit - is activated.")]
    [SerializeField] private Transform fireEffectRoot;

    [Header("Death")]
    [Tooltip("Force applied to fragments, pointing away from whatever captured this bishop, when it dies.")]
    [SerializeField] private float explosionForce = 8f;

    [Tooltip("Extra upward force added on top of the away-from-attacker push.")]
    [SerializeField] private float explosionUpwardForce = 3f;

    public static readonly List<BishopController> All = new();

    // Fired with the bishop that just finished a move, so something like
    // TurnActivationController can tell a bishop (not a King, Pawn, Rook or
    // Knight) moved.
    public static event System.Action<BishopController> BishopMoved;

    private static readonly Vector2Int[] Directions =
    {
        new(1, 1), new(1, -1), new(-1, 1), new(-1, -1)
    };

    private Tile currentTile;
    public Tile CurrentTile => currentTile;

    private Tile previousTile;
    public Tile PreviousTile => previousTile;

    public bool IsMoving => isSliding;

    public bool HasBeenActivated { get; private set; }

    private readonly List<Tile> reachableTiles = new();

    // Same pivot-offset fix as the rest of the pieces: keeps whatever X/Z
    // offset this piece was placed with on its starting tile, and
    // re-applies it on every tile it steps onto so it stays visually
    // centered.
    private Vector3 tileOffset;

    // How far above a tile's resting surface this bishop's model sits, re-
    // derived from its current tile's live height every frame (tiles rise
    // while highlighted), same as the rest of the pieces.
    private float heightAboveTile;

    private bool isSliding;
    private Vector3 slideTarget;

    private Health health;
    private RayfireRigid rigid;
    private PieceMoveSound moveSound;
    private PlayerController king;

    // Set right before Health.Kill() so HandleDeath (its synchronous
    // OnDeath reaction) knows which way to push the fragments.
    private Vector3 pendingKnockbackDirection = Vector3.forward;

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
        king = FindFirstObjectByType<PlayerController>();

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

    private void Slide()
    {
        // Only X/Z are interpolated here - Y is owned entirely by
        // FollowTileHeight, so this bishop keeps tracking its tile's live
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
        HasBeenActivated = true;

        // Locks out every White Pawn, Rook, Knight and other Bishop for the
        // rest of this turn too (see PieceSelectionManager.SelectPiece), and
        // switches all of their fire off right along with this one's.
        PieceActivationLimit.MarkActivated();

        Deselect();
        BishopMoved?.Invoke(this);
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

    private void HandleTurnEnded()
    {
        HasBeenActivated = false;
        PieceActivationLimit.ResetForNewTurn();
        SetFireActive(true);
    }

    // Called by an EnemyPawnController that just captured this bishop.
    public void Kill(Vector3 knockbackDirection)
    {
        pendingKnockbackDirection = knockbackDirection;
        health?.Kill();
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

    // Slides up to maxRange tiles in each of the four diagonal directions,
    // stopping before any tile a friendly piece occupies (it blocks that
    // whole direction) and including - but not going past - the first tile
    // an enemy pawn occupies (it can be landed on and attacked for
    // CaptureDamage; see EnemyPawnController.HandlePieceMoved).
    private void RefreshReachableTiles()
    {
        SetReachableHighlighted(false);
        reachableTiles.Clear();

        if (currentTile == null || !TileGrid.TryGetCoord(currentTile, out Vector2Int coord))
        {
            return;
        }

        foreach (Vector2Int direction in Directions)
        {
            for (int step = 1; step <= maxRange; step++)
            {
                Tile tile = TileGrid.GetTileAt(coord + direction * step);

                if (tile == null || IsOccupiedByFriendly(tile))
                {
                    break;
                }

                reachableTiles.Add(tile);

                if (IsEnemyOnTile(tile))
                {
                    break;
                }
            }
        }

        SetReachableHighlighted(true);
    }

    private bool IsOccupiedByFriendly(Tile tile)
    {
        if (king != null && king.CurrentTile == tile)
        {
            return true;
        }

        foreach (WhitePawnController pawn in WhitePawnController.All)
        {
            if (pawn.CurrentTile == tile)
            {
                return true;
            }
        }

        foreach (RookController rook in RookController.All)
        {
            if (rook.CurrentTile == tile)
            {
                return true;
            }
        }

        foreach (KnightController knight in KnightController.All)
        {
            if (knight.CurrentTile == tile)
            {
                return true;
            }
        }

        foreach (BishopController bishop in All)
        {
            if (bishop != this && bishop.CurrentTile == tile)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEnemyOnTile(Tile tile)
    {
        foreach (EnemyPawnController pawn in EnemyPawnController.All)
        {
            if (pawn.CurrentTile == tile)
            {
                return true;
            }
        }

        return false;
    }

    private void SetReachableHighlighted(bool highlighted)
    {
        foreach (Tile tile in reachableTiles)
        {
            tile.SetHighlighted(highlighted);
        }
    }
}
