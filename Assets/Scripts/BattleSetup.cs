using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// A battle's setup phase. Put this on one object in a battle scene (the
/// Manager).
///
/// As the scene starts every piece is waiting in its side's Pawn Case (see
/// PawnCase) - all but the King, who's already on the board - and nothing
/// can be selected or moved. The rows of tiles nearest the player's case
/// flash green: that's where the player can deploy. The player clicks a
/// piece in the case to pick it up - a see-through copy of it follows the
/// cursor, turning red anywhere it can't be put down - and clicks a free
/// green tile to deploy it there. The enemy then deploys one of its own onto
/// the rows nearest its case, and so on in turn until both sides' cases are
/// empty; a side with pieces left over carries on alone.
///
/// Then the battle begins: an Initiative Turn Manager starts its first
/// round, or, in a scene without one, the usual End Turn play starts.
/// </summary>
// After every Pawn Case has filled its holders (and so after every piece's
// own Start).
[DefaultExecutionOrder(100)]
public class BattleSetup : MonoBehaviour
{
    [Tooltip("How many rows of tiles each side can deploy onto, counted from the edge of the board nearest that side's Pawn Case.")]
    [SerializeField, Min(1)] private int deployRows = 2;

    [Tooltip("Seconds the enemy waits before deploying each of its pieces.")]
    [SerializeField] private float enemyDeployDelay = 0.6f;

    [Tooltip("Seconds a piece takes to travel from its Pawn Holder to its tile. 0 puts it there at once.")]
    [SerializeField] private float deployMoveTime = 0.35f;

    [Tooltip("How high a piece rises on its way from its Pawn Holder to its tile.")]
    [SerializeField] private float deployHopHeight = 3f;

    [Header("Ghost")]
    [Tooltip("The picked-up piece's see-through copy, over a tile it can be deployed on.")]
    [SerializeField] private Color ghostColor = new Color(1f, 1f, 1f, 0.6f);

    [Tooltip("The same copy anywhere it can't be deployed: outside the green rows, or on a tile that's taken.")]
    [SerializeField] private Color ghostBlockedColor = new Color(1f, 0.15f, 0.15f, 0.6f);

    private static BattleSetup active;

    // On from the moment a battle scene with a Battle Setup loads until both
    // sides have deployed. Selecting, moving, enemy plans and the turn
    // system all stand down while it's on.
    public static bool IsActive => active != null;

    // Raised once both sides have deployed and the battle can begin.
    public static event System.Action Finished;

    private readonly List<IDeployablePiece> playerWaiting = new();
    private readonly List<IDeployablePiece> enemyWaiting = new();

    private List<Tile> playerZone = new();
    private List<Tile> enemyZone = new();

    // Where each tile is drawn, measured once - tiles don't lift during
    // setup - and how high the board's surface is, for following the cursor
    // across it.
    private readonly Dictionary<Tile, Bounds> tileBounds = new();
    private float boardTopY;

    private GameObject ghost;
    private Material ghostMaterial;

    // From the middle of the held piece's base to its own position, so its
    // ghost can be stood wherever the cursor points.
    private Vector3 heldBaseOffset;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        active = null;
    }

    private void Awake()
    {
        // Switched off in the Inspector, this never runs - so it mustn't
        // lock the battle either.
        if (enabled)
        {
            active = this;
        }
    }

    private void OnDestroy()
    {
        if (active == this)
        {
            active = null;
        }

        DestroyGhost();

        if (ghostMaterial != null)
        {
            Destroy(ghostMaterial);
        }
    }

    private void Start()
    {
        // Pieces may have lit up their moves by themselves as the scene
        // started - during setup nothing is selected.
        DeselectPlayerPieces();
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        // Every Pawn Case has filled its own holders by now. This catches
        // any holder that isn't in a case.
        PawnHolder.FillAll();

        CollectWaitingPieces();
        playerZone = FindZone(PawnCase.Side.Player);
        enemyZone = FindZone(PawnCase.Side.Enemy);
        MeasureBoard();

        // One frame, so a King spawned during Awake has had his Start too,
        // and can be told to put his own highlights away.
        yield return null;
        DeselectPlayerPieces();

        // Nothing to click until the scene's reveal has finished.
        while (ScreenWipe.IsBusy)
        {
            yield return null;
        }

        RefreshZoneHighlight();

        // One each, the player first, until neither has anything left to
        // deploy - or anywhere left to deploy it.
        while (true)
        {
            bool anyDeployed = false;

            if (playerWaiting.Count > 0 && HasFreeTile(playerZone))
            {
                yield return PlayerDeploys();
                anyDeployed = true;
            }

            if (enemyWaiting.Count > 0 && HasFreeTile(enemyZone))
            {
                yield return EnemyDeploys();
                anyDeployed = true;
            }

            if (!anyDeployed)
            {
                break;
            }
        }

        if (playerWaiting.Count > 0 || enemyWaiting.Count > 0)
        {
            Debug.LogWarning($"BattleSetup: {playerWaiting.Count} of the player's piece(s) and {enemyWaiting.Count} of the enemy's had no free tile to deploy onto, and sit this battle out in their Pawn Case.", this);
        }

        Finish();
    }

    private void Finish()
    {
        foreach (Tile tile in playerZone)
        {
            tile.SetDeployHighlighted(false);
        }

        active = null;
        Finished?.Invoke();

        // An Initiative Turn Manager hands the turns out itself from here.
        // Without one the scene starts the way it always has: with the King
        // selected.
        if (InitiativeTurnManager.IsRunning || !TurnManager.TurnsEnabled || !PlayerController.HighlightOnStart)
        {
            return;
        }

        PieceSelectionManager selection = FindFirstObjectByType<PieceSelectionManager>();
        PlayerController king = FindFirstObjectByType<PlayerController>();

        if (selection != null && king != null)
        {
            selection.ActivatePiece(king);
        }
    }

    private static void DeselectPlayerPieces()
    {
        foreach (PlayerController king in PlayerController.All)
        {
            king.Deselect();
        }

        foreach (WhitePawnController pawn in WhitePawnController.All)
        {
            pawn.Deselect();
        }

        foreach (RookController rook in RookController.All)
        {
            rook.Deselect();
        }

        foreach (KnightController knight in KnightController.All)
        {
            knight.Deselect();
        }

        foreach (BishopController bishop in BishopController.All)
        {
            bishop.Deselect();
        }
    }

    // Whatever is standing on each side's Pawn Holders is what that side has
    // to deploy.
    private void CollectWaitingPieces()
    {
        foreach (PawnHolder holder in PawnHolder.All)
        {
            if (!holder.IsFree)
            {
                (holder.Side == PawnCase.Side.Player ? playerWaiting : enemyWaiting).Add(holder.Occupant);
            }
        }
    }

    // The tiles a side can deploy onto: the first Deploy Rows rows of the
    // board, counted from whichever of its four edges that side's Pawn
    // Holders stand beyond. Empty if the side has no holders.
    private List<Tile> FindZone(PawnCase.Side side)
    {
        List<Tile> zone = new();

        Vector3 casesCenter = Vector3.zero;
        int holderCount = 0;

        foreach (PawnHolder holder in PawnHolder.All)
        {
            if (holder.Side == side)
            {
                casesCenter += holder.transform.position;
                holderCount++;
            }
        }

        if (holderCount == 0 || Tile.All.Count == 0)
        {
            return zone;
        }

        casesCenter /= holderCount;

        Vector3 boardCenter = Vector3.zero;

        foreach (Tile tile in Tile.All)
        {
            boardCenter += tile.transform.position;
        }

        boardCenter /= Tile.All.Count;

        // Which way the holders lie from the board decides which edge the
        // rows are counted from. The grid's columns follow the world's X
        // axis and its rows the Z axis (see TileGrid).
        Vector3 toCases = casesCenter - boardCenter;
        bool alongX = Mathf.Abs(toCases.x) >= Mathf.Abs(toCases.z);
        int direction = (alongX ? toCases.x : toCases.z) >= 0f ? 1 : -1;

        int edge = int.MinValue;

        foreach (Tile tile in Tile.All)
        {
            if (TileGrid.TryGetCoord(tile, out Vector2Int coord))
            {
                edge = Mathf.Max(edge, (alongX ? coord.x : coord.y) * direction);
            }
        }

        foreach (Tile tile in Tile.All)
        {
            if (tile.IsSunk || TileGrid.IsBlocked(tile) || !TileGrid.TryGetCoord(tile, out Vector2Int coord))
            {
                continue;
            }

            int rowsFromEdge = edge - (alongX ? coord.x : coord.y) * direction;

            if (rowsFromEdge < deployRows)
            {
                zone.Add(tile);
            }
        }

        return zone;
    }

    private void MeasureBoard()
    {
        float topSum = 0f;

        foreach (Tile tile in Tile.All)
        {
            if (tile.TryGetComponent(out Renderer tileRenderer))
            {
                tileBounds[tile] = tileRenderer.bounds;
                topSum += tileRenderer.bounds.max.y;
            }
        }

        boardTopY = tileBounds.Count > 0 ? topSum / tileBounds.Count : 0f;
    }

    // Free to deploy onto: in play, and with nothing of either side's on it
    // (the King's tile included).
    private static bool IsFree(Tile tile)
    {
        return !tile.IsSunk && !TileGrid.IsBlocked(tile) &&
            !BoardPieces.IsPlayerPieceOn(tile) && !BoardPieces.IsEnemyOn(tile);
    }

    private static bool HasFreeTile(List<Tile> zone)
    {
        foreach (Tile tile in zone)
        {
            if (IsFree(tile))
            {
                return true;
            }
        }

        return false;
    }

    // The player's rows flash green wherever a piece can still be put down,
    // for as long as the player has pieces left to deploy.
    private void RefreshZoneHighlight()
    {
        bool hasPieces = playerWaiting.Count > 0;

        foreach (Tile tile in playerZone)
        {
            tile.SetDeployHighlighted(hasPieces && IsFree(tile));
        }
    }

    // The player's go: click a waiting piece to pick it up (click another to
    // swap, right-click or Escape to put it back), then click a free green
    // tile to deploy it. Ends once a piece has been deployed.
    private IEnumerator PlayerDeploys()
    {
        IDeployablePiece held = null;
        Tile chosen = null;

        while (chosen == null)
        {
            yield return null;

            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;

            if (mouse == null || cam == null || ScreenWipe.IsBusy)
            {
                continue;
            }

            Vector2 pointer = mouse.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(pointer);

            // A click on a menu button or window isn't a click on the board
            // behind it.
            bool clicked = mouse.leftButton.wasPressedThisFrame && !SettingsWindowController.IsPointerOverUI(pointer);

            if (held != null && (mouse.rightButton.wasPressedThisFrame || WasEscapePressed()))
            {
                held = null;
                DestroyGhost();
                continue;
            }

            if (held != null)
            {
                bool isOverBoard = TryFindBoardPoint(ray, out Vector3 point, out Tile hovered);
                bool canDeploy = hovered != null && playerZone.Contains(hovered) && IsFree(hovered);

                ShowGhost(isOverBoard, point, hovered, canDeploy);

                if (clicked && canDeploy)
                {
                    chosen = hovered;
                    break;
                }
            }

            if (clicked)
            {
                IDeployablePiece picked = FindWaitingPieceUnder(ray);

                if (picked != null && picked != held)
                {
                    held = picked;
                    BuildGhost(held);
                }
            }
        }

        DestroyGhost();
        yield return Deploy(held, chosen);
    }

    // The enemy's go: one of its waiting pieces, onto one of its free tiles,
    // both picked at random.
    private IEnumerator EnemyDeploys()
    {
        if (enemyDeployDelay > 0f)
        {
            yield return new WaitForSeconds(enemyDeployDelay);
        }

        List<Tile> freeTiles = enemyZone.FindAll(IsFree);

        if (enemyWaiting.Count == 0 || freeTiles.Count == 0)
        {
            yield break;
        }

        IDeployablePiece piece = enemyWaiting[Random.Range(0, enemyWaiting.Count)];
        yield return Deploy(piece, freeTiles[Random.Range(0, freeTiles.Count)]);
    }

    // Takes a piece from its Pawn Holder to a tile, in a short hop, and
    // stands it there.
    private IEnumerator Deploy(IDeployablePiece piece, Tile tile)
    {
        playerWaiting.Remove(piece);
        enemyWaiting.Remove(piece);

        PawnHolder holder = PawnHolder.FindHolding(piece);

        if (holder != null)
        {
            holder.Release();
        }

        Transform pieceTransform = piece.transform;

        if (deployMoveTime > 0f &&
            tileBounds.TryGetValue(tile, out Bounds bounds) &&
            PiecePivotUtility.TryGetBaseOffset(pieceTransform, out Vector3 baseOffset))
        {
            Vector3 from = pieceTransform.position;
            Vector3 to = PiecePivotUtility.TopCenter(bounds) + baseOffset;

            for (float t = 0f; t < 1f; t += Time.deltaTime / deployMoveTime)
            {
                Vector3 position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                position.y += Mathf.Sin(t * Mathf.PI) * deployHopHeight;
                pieceTransform.position = position;
                yield return null;
            }
        }

        piece.PlaceOn(tile);
        RefreshZoneHighlight();
    }

    // Where the cursor points on the board's surface, and the tile there, if
    // any. False if it isn't pointing down at the board's level at all.
    private bool TryFindBoardPoint(Ray ray, out Vector3 point, out Tile tile)
    {
        tile = null;

        Plane surface = new Plane(Vector3.up, new Vector3(0f, boardTopY, 0f));

        if (!surface.Raycast(ray, out float distance))
        {
            point = Vector3.zero;
            return false;
        }

        point = ray.GetPoint(distance);

        foreach (KeyValuePair<Tile, Bounds> entry in tileBounds)
        {
            Bounds bounds = entry.Value;

            if (point.x >= bounds.min.x && point.x <= bounds.max.x &&
                point.z >= bounds.min.z && point.z <= bounds.max.z)
            {
                tile = entry.Key;
                break;
            }
        }

        return true;
    }

    // The waiting piece of the player's that a click lands on - on its
    // model, or on the Pawn Holder it's standing on. Goes by what's drawn
    // rather than by colliders, so it works whatever a piece is made of.
    private IDeployablePiece FindWaitingPieceUnder(Ray ray)
    {
        IDeployablePiece nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (IDeployablePiece piece in playerWaiting)
        {
            if (PiecePivotUtility.TryGetModelBounds(piece.transform, out Bounds bounds) &&
                bounds.IntersectRay(ray, out float distance) && distance < nearestDistance)
            {
                nearest = piece;
                nearestDistance = distance;
            }

            PawnHolder holder = PawnHolder.FindHolding(piece);

            if (holder != null && holder.IsHitBy(ray, out float holderDistance) && holderDistance < nearestDistance)
            {
                nearest = piece;
                nearestDistance = holderDistance;
            }
        }

        return nearest;
    }

    private static bool WasEscapePressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
    }

    // A see-through copy of a piece's model - its meshes only, with none of
    // what makes it a piece - to follow the cursor while that piece is held.
    private void BuildGhost(IDeployablePiece piece)
    {
        DestroyGhost();

        Transform source = piece.transform;
        PiecePivotUtility.TryGetBaseOffset(source, out heldBaseOffset);

        ghost = new GameObject($"{source.name} (Ghost)");
        ghost.transform.SetPositionAndRotation(source.position, source.rotation);

        Material material = GetGhostMaterial();

        foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null || !filter.TryGetComponent(out MeshRenderer sourceRenderer) || !sourceRenderer.enabled)
            {
                continue;
            }

            GameObject part = new GameObject(filter.name);
            part.transform.SetParent(ghost.transform);
            part.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
            part.transform.localScale = filter.transform.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;

            Material[] materials = new Material[Mathf.Max(1, sourceRenderer.sharedMaterials.Length)];

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            MeshRenderer partRenderer = part.AddComponent<MeshRenderer>();
            partRenderer.sharedMaterials = materials;
            partRenderer.shadowCastingMode = ShadowCastingMode.Off;
            partRenderer.receiveShadows = false;
        }

        // Out of sight until the cursor is somewhere to show it.
        ghost.SetActive(false);
    }

    // Stands the ghost on the tile the cursor is over, or right under the
    // cursor between tiles and off the board - red unless it's somewhere the
    // held piece can actually be deployed.
    private void ShowGhost(bool isOverBoard, Vector3 point, Tile hovered, bool canDeploy)
    {
        if (ghost == null)
        {
            return;
        }

        ghost.SetActive(isOverBoard);

        if (!isOverBoard)
        {
            return;
        }

        if (hovered != null && tileBounds.TryGetValue(hovered, out Bounds bounds))
        {
            point = PiecePivotUtility.TopCenter(bounds);
        }

        ghost.transform.position = point + heldBaseOffset;
        GetGhostMaterial().color = canDeploy ? ghostColor : ghostBlockedColor;
    }

    private void DestroyGhost()
    {
        if (ghost != null)
        {
            Destroy(ghost);
            ghost = null;
        }
    }

    private Material GetGhostMaterial()
    {
        if (ghostMaterial != null)
        {
            return ghostMaterial;
        }

        // Same see-through setup the Shop's summon preview uses.
        Shader shader = Shader.Find("Standard");

        if (shader != null)
        {
            ghostMaterial = new Material(shader);
            ghostMaterial.SetFloat("_Mode", 3f);
            ghostMaterial.SetFloat("_Glossiness", 0f);
            ghostMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            ghostMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            ghostMaterial.SetInt("_ZWrite", 0);
            ghostMaterial.DisableKeyword("_ALPHATEST_ON");
            ghostMaterial.EnableKeyword("_ALPHABLEND_ON");
            ghostMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            ghostMaterial.renderQueue = 3000;
        }
        else
        {
            ghostMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        ghostMaterial.color = ghostColor;
        return ghostMaterial;
    }
}
