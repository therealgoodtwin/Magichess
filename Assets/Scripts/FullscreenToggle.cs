using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The Settings Window's Fullscreen checkbox. Clicking the button switches the
/// game between fullscreen and windowed, and the indicator shows only while
/// fullscreen is on. Only reacts while the Settings Window is fully open.
/// Note the editor's Game view ignores fullscreen changes - it takes a build
/// to see the window actually change.
/// </summary>
public class FullscreenToggle : MonoBehaviour
{
    [Tooltip("The clickable checkbox (e.g. 'FSButton'). Its collider, or a child's, is what a click has to hit.")]
    [SerializeField] private Transform button;

    [Tooltip("The mark that shows while fullscreen is on (e.g. 'Fullscreen Indicator'). Usually a child of the button.")]
    [SerializeField] private GameObject indicator;

    [Tooltip("The camera the settings window is rendered through (UI Camera).")]
    [SerializeField] private Camera uiCamera;

    private bool isFullscreen;

    private void Start()
    {
        isFullscreen = Screen.fullScreen;
        ShowIndicator();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null || uiCamera == null || button == null ||
            !mouse.leftButton.wasPressedThisFrame || !SettingsWindowController.IsOpen)
        {
            return;
        }

        Ray ray = uiCamera.ScreenPointToRay(mouse.position.ReadValue());

        // The nearest hit decides, so the button can't be clicked through
        // something in front of it.
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, uiCamera.cullingMask) ||
            !hit.collider.transform.IsChildOf(button))
        {
            return;
        }

        isFullscreen = !isFullscreen;
        Screen.fullScreenMode = isFullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        ShowIndicator();
    }

    private void ShowIndicator()
    {
        if (indicator != null)
        {
            indicator.SetActive(isFullscreen);
        }
    }
}
