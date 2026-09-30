using System.Collections;
using UnityEngine;

/// <summary>
/// Put on a tile, or on an object standing over one (like the Arsenal
/// Teleporter). When the King finishes a move onto that tile, the player is
/// wiped back to the Overworld, which puts him back on the tile of the
/// encounter that sent him here.
/// </summary>
public class OverworldExit : MonoBehaviour
{
    [Tooltip("Leave empty to go back to whichever Overworld the player came from. Only needed when playing this scene straight from the Editor without going through the Overworld first.")]
    [SerializeField] private SceneField overworldScene;

    private Tile tile;
    private bool isLeaving;

    // The tile this is on, or stands over, found once tiles exist.
    private Tile Tile
    {
        get
        {
            if (tile == null && !TryGetComponent(out tile))
            {
                tile = TileGrid.FindNearest(transform.position);
            }

            return tile;
        }
    }

    private void OnEnable()
    {
        PlayerController.KingMoved += HandleKingMoved;
    }

    private void OnDisable()
    {
        PlayerController.KingMoved -= HandleKingMoved;
    }

    private void HandleKingMoved()
    {
        if (isLeaving)
        {
            return;
        }

        PlayerController king = FindFirstObjectByType<PlayerController>();

        if (king == null || king.CurrentTile == null || king.CurrentTile != Tile)
        {
            return;
        }

        string sceneName = overworldScene != null && overworldScene.IsSet
            ? overworldScene.SceneName
            : OverworldState.SceneName;

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("OverworldExit: no Overworld to go back to - the player didn't come from one, and no Overworld Scene is set.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"OverworldExit: can't load scene '{sceneName}' - add it to the scene list in File > Build Profiles.", this);
            return;
        }

        isLeaving = true;
        king.Deselect();

        PieceSelectionManager selectionManager = FindFirstObjectByType<PieceSelectionManager>();

        if (selectionManager != null)
        {
            selectionManager.enabled = false;
        }

        StartCoroutine(Leave(sceneName));
    }

    private IEnumerator Leave(string sceneName)
    {
        // A wipe that's still revealing this scene would ignore the request.
        while (ScreenWipe.IsBusy)
        {
            yield return null;
        }

        ScreenWipe.LoadScene(sceneName);
    }
}
