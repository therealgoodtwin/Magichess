using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Put on a tile, or on an object standing over one (like a clump of tall
/// grass). Whatever stands on that tile is hidden from the other side.
///
/// An enemy piece there can't be seen by the player - its model, its fires
/// and their light are all switched off - and neither can the tile it's
/// about to move to. It goes out of sight as it arrives, and comes back the
/// moment it sets off again.
///
/// One of the player's pieces there stays on screen, but the enemy can't see
/// it: no enemy heads for it or takes it, and its tile is simply closed to
/// them. With the King in Grass the enemy has nothing to chase, and stays
/// where it is until he steps out.
///
/// Both halves are carried out by EnemyPawnController (see UpdateHiding and
/// PlanNextMove there).
/// </summary>
public class Grass : MonoBehaviour
{
    private static readonly List<Grass> all = new();

    private Tile tile;

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

    // Whether a piece standing on the given tile is hidden from the other
    // side.
    public static bool Hides(Tile tile)
    {
        if (tile == null)
        {
            return false;
        }

        foreach (Grass grass in all)
        {
            if (grass.Tile == tile)
            {
                return true;
            }
        }

        return false;
    }

    private void OnEnable()
    {
        all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }
}
