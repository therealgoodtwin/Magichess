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

    // What each Encounter Spawn Tile rolled, kept from the moment it's
    // rolled rather than only when the player leaves, so the same
    // encounters are there every time the Overworld loads.
    private struct EncounterRoll
    {
        public string SceneName;
        public Vector3 TilePosition;
        public string PrefabName;
        public string BattleMap;
    }

    private static readonly List<EncounterRoll> encounterRolls = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        HasSave = false;
        SceneName = null;
        KingTilePosition = Vector3.zero;
        sunkTilePositions.Clear();
        clearedEncounterPositions.Clear();
        encounterRolls.Clear();
    }

    public static bool TryGetEncounterRoll(string sceneName, Vector3 tilePosition, out string prefabName, out string battleMap)
    {
        int index = FindEncounterRoll(sceneName, tilePosition);

        prefabName = index >= 0 ? encounterRolls[index].PrefabName : null;
        battleMap = index >= 0 ? encounterRolls[index].BattleMap : null;
        return index >= 0;
    }

    public static void SetEncounterRoll(string sceneName, Vector3 tilePosition, string prefabName, string battleMap)
    {
        EncounterRoll roll = new EncounterRoll
        {
            SceneName = sceneName,
            TilePosition = tilePosition,
            PrefabName = prefabName,
            BattleMap = battleMap
        };

        int index = FindEncounterRoll(sceneName, tilePosition);

        if (index >= 0)
        {
            encounterRolls[index] = roll;
        }
        else
        {
            encounterRolls.Add(roll);
        }
    }

    // The prefab names rolled so far on a map, leaving out whatever one
    // tile rolled (the tile about to roll again).
    public static List<string> GetRolledEncounters(string sceneName, Vector3 exceptTilePosition)
    {
        List<string> rolled = new();

        foreach (EncounterRoll roll in encounterRolls)
        {
            if (roll.SceneName == sceneName &&
                (roll.TilePosition - exceptTilePosition).sqrMagnitude >= MatchDistance * MatchDistance)
            {
                rolled.Add(roll.PrefabName);
            }
        }

        return rolled;
    }

    private static int FindEncounterRoll(string sceneName, Vector3 tilePosition)
    {
        for (int i = 0; i < encounterRolls.Count; i++)
        {
            if (encounterRolls[i].SceneName == sceneName &&
                (encounterRolls[i].TilePosition - tilePosition).sqrMagnitude < MatchDistance * MatchDistance)
            {
                return i;
            }
        }

        return -1;
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
