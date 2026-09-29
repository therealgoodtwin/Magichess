using UnityEngine;

/// <summary>
/// Works out how far above a tile's surface a piece prefab's own pivot
/// needs to sit for its mesh to rest exactly on top of the tile - derived
/// entirely from the prefab's own mesh bounds, not from wherever the prefab
/// asset happens to be positioned.
///
/// A prefab's own Transform holds whatever absolute position it was
/// originally authored at - e.g. a specific board square in one particular
/// scene - which has nothing to do with any tile in a different scene that
/// reuses the same prefab. Measuring against that stale position (as
/// PawnSummonController and WaveManager both used to, via
/// TileGrid.FindNearest(prefab.transform.position)) finds whichever tile
/// happens to be nearest that unrelated point, however far away that
/// actually is - producing a wildly wrong height in a scene whose board
/// sits somewhere else entirely (e.g. a summoned pawn floating high above
/// the tiles in a scene built far from where the prefab was first placed).
/// </summary>
public static class PiecePivotUtility
{
    public static float GetPivotHeightAboveTile(GameObject prefab)
    {
        if (prefab == null)
        {
            return 0f;
        }

        MeshFilter meshFilter = prefab.GetComponentInChildren<MeshFilter>();

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return 0f;
        }

        // The mesh's lowest local point, scaled the same way the prefab
        // itself is - assumes neither the prefab nor whichever child holds
        // the mesh is tilted away from upright, true for every piece in
        // this game.
        float lowestLocalY = meshFilter.sharedMesh.bounds.min.y;
        float heightBelowMeshPivot = -lowestLocalY * meshFilter.transform.lossyScale.y;

        // If the mesh sits on a child rather than the prefab's own root,
        // fold in that child's own offset from the root too.
        float childOffsetY = meshFilter.transform.position.y - prefab.transform.position.y;

        return heightBelowMeshPivot + childOffsetY;
    }
}
