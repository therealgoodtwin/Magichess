using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Slides the Shop panel between its authored (hidden) position and a
/// revealed position, toggled by the Tab key. Also drivable in code (e.g.
/// by PawnSummonController while placing a summoned pawn), which can lock
/// out the Tab key toggle for as long as it needs to.
/// </summary>
public class ShopToggleController : MonoBehaviour
{
    [Tooltip("Local position the Shop slides to when revealed.")]
    [SerializeField] private Vector3 revealedLocalPosition;

    [Tooltip("Units per second the Shop slides at.")]
    [SerializeField] private float slideSpeed = 20f;

    // Fired the instant the Shop becomes revealed - a Tab press or a
    // scripted Show() call, whichever actually causes the transition (calling
    // Show() while already revealed, or Tab while locked, doesn't fire it).
    public event System.Action Opened;

    // Same idea, the instant it becomes hidden again - a Tab press or a
    // scripted Hide() call.
    public event System.Action Closed;

    private Vector3 hiddenLocalPosition;
    private bool isRevealed;
    private bool locked;

    private void Start()
    {
        hiddenLocalPosition = transform.localPosition;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (!locked && keyboard != null && keyboard.tabKey.wasPressedThisFrame)
        {
            SetRevealed(!isRevealed);
        }

        Vector3 target = isRevealed ? revealedLocalPosition : hiddenLocalPosition;
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, slideSpeed * Time.deltaTime);
    }

    public void Show()
    {
        SetRevealed(true);
    }

    public void Hide()
    {
        SetRevealed(false);
    }

    private void SetRevealed(bool value)
    {
        if (isRevealed == value)
        {
            return;
        }

        isRevealed = value;

        if (isRevealed)
        {
            Opened?.Invoke();
        }
        else
        {
            Closed?.Invoke();
        }
    }

    // While locked, the Tab key no longer toggles the Shop - used so it
    // can't pop back open mid-placement.
    public void SetLocked(bool value)
    {
        locked = value;
    }
}
