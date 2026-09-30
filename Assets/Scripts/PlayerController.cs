using System.Collections.Generic;
using RayFire;
using UnityEngine;

/// <summary>
/// The King, chess-king style: every tile adjacent to it (including
/// diagonally) highlights as clickable while it's the selected piece.
/// Clicking one slides it there and the newly-adjacent tiles take over as
/// the clickable set. PieceSelectionManager owns input and decides when the
/// King is the active selection versus a White Pawn.
/// </summary>
public class PlayerController : MonoBehaviour, ISelectablePiece
{
    [Tooltip("World units per second the King slides at when stepping onto a tile.")]
    [SerializeField] private float moveSpeed = 8f;

    [Tooltip("HP dealt to an enemy pawn's Health when the King moves onto its tile, instead of an unconditional kill.")]
    [SerializeField] private int captureDamage = 3;

    public int CaptureDamage => captureDamage;

    [Header("Death")]
    [Tooltip("Force applied to fragments, pointing away from whatever dealt the fatal hit, when the King dies.")]
    [SerializeField] private float explosionForce = 8f;

    [Tooltip("Extra upward force added on top of the away-from-attacker push.")]
    [SerializeField] private float explosionUpwardForce = 3f;

    public static event System.Action OnMoveCompleted;

    // Fired specifically when the King (as opposed to any piece in
    // general) finishes a move, so something like a turn/activation
    // tracker can tell the King was the one that moved.
    public static event System.Action KingMoved;

    public static void RaiseMoveCompleted()
    {
        OnMoveCompleted?.Invoke();
    }

    // A scripted intro (see TutorialController) that wants the King to stay
    // deselected past its own Start() - despite TurnsEnabled being on -
    // sets this to false from another script's Awake(), which always runs
    // before every Start(). Whatever sets it false is responsible for
    // resetting it back to true again (e.g. from its own OnDestroy), so it
    // doesn't leak into a later scene.
    public static bool HighlightOnStart { get; set; } = true;

    private Tile currentTile;
    public Tile CurrentTile => currentTile;

    private Tile previousTile;
    public Tile PreviousTile => previousTile;

    public bool IsMoving => isSliding;

    public bool HasBeenActivated { get; private set; }

    private readonly List<Tile> reachableTiles = new();

    // The King's model pivot isn't at the mesh's visual center, so snapping
    // straight to a tile's transform.position renders off-center. Instead we
    // keep whatever X/Z offset the piece was placed with on its starting
    // tile, and re-apply that same offset on every tile it steps onto.
    private Vector3 tileOffset;

    private bool isSliding;
    private Vector3 slideTarget;

    private Health health;
    private PieceMoveSound moveSound;

    private void OnEnable()
    {
        TurnManager.EnemyTurnEnded += HandleTurnEnded;

        health = GetComponent<Health>();

        if (health != null)
        {
            health.OnDeath += HandleDeath;
        }
    }

    private void OnDisable()
    {
        TurnManager.EnemyTurnEnded -= HandleTurnEnded;

        if (health != null)
        {
            health.OnDeath -= HandleDeath;
        }
    }

    private void HandleTurnEnded()
    {
        HasBeenActivated = false;
    }

    private void Start()
    {
        currentTile = TileGrid.FindNearest(transform.position);

        if (currentTile != null)
        {
            tileOffset = transform.position - currentTile.transform.position;
            tileOffset.y = 0f;
        }

        moveSound = GetComponent<PieceMoveSound>();

        // Scenes without turns (the Main Menu) light up the King's tiles
        // themselves once the player can actually move - lighting them here
        // would flash them on for a frame before being cleared again. Same
        // idea for a scripted intro that wants the King to stay deselected
        // for longer despite this being a normal turn-based scene.
        if (TurnManager.TurnsEnabled && HighlightOnStart)
        {
            RefreshReachableTiles();
        }
    }

    private void HandleDeath()
    {
        enabled = false;

        if (!TryGetComponent(out RayfireRigid rigid))
        {
            if (TryGetComponent(out MeshRenderer meshRenderer))
            {
                meshRenderer.enabled = false;
            }

            return;
        }

        Vector3 direction = HorizontalDirection(health.LastDamageSourcePosition, transform.position);

        rigid.Demolish();

        if (!rigid.HasFragments)
        {
            return;
        }

        Vector3 force = direction * explosionForce + Vector3.up * explosionUpwardForce;

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

    private void Update()
    {
        if (isSliding)
        {
            Slide();
        }
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

    // Puts the King straight onto another tile (an Arsenal Teleporter's exit),
    // keeping its offset on the tile and its height relative to the tiles.
    // If its reachable tiles were lit, they move with it.
    public void TeleportTo(Tile target)
    {
        bool wasSelected = reachableTiles.Count > 0;

        SetReachableHighlighted(false);
        reachableTiles.Clear();

        Vector3 position = transform.position;
        position.x = target.transform.position.x + tileOffset.x;
        position.z = target.transform.position.z + tileOffset.z;

        if (currentTile != null)
        {
            position.y += target.BaseWorldPosition.y - currentTile.BaseWorldPosition.y;
        }

        previousTile = currentTile;
        currentTile = target;
        transform.position = position;

        if (wasSelected)
        {
            RefreshReachableTiles();
        }
    }

    private void Slide()
    {
        transform.position = Vector3.MoveTowards(transform.position, slideTarget, moveSpeed * Time.deltaTime);

        if (transform.position == slideTarget)
        {
            isSliding = false;

            if (TurnManager.TurnsEnabled)
            {
                HasBeenActivated = true;
                Deselect();
            }
            else
            {
                // No turns here, so nothing ever resets activation - keep
                // the King selected and ready to move again.
                Select();
            }

            KingMoved?.Invoke();
            OnMoveCompleted?.Invoke();
        }
    }

    private void RefreshReachableTiles()
    {
        SetReachableHighlighted(false);
        reachableTiles.Clear();

        if (currentTile == null)
        {
            return;
        }

        foreach (Tile neighbor in TileGrid.GetNeighbors(currentTile))
        {
            // Sunk tiles (left behind in the Overworld) can't be walked onto.
            if (!neighbor.IsSunk)
            {
                reachableTiles.Add(neighbor);
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
