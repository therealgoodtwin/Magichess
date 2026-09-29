using UnityEngine;

/// <summary>
/// One end of a two-way teleport. Put this on an object standing over a tile,
/// and drag the other teleporter into Destination. When the King finishes a
/// move onto this teleporter's tile, it is sent to the destination's tile, and
/// the camera slides over to centre on it. Controls stay locked until the
/// camera arrives. Linking one end is enough: the destination is linked back
/// automatically unless it already has a destination of its own.
/// </summary>
public class ArsenalTeleporter : MonoBehaviour
{
    [Tooltip("The teleporter the King comes out of.")]
    [SerializeField] private ArsenalTeleporter destination;

    [Tooltip("Seconds the camera takes to slide over to the King's new position.")]
    [SerializeField] private float cameraSlideDuration = 0.5f;

    [Tooltip("How far below the centre of the screen the King ends up, in world units. 0 centres him exactly.")]
    [SerializeField] private float kingLowerBy = 1.25f;

    // True from the moment the King is sent until the camera has arrived.
    public static bool IsTeleporting { get; private set; }

    private Tile tile;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsTeleporting = false;
    }

    // The tile this teleporter stands over, found once tiles exist.
    private Tile Tile
    {
        get
        {
            if (tile == null)
            {
                tile = TileGrid.FindNearest(transform.position);
            }

            return tile;
        }
    }

    private void Awake()
    {
        // Sits on top of a tile, so its collider would swallow clicks meant
        // for that tile. Ignore Raycast keeps it out of every click raycast.
        foreach (Transform part in GetComponentsInChildren<Transform>(true))
        {
            part.gameObject.layer = 2;
        }

        if (destination == this)
        {
            Debug.LogWarning("ArsenalTeleporter: destination is itself.", this);
            destination = null;
        }

        if (destination != null && destination.destination == null)
        {
            destination.destination = this;
        }
    }

    private void OnEnable()
    {
        PlayerController.KingMoved += HandleKingMoved;
    }

    private void OnDisable()
    {
        PlayerController.KingMoved -= HandleKingMoved;
    }

    private void HandleKingMoved()
    {
        // Both ends hear the same event. Whichever sends the King first sets
        // IsTeleporting, so the other doesn't bounce him straight back.
        if (IsTeleporting || destination == null)
        {
            return;
        }

        PlayerController king = FindFirstObjectByType<PlayerController>();

        if (king == null || king.CurrentTile == null || king.CurrentTile != Tile)
        {
            return;
        }

        Tile exit = destination.Tile;

        if (exit == null)
        {
            Debug.LogWarning("ArsenalTeleporter: the destination has no tile under it.", this);
            return;
        }

        IsTeleporting = true;
        king.TeleportTo(exit);

        CameraController cameraController = FindFirstObjectByType<CameraController>();

        if (cameraController == null)
        {
            IsTeleporting = false;
            return;
        }

        cameraController.GlideToFocus(exit.BaseWorldPosition, cameraSlideDuration, kingLowerBy, () => IsTeleporting = false);
    }
}
