using System.Collections;
using UnityEngine;

/// <summary>
/// Drops onto a tile (see EnemyPawnController.HandleDeath) when an enemy
/// pawn dies. The instant the King or a White Pawn steps onto that tile,
/// this flies to the XP icon in the UI and awards 1 XP right as it arrives,
/// then destroys itself.
///
/// The UI is rendered by its own fixed, orthographic UI Camera that only
/// ever sees objects on uiLayer - a completely separate space from the
/// board, not just a different screen position - so the flight can't be a
/// simple world-position lerp from the tile to the icon. Instead, the start
/// point is computed by reading this object's current on-screen position
/// under the main camera and re-projecting that same screen point into the
/// UI camera's world space, so the flight visually begins exactly where the
/// pickup happened and then moves entirely within UI space to the icon.
///
/// The UI camera and target icon come from XPIcon rather than being wired
/// here directly - this component lives on a prefab that gets Instantiate()'d
/// fresh every time an enemy dies, and a prefab asset can't hold a reference
/// to a live scene object.
/// </summary>
public class XPPickup : MonoBehaviour
{
    [Tooltip("Layer this object switches to while flying, so only the UI camera renders it (matches the UI Camera's culling mask).")]
    [SerializeField] private int uiLayer = 6;

    [Tooltip("Seconds the fly-to-UI animation takes.")]
    [SerializeField] private float flightDuration = 0.6f;

    [Tooltip("Extra height added mid-flight for an arc, in world units.")]
    [SerializeField] private float arcHeight = 1.5f;

    private Tile currentTile;
    private Collider pickupCollider;
    private bool collected;

    private void OnEnable()
    {
        PlayerController.OnMoveCompleted += HandlePieceMoved;
    }

    private void OnDisable()
    {
        PlayerController.OnMoveCompleted -= HandlePieceMoved;
    }

    private void Start()
    {
        currentTile = TileGrid.FindNearest(transform.position);
        pickupCollider = GetComponent<Collider>();

        // Covers the capture case: when a friendly piece kills this pawn by
        // moving directly onto its tile, this pickup is spawned mid-dispatch
        // of that same OnMoveCompleted event, so it never receives that
        // particular invocation (it wasn't subscribed yet when the event's
        // invocation list was captured) and would otherwise just sit here
        // uncollected until some future move happened to fire the event again.
        HandlePieceMoved();
    }

    private void HandlePieceMoved()
    {
        if (collected || currentTile == null || !IsFriendlyPieceOnTile(currentTile))
        {
            return;
        }

        collected = true;
        StartCoroutine(FlyToUI());
    }

    private static bool IsFriendlyPieceOnTile(Tile tile)
    {
        PlayerController king = FindFirstObjectByType<PlayerController>();

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

        return false;
    }

    private IEnumerator FlyToUI()
    {
        if (pickupCollider != null)
        {
            pickupCollider.enabled = false;
        }

        Camera uiCamera = XPIcon.UICamera;
        Transform xpIconTarget = XPIcon.Target;

        if (uiCamera == null || xpIconTarget == null || Camera.main == null)
        {
            Destroy(gameObject);
            yield break;
        }

        Vector3 screenStart = Camera.main.WorldToScreenPoint(transform.position);

        // Both the start and end points are projected at the same distance
        // in front of the UI camera - its own icon's depth - so the whole
        // flight stays on the UI diorama's plane instead of drifting toward
        // or away from that camera.
        float depth = Vector3.Dot(xpIconTarget.position - uiCamera.transform.position, uiCamera.transform.forward);
        Vector3 startPos = uiCamera.ScreenToWorldPoint(new Vector3(screenStart.x, screenStart.y, depth));
        Vector3 endPos = xpIconTarget.position;

        SetLayerRecursively(gameObject, uiLayer);
        transform.position = startPos;

        float t = 0f;

        while (t < flightDuration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / flightDuration);
            Vector3 position = Vector3.Lerp(startPos, endPos, progress);
            position += uiCamera.transform.up * (arcHeight * Mathf.Sin(progress * Mathf.PI));
            transform.position = position;
            yield return null;
        }

        XPManager.AddXP(1);
        Destroy(gameObject);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;

        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
