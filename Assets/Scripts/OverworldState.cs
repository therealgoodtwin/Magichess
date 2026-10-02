using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the Overworld looked like the last time the player left it through a
/// scene transition: the tile the King was standing on, the tiles that had
/// already sunk behind him, and the encounters already taken. Kept in memory
/// only, so it lasts until the game is closed. Tiles and encounters are
/// remembered by position, since the objects themselves are rebuilt every
/// time the scene loads.
/// </summary>
public static class OverworldState
{
    // Closer than this counts as the same spot.
    private const float MatchDistance = 0.1f;

    public static bool HasSave { get; private set; }
    public static string SceneName { get; private set; }
    public static Vector3 KingTilePosition { get; private set; }

    private static readonly List<Vector3> sunkTilePositions = new();
    private static readonly List<Vector3> clearedEncounterPositions = new();

    public static IReadOnlyList<Vector3> SunkTilePositions => sunkTilePositions;
    public static IReadOnlyList<Vector3> ClearedEncounterPositions => clearedEncounterPositions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        HasSave = false;
        SceneName = null;
        KingTilePosition = Vector3.zero;
        sunkTilePositions.Clear();
        clearedEncounterPositions.Clear();
    }

    public static void Save(string sceneName, Vector3 kingTilePosition, IEnumerable<Vector3> sunkTiles, IEnumerable<Vector3> clearedEncounters)
    {
        HasSave = true;
        SceneName = sceneName;
        KingTilePosition = kingTilePosition;

        sunkTilePositions.Clear();
        sunkTilePositions.AddRange(sunkTiles);

        clearedEncounterPositions.Clear();
        clearedEncounterPositions.AddRange(clearedEncounters);
    }

    public static bool IsEncounterCleared(string sceneName, Vector3 position)
    {
        if (!HasSave || sceneName != SceneName)
        {
            return false;
        }

        foreach (Vector3 cleared in clearedEncounterPositions)
        {
            if ((cleared - position).sqrMagnitude < MatchDistance * MatchDistance)
            {
                return true;
            }
        }

        return false;
    }
}
