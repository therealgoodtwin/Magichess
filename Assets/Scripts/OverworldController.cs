using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runs the Overworld. There are no turns here: the King is always selected
/// and can keep moving to any tile next to him. Every tile he steps off
/// sinks out of the way and can't be walked onto again. Moving onto an
/// encounter (see OverworldEncounter) saves the Overworld and wipes over to
/// that encounter's scene. Coming back restores everything as it was left,
/// with the King on the encounter's tile.
/// </summary>
// Starts after the King's own Start, so he already knows which tile he's on.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(PieceSelectionManager))]
public class OverworldController : MonoBehaviour
{
    [Tooltip("How far down, in world units, a tile sinks once the King steps off it.")]
    [SerializeField] private float sinkDepth = 4f;

    [Tooltip("Seconds between the King taking an encounter and the screen wipe starting, so the pawn breaking apart can be seen.")]
    [SerializeField] private float encounterDelay = 0.5f;

    [Tooltip("When coming back to the Overworld, how far below the centre of the screen the King ends up, in world units. 0 centres him exactly.")]
    [SerializeField] private float cameraLowerBy = 1.25f;

    private PlayerController king;
    private PieceSelectionManager selectionManager;

    private readonly List<Tile> sunkTiles = new();
    private readonly List<Vector3> clearedEncounters = new();

    private bool isLeaving;

    private void Awake()
    {
        TurnManager.DisableTurns(this);
        selectionManager = GetComponent<PieceSelectionManager>();
    }

    private void OnDestroy()
    {
        TurnManager.RestoreTurns(this);
    }

    private void OnEnable()
    {
        PlayerController.KingMoved += HandleKingMoved;
    }

    private void OnDisable()
    {
        PlayerController.KingMoved -= HandleKingMoved;
    }

    private void Start()
    {
        king = FindFirstObjectByType<PlayerController>();

        if (king == null)
        {
            Debug.LogWarning("OverworldController: no King (PlayerController) found in the scene.", this);
            return;
        }

        RestoreSavedState();

        // With turns off the King doesn't light up his own tiles at start.
        king.Select();
    }

    private void RestoreSavedState()
    {
        if (!OverworldState.HasSave || OverworldState.SceneName != gameObject.scene.name)
        {
            return;
        }

        clearedEncounters.AddRange(OverworldState.ClearedEncounterPositions);

        // The King moves first: TeleportTo keeps his height relative to the
        // tile he's leaving, which has to still be at its normal height.
        Tile kingTile = TileGrid.FindNearest(OverworldState.KingTilePosition);

        if (kingTile != null)
        {
            king.TeleportTo(kingTile);
            FocusCamera(kingTile);
        }

        foreach (Vector3 position in OverworldState.SunkTilePositions)
        {
            Tile tile = TileGrid.FindNearest(position);

            if (tile != null && !tile.IsSunk)
            {
                tile.Sink(sinkDepth, true);
                sunkTiles.Add(tile);
            }
        }
    }

    private void FocusCamera(Tile tile)
    {
        CameraController cameraController = FindFirstObjectByType<CameraController>();

        if (cameraController != null)
        {
            cameraController.GlideToFocus(tile.BaseWorldPosition, 0f, cameraLowerBy, null);
        }
    }

    private void HandleKingMoved()
    {
        if (isLeaving || king == null)
        {
            return;
        }

        Tile left = king.PreviousTile;

        if (left != null && left != king.CurrentTile && !left.IsSunk)
        {
            left.Sink(sinkDepth, false);
            sunkTiles.Add(left);
        }

        OverworldEncounter encounter = OverworldEncounter.FindOnTile(king.CurrentTile);

        if (encounter == null)
        {
            // The King re-lit his neighbours as he landed, before the tile
            // he left sank - light them again without it.
            king.Select();
            return;
        }

        string sceneName = encounter.SceneName;
        clearedEncounters.Add(encounter.HomePosition);
        encounter.Clear();

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning($"OverworldController: encounter '{encounter.name}' has no scene set, so there's nowhere to go.", encounter);
            king.Select();
            return;
        }

        // Caught here rather than left to the wipe, which would cover the
        // screen, fail to load, and reveal this same scene with the King
        // stuck in place.
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"OverworldController: can't load scene '{sceneName}' - add it to the scene list in File > Build Profiles (or click 'Add to Build' next to the encounter's Scene slot).", encounter);
            king.Select();
            return;
        }

        StartCoroutine(Leave(sceneName, encounterDelay));
    }

    // Saves the Overworld and wipes over to another scene. Anything that
    // takes the player out of the Overworld should go through here, so
    // coming back finds everything as it was left.
    public void LeaveTo(string sceneName)
    {
        if (!isLeaving)
        {
            StartCoroutine(Leave(sceneName, 0f));
        }
    }

    private IEnumerator Leave(string sceneName, float delay)
    {
        isLeaving = true;
        selectionManager.enabled = false;

        if (king != null)
        {
            king.Deselect();
        }

        SaveState();

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        // A wipe that's still revealing this scene would ignore the request.
        while (ScreenWipe.IsBusy)
        {
            yield return null;
        }

        // Handed back now rather than in OnDestroy: the next scene can start
        // up before this one is torn down, and must find turns back on
        // unless it switches them off itself.
        TurnManager.RestoreTurns(this);
        ScreenWipe.LoadScene(sceneName);
    }

    private void SaveState()
    {
        List<Vector3> sunkPositions = new();

        foreach (Tile tile in sunkTiles)
        {
            if (tile != null)
            {
                sunkPositions.Add(tile.transform.position);
            }
        }

        Vector3 kingTilePosition = king != null && king.CurrentTile != null
            ? king.CurrentTile.transform.position
            : king != null ? king.transform.position : Vector3.zero;

        OverworldState.Save(gameObject.scene.name, kingTilePosition, sunkPositions, clearedEncounters);
    }
}
