using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Put on a tile, or on an object standing over one (a rock, a wall, a
/// pillar). That tile is closed to every piece, the player's and the enemy's
/// alike: nothing can move onto it or slide through it, so it never lights
/// up as somewhere to move. A Knight - either side's - can't land on it, but
/// still jumps over it, as it does over everything else.
///
/// TileGrid leaves blocked tiles out of every answer about where a piece can
/// go (see TileGrid.IsBlocked), which is all it takes.
/// </summary>
public class Blocker : MonoBehaviour
{
    public static readonly List<Blocker> All = new();

    // The tile this is on, or stands over.
    public Tile FindTile()
    {
        return TryGetComponent(out Tile tile) ? tile : TileGrid.FindNearest(transform.position);
    }

    private void OnEnable()
    {
        All.Add(this);
        TileGrid.MarkDirty();
    }

    private void OnDisable()
    {
        All.Remove(this);
        TileGrid.MarkDirty();
    }
}
