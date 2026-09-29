using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the whole End Turn button cycle. Tracks whether the King and/or at
/// least one White Pawn has been "activated" this turn - clicked on and
/// actually moved, not just selected. As soon as either has, slides the End
/// Turn object from its hidden position to its revealed one. Clicking it
/// while revealed plays a quick press-in/press-out animation, slides it back
/// to hidden, and ends the player's turn - which is when every enemy pawn
/// actually carries out its already-planned move, all at once (see
/// TurnManager.EndPlayerTurn). Once every enemy has finished moving,
/// activation resets so the player can activate the King and/or a pawn
/// again next turn.
/// </summary>
public class TurnActivationController : MonoBehaviour
{
    [Header("End Turn Object")]
    [Tooltip("The 'End Turn' object, hidden at its starting position until something's been activated.")]
    [SerializeField] private Transform endTurnObject;

    [Tooltip("Local position End Turn slides to once revealed - position it there once in Play mode and copy the values here.")]
    [SerializeField] private Vector3 revealedLocalPosition;

    [Tooltip("Units per second End Turn slides between hidden and revealed.")]
    [SerializeField] private float slideSpeed = 8f;

    [Header("Button Click")]
    [Tooltip("The camera that renders the UI3D layer End Turn lives on - not the main game camera.")]
    [SerializeField] private Camera uiCamera;

    [Tooltip("How far End Turn's face pushes in, along its own forward direction, when clicked.")]
    [SerializeField] private float pressDepth = 0.08f;

    [Tooltip("Units per second the press-in/press-out animation plays at.")]
    [SerializeField] private float pressSpeed = 20f;

    public bool KingActivated { get; private set; }
    public bool AnyPawnActivated { get; private set; }

    private enum EndTurnState
    {
        Hidden,
        Sliding,
        Revealed,
        Pressing
    }

    private EndTurnState state = EndTurnState.Hidden;

    private Vector3 hiddenLocalPosition;
    private Vector3 slideDestination;
    private Vector3 pressStartLocalPosition;
    private bool isPressingIn;

    private void OnEnable()
    {
        PlayerController.KingMoved += HandleKingMoved;
        WhitePawnController.PawnMoved += HandlePawnMoved;
        RookController.RookMoved += HandleRookMoved;
        KnightController.KnightMoved += HandleKnightMoved;
        BishopController.BishopMoved += HandleBishopMoved;
        PawnSummonController.PawnSummoned += HandlePawnSummoned;
        TurnManager.EnemyTurnEnded += HandleEnemyTurnEnded;
    }

    private void OnDisable()
    {
        PlayerController.KingMoved -= HandleKingMoved;
        WhitePawnController.PawnMoved -= HandlePawnMoved;
        RookController.RookMoved -= HandleRookMoved;
        KnightController.KnightMoved -= HandleKnightMoved;
        BishopController.BishopMoved -= HandleBishopMoved;
        PawnSummonController.PawnSummoned -= HandlePawnSummoned;
        TurnManager.EnemyTurnEnded -= HandleEnemyTurnEnded;
    }

    private void Start()
    {
        if (endTurnObject != null)
        {
            hiddenLocalPosition = endTurnObject.localPosition;
        }
    }

    private void Update()
    {
        if (endTurnObject == null)
        {
            return;
        }

        switch (state)
        {
            case EndTurnState.Revealed:
                CheckForClick();
                break;
            case EndTurnState.Pressing:
                AnimatePress();
                break;
            case EndTurnState.Sliding:
                AnimateSlide();
                break;
        }
    }

    private void CheckForClick()
    {
        if (TurnManager.IsEnemyTurn)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        bool spacePressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;

        if (!spacePressed && !WasEndTurnClicked())
        {
            return;
        }

        pressStartLocalPosition = endTurnObject.localPosition;
        isPressingIn = true;
        state = EndTurnState.Pressing;
    }

    // Same effect as clicking End Turn directly - Space is the shortcut for
    // it, same as the dialogue in the Tutorial scene already tells the
    // player ("press Space to let the other guys play as well").
    private bool WasEndTurnClicked()
    {
        if (uiCamera == null)
        {
            return false;
        }

        Mouse mouse = Mouse.current;

        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
        {
            return false;
        }

        Ray ray = uiCamera.ScreenPointToRay(mouse.position.ReadValue());
        return Physics.Raycast(ray, out RaycastHit hit) && hit.collider.transform.IsChildOf(endTurnObject);
    }

    private void AnimatePress()
    {
        Vector3 pressedPosition = pressStartLocalPosition - endTurnObject.forward * pressDepth;
        Vector3 target = isPressingIn ? pressedPosition : pressStartLocalPosition;

        endTurnObject.localPosition = Vector3.MoveTowards(endTurnObject.localPosition, target, pressSpeed * Time.deltaTime);

        if (endTurnObject.localPosition != target)
        {
            return;
        }

        if (isPressingIn)
        {
            isPressingIn = false;
            return;
        }

        // Press-out finished - slide back to hidden and hand the turn over
        // to the enemies.
        BeginSlide(hiddenLocalPosition);
        TurnManager.EndPlayerTurn();
    }

    private void BeginSlide(Vector3 destination)
    {
        slideDestination = destination;
        state = EndTurnState.Sliding;
    }

    private void AnimateSlide()
    {
        endTurnObject.localPosition = Vector3.MoveTowards(endTurnObject.localPosition, slideDestination, slideSpeed * Time.deltaTime);

        if (endTurnObject.localPosition == slideDestination)
        {
            state = slideDestination == revealedLocalPosition ? EndTurnState.Revealed : EndTurnState.Hidden;
        }
    }

    private void HandleKingMoved()
    {
        KingActivated = true;
        TryReveal();
    }

    private void HandlePawnMoved(WhitePawnController pawn)
    {
        AnyPawnActivated = true;
        TryReveal();
    }

    // Shares the same AnyPawnActivated flag as a Pawn moving - it means "the
    // one extra piece slot has been used", not specifically "a Pawn".
    private void HandleRookMoved(RookController rook)
    {
        AnyPawnActivated = true;
        TryReveal();
    }

    // Same shared-flag reasoning as HandleRookMoved.
    private void HandleKnightMoved(KnightController knight)
    {
        AnyPawnActivated = true;
        TryReveal();
    }

    // Same shared-flag reasoning as HandleRookMoved.
    private void HandleBishopMoved(BishopController bishop)
    {
        AnyPawnActivated = true;
        TryReveal();
    }

    // Summoning a pawn from the Shop is a turn-defining action too, same as
    // moving one - but it doesn't slide onto the board like a normal move,
    // so it never fires PawnMoved. Without this, summoning right after an
    // End Turn click (which hides the button again) would leave no way to
    // click End Turn again - it simply never reappears.
    private void HandlePawnSummoned()
    {
        AnyPawnActivated = true;
        TryReveal();
    }

    private void TryReveal()
    {
        if (state != EndTurnState.Hidden)
        {
            return;
        }

        BeginSlide(revealedLocalPosition);
    }

    private void HandleEnemyTurnEnded()
    {
        KingActivated = false;
        AnyPawnActivated = false;
    }
}
