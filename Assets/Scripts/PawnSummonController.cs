using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the Shop's SUMMON button: a press-in/press-out click (same style as
/// TurnActivationController's End Turn), only responsive while the player
/// has at least xpCost XP. A successful click hides the Shop and enters
/// placement mode - a translucent preview Pawn (White) follows the cursor
/// across the board until the player either clicks a free tile (spawns a
/// real pawn there and spends xpCost XP, looping back into placement if XP
/// remains) or presses Escape (cancels back to the Shop at no cost).
///
/// PieceSelectionManager checks the static IsPlacing flag and stands down
/// while it's set, so a placement click can't also be read as a normal
/// move/select click.
/// </summary>
public class PawnSummonController : MonoBehaviour
{
    [Header("Summon Button")]
    [Tooltip("The clickable 3D face of the SUMMON button (not the text).")]
    [SerializeField] private Transform summonButtonFace;

    [Tooltip("The camera the Shop's UI3D layer is rendered through (UI Camera).")]
    [SerializeField] private Camera uiCamera;

    [Tooltip("How far the button's face pushes in, along its own forward direction, when clicked.")]
    [SerializeField] private float pressDepth = 0.08f;

    [Tooltip("Units per second the press-in/press-out animation plays at.")]
    [SerializeField] private float pressSpeed = 20f;

    [Header("Summoning")]
    [Tooltip("The White Pawn prefab to summon.")]
    [SerializeField] private GameObject pawnPrefab;

    [Tooltip("XP cost per pawn summoned.")]
    [SerializeField] private int xpCost = 3;

    [Tooltip("Opacity the placement preview renders at.")]
    [SerializeField, Range(0f, 1f)] private float previewAlpha = 0.6f;

    [Tooltip("Shop panel to hide while placing and re-reveal once done or cancelled. Auto-found on this object if left empty.")]
    [SerializeField] private ShopToggleController shopToggle;

    public static bool IsPlacing { get; private set; }

    // Fired every time a real pawn is placed on the board (not the ghost
    // preview) - once per click that successfully spends XP on a tile.
    public static event System.Action PawnSummoned;

    private enum State
    {
        Idle,
        Pressing,
        Placing
    }

    private State state = State.Idle;

    private Vector3 pressStartLocalPosition;
    private bool isPressingIn;

    private GameObject previewInstance;
    private float pawnHeightOffset;

    private void Awake()
    {
        if (shopToggle == null)
        {
            shopToggle = GetComponent<ShopToggleController>();
        }
    }

    private void Start()
    {
        // How far above a tile's surface this pawn's pivot needs to sit for
        // its model to rest on top of a tile instead of sinking into (or
        // floating above) it - see PiecePivotUtility for why this is
        // derived from the prefab's own mesh rather than its position.
        if (pawnPrefab != null)
        {
            pawnHeightOffset = PiecePivotUtility.GetPivotHeightAboveTile(pawnPrefab);
        }
    }

    private void OnDisable()
    {
        if (state == State.Placing)
        {
            EndPlacement();
        }
    }

    private void Update()
    {
        switch (state)
        {
            case State.Idle:
                CheckForClick();
                break;
            case State.Pressing:
                AnimatePress();
                break;
            case State.Placing:
                UpdatePlacement();
                break;
        }
    }

    private void CheckForClick()
    {
        if (uiCamera == null || summonButtonFace == null || TurnManager.IsEnemyTurn || XPManager.TotalXP < xpCost)
        {
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Ray ray = uiCamera.ScreenPointToRay(mouse.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider.transform.IsChildOf(summonButtonFace))
        {
            pressStartLocalPosition = summonButtonFace.localPosition;
            isPressingIn = true;
            state = State.Pressing;
        }
    }

    private void AnimatePress()
    {
        Vector3 pressedPosition = pressStartLocalPosition - summonButtonFace.forward * pressDepth;
        Vector3 target = isPressingIn ? pressedPosition : pressStartLocalPosition;

        summonButtonFace.localPosition = Vector3.MoveTowards(summonButtonFace.localPosition, target, pressSpeed * Time.deltaTime);

        if (summonButtonFace.localPosition != target)
        {
            return;
        }

        if (isPressingIn)
        {
            isPressingIn = false;
            return;
        }

        BeginPlacement();
    }

    private void BeginPlacement()
    {
        shopToggle?.SetLocked(true);
        shopToggle?.Hide();
        SpawnPreview();
        state = State.Placing;
        IsPlacing = true;
    }

    private void SpawnPreview()
    {
        if (pawnPrefab == null)
        {
            return;
        }

        previewInstance = Instantiate(pawnPrefab);

        // Disable first so every real gameplay component's OnDisable runs
        // (unregistering from WhitePawnController.All, unsubscribing from
        // events, etc.) before it's stripped down to a visual-only preview.
        previewInstance.SetActive(false);
        StripForPreview(previewInstance);
        previewInstance.SetActive(true);
    }

    private void StripForPreview(GameObject instance)
    {
        foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
        {
            Destroy(behaviour);
        }

        foreach (Collider hitCollider in instance.GetComponentsInChildren<Collider>(true))
        {
            Destroy(hitCollider);
        }

        foreach (Rigidbody rb in instance.GetComponentsInChildren<Rigidbody>(true))
        {
            Destroy(rb);
        }

        ApplyPreviewMaterial(instance);
    }

    private void ApplyPreviewMaterial(GameObject instance)
    {
        Shader fadeShader = Shader.Find("Standard");

        foreach (Renderer instanceRenderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = instanceRenderer.materials;

            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                Material ghost = fadeShader != null ? new Material(fadeShader) : new Material(source);

                if (fadeShader != null)
                {
                    SetupTransparent(ghost);
                    Color tint = source != null && source.HasProperty("_Color") ? source.color : Color.white;
                    ghost.color = new Color(tint.r, tint.g, tint.b, previewAlpha);

                    if (source != null && source.HasProperty("_MainTex"))
                    {
                        ghost.mainTexture = source.mainTexture;
                    }
                }
                else
                {
                    Color c = ghost.color;
                    ghost.color = new Color(c.r, c.g, c.b, previewAlpha);
                }

                materials[i] = ghost;
            }

            instanceRenderer.materials = materials;
        }
    }

    private static void SetupTransparent(Material material)
    {
        material.SetFloat("_Mode", 3f);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }

    private void UpdatePlacement()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            EndPlacement();
            return;
        }

        Camera cam = Camera.main;
        Mouse mouse = Mouse.current;

        if (cam == null || mouse == null)
        {
            return;
        }

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit) || !hit.collider.TryGetComponent(out Tile hoverTile))
        {
            return;
        }

        if (previewInstance != null)
        {
            previewInstance.transform.position = hit.point + Vector3.up * pawnHeightOffset;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            TryPlaceOn(hoverTile);
        }
    }

    private void TryPlaceOn(Tile tile)
    {
        if (IsTileOccupied(tile))
        {
            return;
        }

        Vector3 spawnPosition = tile.BaseWorldPosition + Vector3.up * pawnHeightOffset;
        Instantiate(pawnPrefab, spawnPosition, pawnPrefab.transform.rotation);
        XPManager.SpendXP(xpCost);
        PawnSummoned?.Invoke();

        if (XPManager.TotalXP < xpCost)
        {
            EndPlacement();
            return;
        }

        if (previewInstance != null)
        {
            Destroy(previewInstance);
        }

        SpawnPreview();
    }

    private static bool IsTileOccupied(Tile tile)
    {
        // A Blocker's tile is closed to every piece, summoned ones included.
        if (TileGrid.IsBlocked(tile))
        {
            return true;
        }

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

        foreach (EnemyPawnController enemy in EnemyPawnController.All)
        {
            if (enemy.CurrentTile == tile)
            {
                return true;
            }
        }

        return false;
    }

    private void EndPlacement()
    {
        if (previewInstance != null)
        {
            Destroy(previewInstance);
        }

        state = State.Idle;
        IsPlacing = false;
        shopToggle?.SetLocked(false);
        shopToggle?.Show();
    }
}
