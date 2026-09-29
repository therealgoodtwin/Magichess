using System.Collections;
using UnityEngine;

/// <summary>
/// Runs the Tutorial scene's scripted opening. Only ever placed in the
/// Tutorial scene - nothing here applies anywhere else. Built up one step at
/// a time; currently:
///
/// - At the start: Space is locked out from opening the Shop, the King
///   starts deselected and can't be clicked/selected, and camera control is
///   locked. 1.5 seconds later, the first dialogue line begins.
/// - The instant Element 2 starts typing, Health UI glides into place.
/// - The instant Element 3 starts typing, camera control unlocks.
/// - The instant Element 5 starts typing, the King unlocks (selectable and
///   movable again).
/// - The instant Element 6 starts typing, "Turn Stuff" (Round Cube and End
///   Turn's shared parent) grows in from nothing to its normal scale -
///   mimics a fade-in without needing material transparency.
/// - Every single line, the pop-up text box itself glides to one of 4 other
///   (hidden) Pop Up Text Box objects' positions, picked at random.
/// - The instant the player picks up their first XP, XP UI glides into
///   place the same way Health UI does.
/// - The instant the player's total XP reaches xpToUnlockShop, the Shop's
///   Tab control unlocks and the Shop hint text box reveals itself. The
///   instant the player actually opens the Shop after that, the hint text
///   box is destroyed.
/// - The first time the player closes the Shop, "Good Job Text" plays -
///   destroyed the instant its sequence finishes.
/// - WaveManager never spawns anything in this scene - it's disabled at
///   Awake() and nothing here ever turns it back on. Once every one of the
///   level's pre-placed enemies is dead, "Tadaaa" plays and controls
///   freeze; once the player finishes it, the screen wipe plays and the
///   Main Menu scene loads.
/// </summary>
public class TutorialController : MonoBehaviour
{
    [Header("Locked At Start")]
    [SerializeField] private PieceSelectionManager pieceSelectionManager;
    [SerializeField] private ShopToggleController shopToggle;
    [SerializeField] private CameraController cameraController;
    [SerializeField] private WaveManager waveManager;

    [Header("Dialogue")]
    [Tooltip("The Pop Up Text Box's DialogueSequence component - this script disables it at Awake() and starts it itself after delayBeforeDialogue, regardless of its own Play On Enable setting.")]
    [SerializeField] private DialogueSequence dialogue;

    [Tooltip("Seconds after the scene starts before the first dialogue line begins.")]
    [SerializeField] private float delayBeforeDialogue = 1.5f;

    [Header("Element 2 - Health UI")]
    [SerializeField] private Transform healthUI;
    [SerializeField] private Vector3 healthUITargetLocalPosition;

    [Header("Element 5 - King")]
    [SerializeField] private PlayerController king;

    [Header("First XP Pickup")]
    [SerializeField] private Transform xpUI;
    [SerializeField] private Vector3 xpUITargetLocalPosition;

    [Header("3rd XP Pickup - Shop Unlock")]
    [Tooltip("Total XP that unlocks the Shop's Tab control and reveals the Shop hint text.")]
    [SerializeField] private int xpToUnlockShop = 3;

    [Tooltip("Prompts the player to open the Shop - destroyed once they actually do.")]
    [SerializeField] private GameObject shopTextBox;

    [Header("First Shop Close - Good Job Text")]
    [Tooltip("Revealed and played the first time the player closes the Shop - destroyed once its sequence finishes.")]
    [SerializeField] private DialogueSequence shopClosedDialogue;

    [Header("Element 6 - Turn Stuff (Round Cube & End Turn)")]
    [Tooltip("The 'Turn Stuff' parent of Round Cube and End Turn - grows in from zero scale (not moved) to reveal both at once, leaving End Turn's own hidden/revealed slide completely untouched. A stand-in for a fade, since the shader on these meshes doesn't support transparency.")]
    [SerializeField] private Transform turnStuff;

    [Tooltip("Scale units per second the grow-in plays at, relative to Turn Stuff's own normal scale - 1 grows from nothing to full size in 1 second, 0.5 takes 2 seconds, etc.")]
    [SerializeField] private float turnStuffRevealSpeed = 1f;

    [Tooltip("Units per second Health UI glides into place at.")]
    [SerializeField] private float uiSlideSpeed = 8f;

    [Header("Pop Up Text Box Gliding")]
    [Tooltip("The active Pop Up Text Box that actually displays the dialogue (Pop Up Text Box 1) - the one that glides around every line.")]
    [SerializeField] private Transform popUpTextBox;

    [Tooltip("The other (hidden) Pop Up Text Box objects, 2 through 5 - purely position markers. One is picked at random every time a new line starts.")]
    [SerializeField] private Transform[] popUpTextBoxLocations = new Transform[4];

    [Tooltip("Units per second the pop-up text box glides between locations.")]
    [SerializeField] private float popUpGlideSpeed = 8f;

    [Header("All Enemies Cleared - Ending")]
    [Tooltip("Played once every one of the level's pre-placed enemies is dead. Controls freeze the instant it starts.")]
    [SerializeField] private DialogueSequence tadaaDialogue;

    [Tooltip("Scene to load once the player finishes reading it - must be listed in Build Settings.")]
    [SerializeField] private string mainMenuSceneName = "Main Menu";

    [SerializeField] private ScreenWipe.Style endingWipeStyle = ScreenWipe.Style.Circle;
    [SerializeField] private float endingWipeCoverDuration = 0.9f;
    [SerializeField] private float endingWipeRevealDuration = 0.9f;
    [SerializeField] private Color endingWipeColor = Color.black;

    private Coroutine popUpGlideRoutine;
    private Vector3 turnStuffOriginalScale;

    private void Awake()
    {
        // Guaranteed to run before PlayerController.Start() - every Awake()
        // in the scene runs before any Start() does - so the King never
        // flashes its reachable tiles on for a frame before this takes hold.
        PlayerController.HighlightOnStart = false;

        // Every enemy pawn in this scene always chases the King, ignoring
        // its own Fire Detection Range - runs before any enemy's own
        // Start()/PlanNextMove(), so this is true from their very first plan.
        EnemyPawnController.AlwaysTargetPlayer = true;

        if (pieceSelectionManager != null)
        {
            pieceSelectionManager.enabled = false;
        }

        if (cameraController != null)
        {
            cameraController.enabled = false;
        }

        // Turn Stuff otherwise stays whatever scale it's authored at in the
        // Editor - cache that as the reveal target, then zero it out here so
        // it's invisible from frame 1 regardless, rather than relying on it
        // being hand-set to zero in the scene.
        if (turnStuff != null)
        {
            turnStuffOriginalScale = turnStuff.localScale;
            turnStuff.localScale = Vector3.zero;
        }

        // This scene uses its own pre-placed enemies only - WaveManager
        // never spawns anything here, at any point. Disabling it stops its
        // own Start() from ever calling SpawnNextWave(), and nothing else
        // in this script ever re-enables it.
        if (waveManager != null)
        {
            waveManager.enabled = false;
        }

        // Disabling this here, before Unity even gets to calling anyone's
        // OnEnable(), stops DialogueSequence's own Play On Enable from
        // firing regardless of whether that checkbox is set - this script
        // re-enables it and starts the sequence itself, after the delay.
        if (dialogue != null)
        {
            dialogue.enabled = false;
        }

        shopToggle?.SetLocked(true);
    }

    private void OnEnable()
    {
        if (dialogue != null)
        {
            dialogue.LineStarted += HandleLineStarted;
        }

        XPManager.XPChanged += HandleFirstXPPickup;
        XPManager.XPChanged += HandleShopUnlockXPChanged;

        if (shopToggle != null)
        {
            shopToggle.Opened += HandleShopOpened;
            shopToggle.Closed += HandleShopClosed;
        }
    }

    private void OnDisable()
    {
        if (dialogue != null)
        {
            dialogue.LineStarted -= HandleLineStarted;
        }

        XPManager.XPChanged -= HandleFirstXPPickup;
        XPManager.XPChanged -= HandleShopUnlockXPChanged;

        if (shopToggle != null)
        {
            shopToggle.Opened -= HandleShopOpened;
            shopToggle.Closed -= HandleShopClosed;
        }

        if (shopClosedDialogue != null)
        {
            shopClosedDialogue.SequenceCompleted -= HandleShopClosedDialogueCompleted;
        }
    }

    private void OnDestroy()
    {
        PlayerController.HighlightOnStart = true;
        EnemyPawnController.AlwaysTargetPlayer = false;
    }

    private void Start()
    {
        StartCoroutine(PlayFirstLineAfterDelay());
        StartCoroutine(WaitForAllEnemiesCleared());
    }

    private IEnumerator PlayFirstLineAfterDelay()
    {
        yield return new WaitForSeconds(delayBeforeDialogue);

        if (dialogue != null)
        {
            // Re-enable it first - Update() (which reads the left click that
            // advances each line) doesn't run at all while disabled.
            dialogue.enabled = true;
            dialogue.Play();
        }
    }

    private void HandleLineStarted(int lineIndex)
    {
        GlideTextBoxToRandomLocation();

        switch (lineIndex)
        {
            case 2:
                SlideIntoPlace(healthUI, healthUITargetLocalPosition);
                break;
            case 3:
                if (cameraController != null)
                {
                    cameraController.enabled = true;
                }
                break;
            case 5:
                UnlockKing();
                break;
            case 6:
                StartCoroutine(ScaleIn(turnStuff, turnStuffOriginalScale, turnStuffRevealSpeed));
                break;
        }
    }

    // XPManager.XPChanged also fires from spending XP (summoning a pawn),
    // but that can never happen before the player has earned at least
    // enough XP to afford it - so its first-ever invocation is always the
    // first pickup. Unsubscribing immediately keeps this from re-sliding
    // (harmlessly, but pointlessly) on every XP change after that.
    private void HandleFirstXPPickup(int totalXP)
    {
        XPManager.XPChanged -= HandleFirstXPPickup;
        SlideIntoPlace(xpUI, xpUITargetLocalPosition);
    }

    private void HandleShopUnlockXPChanged(int totalXP)
    {
        if (totalXP < xpToUnlockShop)
        {
            return;
        }

        XPManager.XPChanged -= HandleShopUnlockXPChanged;
        shopToggle?.SetLocked(false);

        if (shopTextBox != null)
        {
            shopTextBox.SetActive(true);
        }
    }

    private void HandleShopOpened()
    {
        if (shopToggle != null)
        {
            shopToggle.Opened -= HandleShopOpened;
        }

        if (shopTextBox != null)
        {
            Destroy(shopTextBox);
        }
    }

    // Play() reveals it itself (it calls gameObject.SetActive(true)
    // internally), so there's nothing extra to activate here first.
    private void HandleShopClosed()
    {
        if (shopToggle != null)
        {
            shopToggle.Closed -= HandleShopClosed;
        }

        if (shopClosedDialogue == null)
        {
            return;
        }

        shopClosedDialogue.SequenceCompleted += HandleShopClosedDialogueCompleted;
        shopClosedDialogue.Play();
    }

    private void HandleShopClosedDialogueCompleted()
    {
        if (shopClosedDialogue == null)
        {
            return;
        }

        shopClosedDialogue.SequenceCompleted -= HandleShopClosedDialogueCompleted;
        Destroy(shopClosedDialogue.gameObject);
    }

    private void UnlockKing()
    {
        if (pieceSelectionManager != null)
        {
            pieceSelectionManager.enabled = true;
        }

        king?.Select();
    }

    // Picks one of the 4 hidden Pop Up Text Box marker positions at random
    // and glides the real (active) box there - stopping any glide already
    // in progress first, so a line that starts before the previous glide
    // finished doesn't fight it for the same Transform.
    private void GlideTextBoxToRandomLocation()
    {
        if (popUpTextBox == null || popUpTextBoxLocations == null || popUpTextBoxLocations.Length == 0)
        {
            return;
        }

        Transform destination = popUpTextBoxLocations[Random.Range(0, popUpTextBoxLocations.Length)];

        if (destination == null)
        {
            return;
        }

        if (popUpGlideRoutine != null)
        {
            StopCoroutine(popUpGlideRoutine);
        }

        popUpGlideRoutine = StartCoroutine(SlideWorldPosition(popUpTextBox, destination.position, popUpGlideSpeed));
    }

    private void SlideIntoPlace(Transform target, Vector3 localPosition)
    {
        if (target != null)
        {
            StartCoroutine(SlideLocalPosition(target, localPosition, uiSlideSpeed));
        }
    }

    private static IEnumerator SlideLocalPosition(Transform target, Vector3 localPosition, float speed)
    {
        while (target != null && target.localPosition != localPosition)
        {
            target.localPosition = Vector3.MoveTowards(target.localPosition, localPosition, speed * Time.deltaTime);
            yield return null;
        }
    }

    private static IEnumerator SlideWorldPosition(Transform target, Vector3 position, float speed)
    {
        while (target != null && target.position != position)
        {
            target.position = Vector3.MoveTowards(target.position, position, speed * Time.deltaTime);
            yield return null;
        }
    }

    // Waits until every one of the level's pre-placed enemies is dead -
    // WaveManager never spawns anything in this scene, so
    // EnemyPawnController.All only ever contains those. Checked every frame
    // via WaitUntil, so it's not tied to End Turn - a King capture that
    // happens to kill the last enemy mid-turn triggers this immediately.
    private IEnumerator WaitForAllEnemiesCleared()
    {
        yield return new WaitUntil(() => EnemyPawnController.All.Count == 0);

        if (pieceSelectionManager != null)
        {
            pieceSelectionManager.enabled = false;
        }

        if (cameraController != null)
        {
            cameraController.enabled = false;
        }

        if (tadaaDialogue == null)
        {
            LoadMainMenu();
            yield break;
        }

        bool sequenceDone = false;
        void HandleCompleted() => sequenceDone = true;

        tadaaDialogue.SequenceCompleted += HandleCompleted;
        tadaaDialogue.Play();

        yield return new WaitUntil(() => sequenceDone);
        tadaaDialogue.SequenceCompleted -= HandleCompleted;

        LoadMainMenu();
    }

    private void LoadMainMenu()
    {
        ScreenWipe.LoadScene(mainMenuSceneName, endingWipeStyle, endingWipeCoverDuration, endingWipeRevealDuration, endingWipeColor);
    }

    // Grows root from zero to targetScale, at speed scale units per second -
    // works with any shader, unlike a true alpha fade, since it never
    // touches materials at all. targetScale must be captured before root
    // gets zeroed out (see Awake) - by the time this runs, root's own
    // current scale is already zero, not whatever it's authored at.
    private static IEnumerator ScaleIn(Transform root, Vector3 targetScale, float speed)
    {
        if (root == null)
        {
            yield break;
        }

        float rate = speed > 0f ? speed : 1f;

        while (root.localScale != targetScale)
        {
            root.localScale = Vector3.MoveTowards(root.localScale, targetScale, rate * Time.deltaTime);
            yield return null;
        }
    }
}
