using System.Collections.Generic;
using RayFire;
using UnityEngine;

/// <summary>
/// A player-controlled Knight: click it (or the tile it stands on) to select
/// it, same as the King, White Pawns and the Rook. Moves in an L/Γ shape,
/// like a chess knight - two tiles in one of the four cardinal directions,
/// then one tile to either side, for 8 possible destinations 3 tiles away in
/// total. Unlike every other piece, it jumps clean over anything in between -
/// only the destination tile matters, and it can land there as long as no
/// friendly piece already occupies it (an enemy pawn there is captured
/// instead). Counts against the same one-extra-piece-per-turn limit as White
/// Pawns and the Rook (see PieceActivationLimit), and its fire switches off
/// the same way. Has 1 HP - a single hit from an enemy pawn destroys it
/// outright, same as a White Pawn, with no return damage to the attacker.
/// </summary>
public class KnightController : MonoBehaviour, ISelectablePiece
{
    [Tooltip("World units per second this knight slides at when stepping onto a tile.")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("HP dealt to an enemy pawn's Health when this knight moves onto its tile, instead of an unconditional kill.")]
    [SerializeField] private int captureDamage = 3;

    public int CaptureDamage => captureDamage;

    [Tooltip("Roots of this knight's fire effects (e.g. Blue-FireWood) - this model has more than one. All are switched off the first time this knight - or any other piece sharing the one-extra-piece limit - is activated.")]
    [SerializeField] private Transform[] fireEffectRoots;

    [Header("Death")]
    [Tooltip("Force applied to fragments, pointing away from whatever captured this knight, when it dies.")]
    [SerializeField] private float explosionForce = 8f;

    [Tooltip("Extra upward force added on top of the away-from-attacker push.")]
    [SerializeField] private float explosionUpwardForce = 3f;

    public static readonly List<KnightController> All = new();

    // Fired with the knight that just finished a move, so something like
    // TurnActivationController can tell a knight (not a King, Pawn or Rook) moved.
    public static event System.Action<KnightController> KnightMoved;

    // The 8 L-shaped offsets a chess knight can jump to: two tiles in a
    // cardinal direction, then one tile to either side.
    private static readonly Vector2Int[] JumpOffsets =
    {
        new(1, 2), new(-1, 2), new(1, -2), new(-1, -2),
        new(2, 1), new(-2, 1), new(2, -1), new(-2, -1)
    };

    private Tile currentTile;
    public Tile CurrentTile => currentTile;

    private Tile previousTile;
    public Tile PreviousTile => previousTile;

    public bool IsMoving => isSliding;

    public bool HasBeenActivated { get; private set; }

    private readonly List<Tile> reachableTiles = new();

    // Same pivot-offset fix as the King, Pawns and Rook: keeps whatever X/Z
    // offset this piece was placed with on its starting tile, and
    // re-applies it on every tile it steps onto so it stays visually
    // centered.
    private Vector3 tileOffset;

    // How far above a tile's resting surface this knight's model sits, re-
    // derived from its current tile's live height every frame (tiles rise
    // while highlighted), same as the King, Pawns and Rook.
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
        // FollowTileHeight, so this knight keeps tracking its tile's live
        // height (elevated or not) throughout the slide, not just at rest.
        // It's a straight glide to the destination rather than a two-leg
        // path - the "jump" is that nothing along the way can block or stop
        // it, not a particular visual arc.
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

        // Locks out every White Pawn, Rook and other Knight for the rest of
        // this turn too (see PieceSelectionManager.SelectPiece), and
        // switches all of their fire off right along with this one's.
        PieceActivationLimit.MarkActivated();

        Deselect();
        KnightMoved?.Invoke(this);
        PlayerController.RaiseMoveCompleted();
    }

    private void SetFireActive(bool active)
    {
        if (fireEffectRoots == null)
        {
            return;
        }

        foreach (Transform fireEffectRoot in fireEffectRoots)
        {
            if (fireEffectRoot != null)
            {
                fireEffectRoot.gameObject.SetActive(active);
            }
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

    // Called by an EnemyPawnController that just captured this knight.
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

    // Every L-shaped landing tile that isn't off the board and isn't
    // occupied by a friendly piece - what's in between never matters, since
    // this piece jumps over it. A tile an enemy pawn occupies is a valid
    // landing (a capture); a friendly piece's tile isn't.
    private void RefreshReachableTiles()
    {
        SetReachableHighlighted(false);
        reachableTiles.Clear();

        if (currentTile == null || !TileGrid.TryGetCoord(currentTile, out Vector2Int coord))
        {
            return;
        }

        foreach (Vector2Int offset in JumpOffsets)
        {
            Tile tile = TileGrid.GetTileAt(coord + offset);

            if (tile != null && !IsOccupiedByFriendly(tile))
            {
                reachableTiles.Add(tile);
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

        foreach (KnightController knight in All)
        {
            if (knight != this && knight.CurrentTile == tile)
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
