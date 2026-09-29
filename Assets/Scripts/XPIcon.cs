using UnityEngine;

/// <summary>
/// Marks the "XP" icon object in the UI as the flight target for XPPickup,
/// and exposes it (and the camera it's rendered through) statically - a
/// dynamically Instantiate()'d prefab like XPPickup can't hold a serialized
/// reference to a live scene object, so it reads these instead.
/// </summary>
public class XPIcon : MonoBehaviour
{
    [Tooltip("Camera the XP icon (and the rest of the UI) is rendered through - e.g. UI Camera.")]
    [SerializeField] private Camera uiCamera;

    public static Camera UICamera { get; private set; }
    public static Transform Target { get; private set; }

    private void OnEnable()
    {
        UICamera = uiCamera;
        Target = transform;
    }

    private void OnDisable()
    {
        if (Target == transform)
        {
            UICamera = null;
            Target = null;
        }
    }
}
