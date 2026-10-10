using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns enemy pawns in waves, on tiles marked IsSpawnTile (the Is Spawn
/// Tile checkbox on each Tile), replacing the old PawnSpawner.
///
/// Wave 1 spawns immediately. After that, the next wave spawns
/// automatically once turnsPerWave turns have passed - or sooner,
/// gracePeriodTurns turns after every enemy on the board is dead, if that
/// happens first. Once the last configured wave has spawned, nothing further
/// happens.
/// </summary>
public class WaveManager : MonoBehaviour
{
    [System.Serializable]
    private class EnemySpawnEntry
    {
        [Tooltip("Enemy prefab to spawn (e.g. Pawn (Black)).")]
        public GameObject enemyPrefab;

        [Tooltip("How many of this type spawn as part of this wave.")]
        public int count = 1;
    }

    [System.Serializable]
    private class Wave
    {
        [Tooltip("Every enemy type (and how many of each) that spawns as part of this wave.")]
        public List<EnemySpawnEntry> enemies = new();
    }

    [Tooltip("One entry per wave, spawned in order - the list length is the total number of waves.")]
    [SerializeField] private List<Wave> waves = new();

    [Tooltip("Turns after a wave spawns before the next one spawns automatically, regardless of whether this wave's enemies are still alive.")]
    [SerializeField] private int turnsPerWave = 6;

    [Tooltip("Turns to wait, after every enemy on the board is dead, before spawning the next wave - only relevant if that happens before turnsPerWave elapses.")]
    [SerializeField] private int gracePeriodTurns = 2;

    // 1-based, for display - 0 until the first wave actually spawns.
    public static int CurrentWaveNumber { get; private set; }

    // Fired with the 1-based wave number every time a new wave spawns.
    public static event System.Action<int> WaveStarted;

    private int waveIndex = -1;
    private int turnsSinceWaveStart;
    private bool isWaveCleared;
    private int turnsSinceCleared;

    private void OnEnable()
    {
        TurnManager.EnemyTurnEnded += HandleTurnEnded;
    }

    private void OnDisable()
    {
        TurnManager.EnemyTurnEnded -= HandleTurnEnded;
    }

    private void Start()
    {
        SpawnNextWave();
    }

    private void HandleTurnEnded()
    {
        if (waveIndex >= waves.Count)
        {
            return;
        }

        turnsSinceWaveStart++;

        if (!isWaveCleared && EnemyPawnController.All.Count == 0)
        {
            isWaveCleared = true;
            turnsSinceCleared = 0;
        }
        else if (isWaveCleared)
        {
            turnsSinceCleared++;
        }

        bool hardTimeout = turnsSinceWaveStart >= turnsPerWave;
        bool graceExpired = isWaveCleared && turnsSinceCleared >= gracePeriodTurns;

        if (hardTimeout || graceExpired)
        {
            SpawnNextWave();
        }
    }

    private void SpawnNextWave()
    {
        waveIndex++;

        if (waveIndex >= waves.Count)
        {
            return;
        }

        turnsSinceWaveStart = 0;
        isWaveCleared = false;
        turnsSinceCleared = 0;

        CurrentWaveNumber = waveIndex + 1;
        WaveStarted?.Invoke(CurrentWaveNumber);

        List<Tile> candidates = GetCandidateTiles();

        foreach (EnemySpawnEntry entry in waves[waveIndex].enemies)
        {
            if (entry.enemyPrefab == null)
            {
                Debug.LogWarning("WaveManager: a wave entry has no Enemy Prefab assigned - skipping it.", this);
                continue;
            }

            for (int i = 0; i < entry.count && candidates.Count > 0; i++)
            {
                int index = Random.Range(0, candidates.Count);
                Tile tile = candidates[index];
                candidates.RemoveAt(index);

                SpawnPawnOn(entry.enemyPrefab, tile);
            }
        }
    }

    private static List<Tile> GetCandidateTiles()
    {
        List<Tile> candidates = new();

        foreach (Tile tile in Tile.All)
        {
            if (tile.IsSpawnTile && !IsOccupied(tile))
            {
                candidates.Add(tile);
            }
        }

        return candidates;
    }

    private static bool IsOccupied(Tile tile)
    {
        foreach (EnemyPawnController pawn in EnemyPawnController.All)
        {
            if (pawn.CurrentTile == tile)
            {
                return true;
            }
        }

        return false;
    }

    private static void SpawnPawnOn(GameObject enemyPrefab, Tile tile)
    {
        // Use the tile's resting position, not its live (possibly
        // currently-lifted, if it's highlighted) position - otherwise a
        // pawn spawned onto a momentarily-elevated tile would permanently
        // bake that lift into its own resting height.
        Vector3 position = tile.BaseWorldPosition + GetPawnPivotOffset(enemyPrefab);
        Instantiate(enemyPrefab, position, enemyPrefab.transform.rotation);
    }

    // Matches the pivot-offset trick every piece in this game uses: each
    // enemy prefab's own authored position (relative to whichever tile it
    // was originally placed on) tells us how far off a tile's exact center
    // that prefab's model needs to sit to look right, so newly spawned
    // pawns line up visually the same way a hand-placed one would. The
    // height component specifically comes from the prefab's own mesh
    // instead (see PiecePivotUtility) - the prefab's authored position
    // might be nowhere near any tile in whichever scene is currently
    // spawning it, which would otherwise put a wave-spawned enemy floating
    // above (or sunk into) the board.
    private static Vector3 GetPawnPivotOffset(GameObject enemyPrefab)
    {
        Tile referenceTile = TileGrid.FindNearest(enemyPrefab.transform.position);

        Vector3 offset = referenceTile != null
            ? enemyPrefab.transform.position - referenceTile.BaseWorldPosition
            : Vector3.zero;

        offset.y = PiecePivotUtility.GetPivotHeightAboveTile(enemyPrefab);
        return offset;
    }
}
