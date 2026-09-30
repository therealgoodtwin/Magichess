using System.Collections;
using RayFire;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Runs the Main Menu intro. Until the Go button is clicked, camera panning
/// and piece controls stay disabled. Clicking the button pushes it in and
/// back out, slides it off the left edge of the screen, and glides the
/// camera to the target camera's position and facing. Once the glide is done,
/// the disabled controls switch on and the King's reachable tiles light up,
/// so play continues exactly like in the level.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Go Button")]
    [Tooltip("The whole 'Go button' object - slides off screen after being clicked.")]
    [SerializeField] private Transform goButton;

    [Tooltip("The clickable body of the button (the 'Cube'). It's what gets pushed in, and what a click has to hit.")]
    [SerializeField] private Transform buttonFace;

    [Tooltip("The camera the button is rendered through (UI Camera).")]
    [SerializeField] private Camera uiCamera;

    [Tooltip("How far the button pushes in when clicked, in world units.")]
    [SerializeField] private float pressDepth = 0.15f;

    [Tooltip("Units per second the push-in/push-out plays at.")]
    [SerializeField] private float pressSpeed = 3f;

    [Tooltip("Units per second the button slides off screen at.")]
    [SerializeField] private float slideSpeed = 30f;

    [Header("Camera Glide")]
    [Tooltip("The camera that glides. It's the active camera the player sees.")]
    [SerializeField] private Transform cameraToMove;

    [Tooltip("The camera object whose position and facing the camera glides to. It can stay disabled.")]
    [SerializeField] private Transform cameraTarget;

    [Tooltip("Seconds the camera glide takes.")]
    [SerializeField] private float glideDuration = 2.5f;

    [Header("Controls")]
    [Tooltip("Disabled until the glide finishes - add the camera's Camera Controller and the Piece Selection Manager. Scripts only: dragging a whole GameObject in picks its first script.")]
    [SerializeField] private MonoBehaviour[] enableAfterIntro;

    private enum State
    {
        WaitingForClick,
        Pressing,
        Transitioning,
        Done
    }

    private State state = State.WaitingForClick;

    private Vector3 pressStartPosition;
    private Vector3 pressDirection;
    private bool isPressingIn;

    private bool buttonSlideDone;
    private bool cameraGlideDone;

    private PlayerController king;

    // The scene's deco pawns have their gameplay scripts switched off. Turn
    // back on just enough for them to keep standing on their tile as it lifts
    // and lowers, and for enemy pawns to be destroyed when the King moves
    // onto them. With TurnsEnabled off, enemies never plan or make a move,
    // and White Pawns can't be selected.
    private void Awake()
    {
        TurnManager.DisableTurns(this);

        // Switched off here rather than in Start so they never get a single
        // frame of running before the intro is over.
        foreach (MonoBehaviour behaviour in enableAfterIntro)
        {
            if (behaviour != null)
            {
                behaviour.enabled = false;
            }
        }

        foreach (WhitePawnController pawn in FindObjectsByType<WhitePawnController>(FindObjectsSortMode.None))
        {
            pawn.enabled = true;
        }

        foreach (EnemyPawnController enemy in FindObjectsByType<EnemyPawnController>(FindObjectsSortMode.None))
        {
            enemy.enabled = true;

            if (enemy.TryGetComponent(out Health health))
            {
                health.enabled = true;
            }

            if (enemy.TryGetComponent(out RayfireRigid rigid))
            {
                rigid.enabled = true;
            }
        }
    }

    private void OnDestroy()
    {
        TurnManager.RestoreTurns(this);
    }

    private void Start()
    {
        // The King skips lighting its tiles while turns are off (see
        // PlayerController.Start), so nothing is lit until the intro ends.
        king = FindFirstObjectByType<PlayerController>();
    }

    private void Update()
    {
        switch (state)
        {
            case State.WaitingForClick:
                CheckForClick();
                break;
            case State.Pressing:
                AnimatePress();
                break;
        }
    }

    private void CheckForClick()
    {
        Mouse mouse = Mouse.current;

        if (uiCamera == null || buttonFace == null || mouse == null || !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Ray ray = uiCamera.ScreenPointToRay(mouse.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, uiCamera.cullingMask) ||
            !hit.collider.transform.IsChildOf(buttonFace))
        {
            return;
        }

        // The button lives in the same 3D world as the board, and the piece
        // click raycast doesn't filter by layer - once it's been used, its
        // collider must never be able to swallow a click meant for a tile.
        foreach (Collider buttonCollider in goButton.GetComponentsInChildren<Collider>())
        {
            buttonCollider.enabled = false;
        }

        pressStartPosition = buttonFace.position;
        pressDirection = GetPressDirection();
        isPressingIn = true;
        state = State.Pressing;

        StartCoroutine(GlideCamera());
    }

    // Pushes along whichever of the button's own axes points most nearly
    // away from the camera, so "back" is right however the button is tilted.
    private Vector3 GetPressDirection()
    {
        Vector3[] axes = { buttonFace.right, buttonFace.up, buttonFace.forward };
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

    private void AnimatePress()
    {
        Vector3 target = isPressingIn ? pressStartPosition + pressDirection * pressDepth : pressStartPosition;
        buttonFace.position = Vector3.MoveTowards(buttonFace.position, target, pressSpeed * Time.deltaTime);

        if (buttonFace.position != target)
        {
            return;
        }

        if (isPressingIn)
        {
            isPressingIn = false;
            return;
        }

        state = State.Transitioning;
        StartCoroutine(SlideButtonOffScreen());
    }

    private IEnumerator SlideButtonOffScreen()
    {
        Vector3 start = goButton.position;
        Vector3 target = start - uiCamera.transform.right * GetDistanceOffLeftEdge();

        while (goButton.position != target)
        {
            goButton.position = Vector3.MoveTowards(goButton.position, target, slideSpeed * Time.deltaTime);
            yield return null;
        }

        buttonSlideDone = true;
        FinishIfReady();
    }

    // How far left the button must move for its right-most point to sit just
    // past the left edge of the screen.
    private float GetDistanceOffLeftEdge()
    {
        Bounds bounds = new Bounds(goButton.position, Vector3.zero);

        foreach (Renderer buttonRenderer in goButton.GetComponentsInChildren<Renderer>())
        {
            bounds.Encapsulate(buttonRenderer.bounds);
        }

        Vector3 right = uiCamera.transform.right;
        Vector3 cameraPosition = uiCamera.transform.position;

        Vector3 leftEdge = uiCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, uiCamera.nearClipPlane));
        float leftEdgeAlongRight = Vector3.Dot(leftEdge - cameraPosition, right);

        float extentAlongRight = Mathf.Abs(right.x) * bounds.extents.x
            + Mathf.Abs(right.y) * bounds.extents.y
            + Mathf.Abs(right.z) * bounds.extents.z;
        float buttonMaxAlongRight = Vector3.Dot(bounds.center - cameraPosition, right) + extentAlongRight;

        return Mathf.Max(0f, buttonMaxAlongRight - leftEdgeAlongRight) + 0.5f;
    }

    private IEnumerator GlideCamera()
    {
        if (cameraToMove != null && cameraTarget != null)
        {
            Vector3 startPosition = cameraToMove.position;
            Quaternion startRotation = cameraToMove.rotation;
            float elapsed = 0f;

            while (elapsed < glideDuration)
            {
                elapsed += Time.deltaTime;
                float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / glideDuration));

                cameraToMove.position = Vector3.Lerp(startPosition, cameraTarget.position, eased);
                cameraToMove.rotation = Quaternion.Slerp(startRotation, cameraTarget.rotation, eased);
                yield return null;
            }

            cameraToMove.SetPositionAndRotation(cameraTarget.position, cameraTarget.rotation);
        }

        cameraGlideDone = true;
        FinishIfReady();
    }

    private void FinishIfReady()
    {
        if (!buttonSlideDone || !cameraGlideDone || state == State.Done)
        {
            return;
        }

        state = State.Done;

        foreach (MonoBehaviour behaviour in enableAfterIntro)
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        if (king == null)
        {
            Debug.LogWarning("MainMenuController: no King (PlayerController) found in the scene, so nothing can be selected.", this);
            return;
        }

        king.Select();

        Debug.Log($"MainMenuController: King is on tile '{(king.CurrentTile != null ? king.CurrentTile.name : "NONE")}' with {TileGrid.GetNeighbors(king.CurrentTile).Count} neighbor tile(s), out of {Tile.All.Count} tiles in the scene.", this);
    }
}
