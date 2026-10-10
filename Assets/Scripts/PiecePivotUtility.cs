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

    // Moves a piece already in the scene so its model sits centred on the
    // tile, resting on the tile's top face - going by what's actually drawn
    // rather than by the piece's pivot, which often isn't at the model's
    // centre (the King's isn't) or anywhere near it (encounter prefabs keep
    // whatever position they were built at). A piece with a mesh of its own
    // is centred by that mesh, so anything decorating it keeps its place
    // around it. One that only holds other meshes is centred by all of them.
    public static void CenterOnTile(Transform piece, Tile tile)
    {
        if (tile != null && tile.TryGetComponent(out Renderer tileRenderer))
        {
            StandOn(piece, TopCenter(tileRenderer.bounds));
        }
    }

    // The middle of a surface's top face - where the base of a piece goes
    // for it to stand on that surface.
    public static Vector3 TopCenter(Bounds surface)
    {
        return new Vector3(surface.center.x, surface.max.y, surface.center.z);
    }

    // Moves a piece so its model stands on a point: centred over it, with
    // its lowest part resting on it.
    public static void StandOn(Transform piece, Vector3 point)
    {
        if (TryGetBaseOffset(piece, out Vector3 baseOffset))
        {
            piece.position = point + baseOffset;
        }
    }

    // How far a piece's own position is from the middle of its model's
    // base. Add it to a point to get where the piece has to be to stand
    // there.
    public static bool TryGetBaseOffset(Transform piece, out Vector3 baseOffset)
    {
        if (piece == null || !TryGetModelBounds(piece, out Bounds bounds))
        {
            baseOffset = Vector3.zero;
            return false;
        }

        baseOffset = piece.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        return true;
    }

    public static bool TryGetModelBounds(Transform piece, out Bounds bounds)
    {
        if (piece.TryGetComponent(out MeshRenderer ownRenderer))
        {
            bounds = ownRenderer.bounds;
            return true;
        }

        bounds = default;
        bool found = false;

        foreach (MeshRenderer meshRenderer in piece.GetComponentsInChildren<MeshRenderer>())
        {
            if (found)
            {
                bounds.Encapsulate(meshRenderer.bounds);
            }
            else
            {
                bounds = meshRenderer.bounds;
                found = true;
            }
        }

        return found;
    }
}
