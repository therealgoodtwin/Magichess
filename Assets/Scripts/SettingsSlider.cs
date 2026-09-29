using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A draggable 3D slider for the Settings Window. The handle slides along the
/// track's long axis and can't go past the track's ends. Value runs from 0
/// (left end) to 1 (right end); ValueChanged fires while it's dragged. Only
/// reacts while the Settings Window is fully open.
/// </summary>
public class SettingsSlider : MonoBehaviour
{
    [Tooltip("The bar the handle slides along (e.g. 'SFX Slider'). Its ends are the handle's limits.")]
    [SerializeField] private Transform track;

    [Tooltip("The part that gets dragged (e.g. 'SFX Slide Button').")]
    [SerializeField] private Transform handle;

    [Tooltip("The camera the settings window is rendered through (UI Camera).")]
    [SerializeField] private Camera uiCamera;

    public event System.Action<float> ValueChanged;

    public float Value { get; private set; }

    private MeshFilter trackMesh;
    private MeshFilter handleMesh;

    private bool dragging;
    private float grabOffset;

    private void Start()
    {
        if (track != null)
        {
            trackMesh = track.GetComponent<MeshFilter>();
        }

        if (handle != null)
        {
            handleMesh = handle.GetComponent<MeshFilter>();
        }

        if (trackMesh == null || handleMesh == null)
        {
            Debug.LogWarning("SettingsSlider: the track and handle each need a mesh to measure.", this);
            enabled = false;
            return;
        }

        Measure(out _, out float low, out float high, out float center);
        Value = Mathf.Approximately(low, high) ? 0f : Mathf.Clamp01((center - low) / (high - low));
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null || uiCamera == null)
        {
            return;
        }

        if (dragging)
        {
            if (!mouse.leftButton.isPressed || !SettingsWindowController.IsOpen)
            {
                dragging = false;
                return;
            }

            DragTo(mouse.position.ReadValue());
            return;
        }

        if (!mouse.leftButton.wasPressedThisFrame || !SettingsWindowController.IsOpen)
        {
            return;
        }

        Vector2 screenPosition = mouse.position.ReadValue();
        Ray ray = uiCamera.ScreenPointToRay(screenPosition);

        // The nearest hit decides, so nothing is grabbed through another object.
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, uiCamera.cullingMask) ||
            !hit.collider.transform.IsChildOf(track))
        {
            return;
        }

        Measure(out Vector3 axis, out _, out _, out float center);
        float mouseAlong = MouseAlong(screenPosition, axis);

        // Grabbing the handle keeps it under the cursor where it was grabbed;
        // clicking the bar itself makes the handle jump there.
        grabOffset = hit.collider.transform.IsChildOf(handle) ? mouseAlong - center : 0f;
        dragging = true;
        DragTo(screenPosition);
    }

    private void DragTo(Vector2 screenPosition)
    {
        Measure(out Vector3 axis, out float low, out float high, out float center);

        float target = Mathf.Clamp(MouseAlong(screenPosition, axis) - grabOffset, low, high);
        handle.position += axis * (target - center);

        float newValue = Mathf.Approximately(low, high) ? 0f : Mathf.Clamp01((target - low) / (high - low));

        if (!Mathf.Approximately(newValue, Value))
        {
            Value = newValue;
            ValueChanged?.Invoke(Value);
        }
    }

    // Where the cursor is along the slide axis, taken on the plane the
    // handle sits in, facing the camera.
    private float MouseAlong(Vector2 screenPosition, Vector3 axis)
    {
        Vector3 handleCenter = handle.TransformPoint(handleMesh.sharedMesh.bounds.center);
        Plane plane = new Plane(-uiCamera.transform.forward, handleCenter);
        Ray ray = uiCamera.ScreenPointToRay(screenPosition);

        return plane.Raycast(ray, out float distance)
            ? Vector3.Dot(ray.GetPoint(distance), axis)
            : Vector3.Dot(handleCenter, axis);
    }

    // The slide axis (the track's longest side, pointing screen-right), the
    // range the handle's centre may travel along it - the track's ends, less
    // half the handle's own width - and where the handle's centre is now.
    // Worked out on demand, so it holds up wherever the window currently is.
    private void Measure(out Vector3 axis, out float low, out float high, out float center)
    {
        Bounds trackBounds = trackMesh.sharedMesh.bounds;
        Vector3 trackScale = track.lossyScale;
        Vector3[] trackAxes = { track.right, track.up, track.forward };

        float[] trackSizes =
        {
            trackBounds.size.x * Mathf.Abs(trackScale.x),
            trackBounds.size.y * Mathf.Abs(trackScale.y),
            trackBounds.size.z * Mathf.Abs(trackScale.z)
        };

        int longest = 0;

        for (int i = 1; i < 3; i++)
        {
            if (trackSizes[i] > trackSizes[longest])
            {
                longest = i;
            }
        }

        axis = trackAxes[longest].normalized;

        if (Vector3.Dot(axis, uiCamera.transform.right) < 0f)
        {
            axis = -axis;
        }

        float trackCenter = Vector3.Dot(track.TransformPoint(trackBounds.center), axis);
        float trackHalf = trackSizes[longest] * 0.5f;

        Bounds handleBounds = handleMesh.sharedMesh.bounds;
        Vector3 handleScale = handle.lossyScale;
        Vector3[] handleAxes = { handle.right, handle.up, handle.forward };

        float[] handleHalves =
        {
            handleBounds.extents.x * Mathf.Abs(handleScale.x),
            handleBounds.extents.y * Mathf.Abs(handleScale.y),
            handleBounds.extents.z * Mathf.Abs(handleScale.z)
        };

        float handleHalf = 0f;

        for (int i = 0; i < 3; i++)
        {
            handleHalf += Mathf.Abs(Vector3.Dot(axis, handleAxes[i].normalized)) * handleHalves[i];
        }

        low = trackCenter - trackHalf + handleHalf;
        high = trackCenter + trackHalf - handleHalf;
        center = Vector3.Dot(handle.TransformPoint(handleBounds.center), axis);
    }
}
