using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Opens and closes the Main Menu's Settings Window. Clicking the Settings
/// button pushes it in and back out, then slides the window up from below the
/// screen into place. Clicking the window's Back button does the same and
/// slides it back down out of sight. Both buttons and the window are 3D
/// objects drawn by the UI Camera, so clicks are found by raycasting through
/// that camera.
/// </summary>
public class SettingsWindowController : MonoBehaviour
{
    [Header("Objects")]
    [Tooltip("The Settings button. Its collider (or a child's) is what a click has to hit.")]
    [SerializeField] private Transform settingsButton;

    [Tooltip("The Back button inside the Settings Window.")]
    [SerializeField] private Transform backButton;

    [Tooltip("The Settings Window itself.")]
    [SerializeField] private Transform window;

    [Tooltip("The camera the buttons and window are rendered through (UI Camera).")]
    [SerializeField] private Camera uiCamera;

    [Header("Window")]
    [Tooltip("World position the window slides to when open. It starts just below the bottom of the screen and slides up to here.")]
    [SerializeField] private Vector3 shownPosition = new Vector3(-1.05039f, 4.606827f, -44.92128f);

    [Tooltip("Seconds the window takes to slide in or out.")]
    [SerializeField] private float slideDuration = 0.5f;

    [Header("Button Press")]
    [Tooltip("How far a button pushes in when clicked, in world units.")]
    [SerializeField] private float pressDepth = 0.3f;

    [Tooltip("Units per second the push-in/push-out plays at.")]
    [SerializeField] private float pressSpeed = 3f;

    private enum State
    {
        Hidden,
        Open
    }

    private State state = State.Hidden;
    private bool busy;
    private Vector3 hiddenPosition;

    private static Camera clickCamera;

    // True only while the window is fully slid into place, so its controls
    // (sliders, toggles) don't react to clicks while it's moving or hidden.
    public static bool IsOpen { get; private set; }

    // True when a click at this screen position lands on any UI object, so
    // clicks meant for a button don't also reach the board behind it.
    public static bool IsPointerOverUI(Vector2 screenPosition)
    {
        if (clickCamera == null)
        {
            return false;
        }

        Ray ray = clickCamera.ScreenPointToRay(screenPosition);
        return Physics.Raycast(ray, Mathf.Infinity, clickCamera.cullingMask);
    }

    private void Awake()
    {
        clickCamera = uiCamera;
        IsOpen = false;

        if (window == null)
        {
            return;
        }

        // Measured from where it will sit when shown, then parked out of sight.
        window.position = shownPosition;
        hiddenPosition = shownPosition + Vector3.down * GetDistanceBelowScreen();
        window.position = hiddenPosition;
    }

    private void OnDestroy()
    {
        if (clickCamera == uiCamera)
        {
            clickCamera = null;
        }
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;

        if (busy || uiCamera == null || window == null || mouse == null || !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Ray ray = uiCamera.ScreenPointToRay(mouse.position.ReadValue());

        // The nearest hit decides what was clicked, so a button hidden behind
        // the open window can't be pressed through it.
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, uiCamera.cullingMask))
        {
            return;
        }

        if (state == State.Hidden && settingsButton != null && hit.collider.transform.IsChildOf(settingsButton))
        {
            StartCoroutine(Toggle(settingsButton, shownPosition, State.Open));
        }
        else if (state == State.Open && backButton != null && hit.collider.transform.IsChildOf(backButton))
        {
            StartCoroutine(Toggle(backButton, hiddenPosition, State.Hidden));
        }
    }

    private IEnumerator Toggle(Transform button, Vector3 windowTarget, State newState)
    {
        busy = true;
        IsOpen = false;

        yield return Press(button);
        yield return SlideWindow(windowTarget);

        state = newState;
        IsOpen = newState == State.Open;
        busy = false;
    }

    private IEnumerator Press(Transform button)
    {
        Vector3 start = button.position;
        Vector3 pressed = start + GetPressDirection(button) * pressDepth;

        while (button.position != pressed)
        {
            button.position = Vector3.MoveTowards(button.position, pressed, pressSpeed * Time.deltaTime);
            yield return null;
        }

        while (button.position != start)
        {
            button.position = Vector3.MoveTowards(button.position, start, pressSpeed * Time.deltaTime);
            yield return null;
        }
    }

    private IEnumerator SlideWindow(Vector3 target)
    {
        Vector3 start = window.position;
        float elapsed = 0f;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            window.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / slideDuration)));
            yield return null;
        }

        window.position = target;
    }

    // Pushes along whichever of the button's own axes points most nearly
    // away from the camera, so "back" is right however the button is tilted.
    private Vector3 GetPressDirection(Transform button)
    {
        Vector3[] axes = { button.right, button.up, button.forward };
        Vector3 cameraForward = uiCamera.transform.forward;
        Vector3 best = cameraForward;
        float bestDot = 0f;

        foreach (Vector3 axis in axes)
        {
            float dot = Vector3.Dot(axis, cameraForward);

            if (Mathf.Abs(dot) > bestDot)
            {
                bestDot = Mathf.Abs(dot);
                best = dot >= 0f ? axis : -axis;
            }
        }

        return best.normalized;
    }

    // How far straight down the window must move for its top edge to sit
    // just below the bottom of the screen.
    private float GetDistanceBelowScreen()
    {
        if (uiCamera == null)
        {
            return 12f;
        }

        Bounds bounds = new Bounds(window.position, Vector3.zero);

        foreach (Renderer windowRenderer in window.GetComponentsInChildren<Renderer>())
        {
            bounds.Encapsulate(windowRenderer.bounds);
        }

        // Straight down in the world can differ from down on screen if the
        // camera is tilted, so measure along the world's up axis.
        Vector3 bottomEdge = uiCamera.ViewportToWorldPoint(new Vector3(0.5f, 0f, uiCamera.nearClipPlane));
        float distance = bounds.max.y - bottomEdge.y;

        return Mathf.Max(0f, distance) + 0.5f;
    }
}
