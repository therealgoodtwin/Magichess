using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Put on an Overworld tile. When the scene starts, puts a random encounter
/// from the Encounter Pool on this tile, centred. One rolled from the battle
/// folder is sent to a random battle map. Whatever was rolled is remembered
/// (see OverworldState), so coming back to the Overworld finds the same
/// encounters as before - and nothing at all on a tile whose encounter has
/// already been taken.
/// </summary>
public class EncounterSpawnTile : MonoBehaviour
{
    [Tooltip("Where the encounters and battle maps come from. Filled in automatically when this is added, if the project has exactly one Encounter Pool.")]
    [SerializeField] private EncounterPool pool;

    public GameObject Spawned { get; private set; }

    private void Awake()
    {
        if (pool == null)
        {
            Debug.LogWarning("EncounterSpawnTile: no Encounter Pool set.", this);
            return;
        }

        Tile tile = FindTile();

        if (tile == null)
        {
            Debug.LogWarning("EncounterSpawnTile: not on a tile.", this);
            return;
        }

        // Everything about this spawn is remembered by the tile's position,
        // which stays the same every time the scene loads.
        Vector3 tilePosition = tile.transform.position;
        string sceneName = gameObject.scene.name;

        if (OverworldState.IsEncounterCleared(sceneName, tilePosition))
        {
            return;
        }

        if (!TryGetRoll(sceneName, tilePosition, out GameObject prefab, out string battleMap))
        {
            return;
        }

        // Placed on the tile's position first: an Overworld Encounter already
        // on the prefab records where it was placed the moment it's created.
        Spawned = Instantiate(prefab, tilePosition, prefab.transform.rotation);
        PiecePivotUtility.CenterOnTile(Spawned.transform, tile);

        // Prefabs without an Overworld Encounter of their own get one, so
        // the King landing on them still counts.
        if (!Spawned.TryGetComponent(out OverworldEncounter encounter))
        {
            encounter = Spawned.AddComponent<OverworldEncounter>();
        }

        encounter.SetHomePosition(tilePosition);

        if (!string.IsNullOrEmpty(battleMap))
        {
            encounter.OverrideScene(battleMap);
        }
    }

    // Reuses what this tile rolled last time the scene loaded, if it's still
    // valid; otherwise rolls (and remembers) something new.
    private bool TryGetRoll(string sceneName, Vector3 tilePosition, out GameObject prefab, out string battleMap)
    {
        prefab = null;
        battleMap = null;

        if (OverworldState.TryGetEncounterRoll(sceneName, tilePosition, out string savedPrefab, out string savedBattleMap))
        {
            prefab = pool.FindEncounter(savedPrefab);
            battleMap = savedBattleMap;
        }

        if (prefab == null)
        {
            List<string> rolled = OverworldState.GetRolledEncounters(sceneName, tilePosition);
            int spawnTiles = CountSpawnTilesInScene();

            if (rolled.Count == 0 && pool.MinimumTotal > spawnTiles)
            {
                Debug.LogWarning($"EncounterSpawnTile: the Encounter Pool's Min Counts add up to {pool.MinimumTotal}, but this map only has {spawnTiles} spawn tiles, so not every minimum can be met.", pool);
            }

            prefab = pool.PickEncounter(rolled, Mathf.Max(1, spawnTiles - rolled.Count));
            battleMap = null;

            if (prefab == null)
            {
                if (pool.HasEncounters)
                {
                    Debug.LogWarning("EncounterSpawnTile: every encounter group has reached its Max Count, so this tile stays empty. To fill every tile, untick Has Limit on at least one group in the Encounter Pool, or raise the Max Counts so they add up to at least the number of spawn tiles.", this);
                }
                else
                {
                    Debug.LogWarning("EncounterSpawnTile: the Encounter Pool has no encounters in it - check its folders.", pool);
                }

                return false;
            }
        }

        if (!pool.UsesBattleMaps(prefab))
        {
            battleMap = null;
        }
        else if (!pool.CanLoadBattleMap(prefab, battleMap))
        {
            battleMap = pool.PickBattleMap(prefab);

            if (battleMap == null)
            {
                Debug.LogWarning($"EncounterSpawnTile: no battle map can be loaded for '{prefab.name}' - its group's Battle Maps Folder (or the pool's default one) has no scenes, or they aren't in the build's scene list (the pool's Inspector has a button for that).", pool);
            }
        }

        OverworldState.SetEncounterRoll(sceneName, tilePosition, prefab.name, battleMap);
        return true;
    }

    // Every spawn tile in the scene exists by the time any of their Awakes
    // run, whether or not theirs has run yet.
    private int CountSpawnTilesInScene()
    {
        int count = 0;

        foreach (EncounterSpawnTile spawnTile in FindObjectsByType<EncounterSpawnTile>(FindObjectsSortMode.None))
        {
            if (spawnTile.gameObject.scene == gameObject.scene)
            {
                count++;
            }
        }

        return count;
    }

    // Tile.All isn't reliably filled in yet during Awake, so other tiles are
    // looked up directly if this isn't one itself.
    private Tile FindTile()
    {
        if (TryGetComponent(out Tile ownTile))
        {
            return ownTile;
        }

        Tile nearest = null;
        float nearestSqrDistance = float.MaxValue;

        foreach (Tile tile in FindObjectsByType<Tile>(FindObjectsSortMode.None))
        {
            Vector3 flatOffset = tile.transform.position - transform.position;
            flatOffset.y = 0f;
            float sqrDistance = flatOffset.sqrMagnitude;

            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = tile;
            }
        }

        return nearest;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        string[] guids = AssetDatabase.FindAssets("t:EncounterPool");

        if (guids.Length == 1)
        {
            pool = AssetDatabase.LoadAssetAtPath<EncounterPool>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
#endif
}
