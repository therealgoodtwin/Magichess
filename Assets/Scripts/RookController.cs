using System.Collections.Generic;
using RayFire;
using UnityEngine;

/// <summary>
/// A player-controlled Rook: click it (or the tile it stands on) to select
/// it, same as the King and White Pawns. Moves like a chess rook - any
/// number of tiles up to 7, in a straight line up/down/left/right - and is
/// blocked by the first occupied tile in each direction. A friendly piece
/// blocks that direction entirely (the Rook can't land on or pass it); an
/// enemy pawn can be landed on and captured, but blocks anything beyond it.
/// Counts against the same one-extra-piece-per-turn limit as White Pawns
/// (see PieceActivationLimit), and its fire switches off the same way.
///
/// Has 2 HP (set on its Health component) rather than the usual 1. When an
/// enemy pawn attacks it: if the hit doesn't kill it, the attacker is
/// destroyed in return (same as attacking the King); if the hit finishes it
/// off, the attacker survives and takes its tile (same as capturing a White
/// Pawn). See EnemyPawnController.ResolveRookAttack.
/// </summary>
public class RookController : MonoBehaviour, ISelectablePiece
{
    [Tooltip("World units per second this rook slides at when stepping onto a tile.")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("Furthest a rook may travel in one move, in tiles.")]
    [SerializeField] private int maxRange = 7;

    [Tooltip("HP dealt to an enemy pawn's Health when this rook moves onto its tile, instead of an unconditional kill. Separate from the return-damage rule that applies when an enemy attacks this rook instead.")]
    [SerializeField] private int captureDamage = 3;

    public int CaptureDamage => captureDamage;

    [Tooltip("Root of this rook's fire effect (e.g. Blue-FireWood). Switched off the first time this rook - or any other piece sharing the one-extra-piece limit - is activated.")]
    [SerializeField] private Transform fireEffectRoot;

    [Header("Death")]
    [Tooltip("Force applied to fragments, pointing away from whatever destroyed this rook, when it dies.")]
    [SerializeField] private float explosionForce = 8f;

    [Tooltip("Extra upward force added on top of the away-from-attacker push.")]
    [SerializeField] private float explosionUpwardForce = 3f;

    public static readonly List<RookController> All = new();

    // Fired with the rook that just finished a move, so something like
    // TurnActivationController can tell a rook (not a King or Pawn) moved.
    public static event System.Action<RookController> RookMoved;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    private Tile currentTile;
    public Tile CurrentTile => currentTile;

    private Tile previousTile;
    public Tile PreviousTile => previousTile;

    public bool IsMoving => isSliding;

    public bool HasBeenActivated { get; private set; }

    private readonly List<Tile> reachableTiles = new();

    // Same pivot-offset fix as the King and Pawns: keeps whatever X/Z offset
    // this piece was placed with on its starting tile, and re-applies it on
    // every tile it steps onto so it stays visually centered.
    private Vector3 tileOffset;

    // How far above a tile's resting surface this rook's model sits, re-
    // derived from its current tile's live height every frame (tiles rise
    // while highlighted), same as the King and Pawns.
    private float heightAboveTile;

    private bool isSliding;
    private Vector3 slideTarget;

    private Health health;
    private RayfireRigid rigid;
    private PieceMoveSound moveSound;
    private PlayerController king;

    // Set right before Health.OnDeath fires (via TakeDamage) so HandleDeath
    // (its synchronous reaction) knows which way to push the fragments.
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
        // FollowTileHeight, so this rook keeps tracking its tile's live
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

        // Locks out every White Pawn and other Rook for the rest of this
        // turn too (see PieceSelectionManager.SelectPiece), and switches all
        // of their fire off right along with this one's.
        PieceActivationLimit.MarkActivated();

        Deselect();
        RookMoved?.Invoke(this);
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

    // Called by an EnemyPawnController whose attack finished this rook off.
    // Returns true if that damage actually killed it, same as Health itself
    // would report, so the caller knows whether to take this rook's tile.
    public bool TakeDamage(int amount, Vector3 sourcePosition)
    {
        if (health == null)
        {
            return false;
        }

        pendingKnockbackDirection = HorizontalDirection(sourcePosition, transform.position);
        health.TakeDamage(amount, sourcePosition);
        return health.CurrentHP <= 0;
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

    private static Vector3 HorizontalDirection(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        direction.y = 0f;

        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }

    // Slides up to maxRange tiles in each of the four cardinal directions,
    // stopping before any tile a friendly piece occupies (it blocks that
    // whole direction) and including - but not going past - the first tile
    // an enemy pawn occupies (it can be landed on and captured).
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

        foreach (RookController rook in All)
        {
            if (rook != this && rook.CurrentTile == tile)
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
