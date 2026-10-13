using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Put on an Overworld tile, or on an object standing over one. When the
/// King steps onto that tile, every tile in the groups given here sinks out
/// of play - it can't be walked onto again - and any encounter standing on
/// one of them is removed. Made for where the path forks: stepping onto one
/// branch closes off the other.
///
/// The Overworld Controller does the sinking and remembers it (see
/// OverworldController.SinkTiles), so the tiles and their encounters are
/// still gone when the player comes back from an encounter.
/// </summary>
public class TileGroupSinker : MonoBehaviour
{
    [Tooltip("The groups of tiles that sink when the King steps onto this tile. Drag in each group's parent object (Left Side Path (1), say) - every tile under it sinks. A single tile can be dragged in too.")]
    [SerializeField] private Transform[] tileGroups = new Transform[0];

    public static readonly List<TileGroupSinker> All = new();

    private Tile tile;

    // The tile this is on, or stands over, found once tiles exist.
    public Tile Tile
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
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void Start()
    {
        if (FindFirstObjectByType<OverworldController>() == null)
        {
            Debug.LogWarning($"TileGroupSinker: '{name}' needs an Overworld Controller in the scene to sink its tiles - without one it does nothing.", this);
        }
    }

    // Every tile in this sinker's groups.
    public List<Tile> FindTiles()
    {
        List<Tile> tiles = new();

        foreach (Transform group in tileGroups)
        {
            if (group == null)
            {
                continue;
            }

            foreach (Tile groupTile in group.GetComponentsInChildren<Tile>(true))
            {
                if (!tiles.Contains(groupTile))
                {
                    tiles.Add(groupTile);
                }
            }
        }

        return tiles;
    }
}
