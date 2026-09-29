using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Derives a (column, row) grid from whatever tiles are currently in the
/// scene and answers neighbor queries, including diagonals, without
/// assuming any particular tile size, spacing or orientation.
/// </summary>
public static class TileGrid
{
    private static readonly Dictionary<Vector2Int, Tile> tileByCoord = new();
    private static readonly Dictionary<Tile, Vector2Int> coordByTile = new();
    private static bool isDirty = true;

    public static void MarkDirty()
    {
        isDirty = true;
    }

    public static bool TryGetCoord(Tile tile, out Vector2Int coord)
    {
        Rebuild();
        return coordByTile.TryGetValue(tile, out coord);
    }

    public static Tile GetTileAt(Vector2Int coord)
    {
        Rebuild();
        return tileByCoord.TryGetValue(coord, out Tile tile) ? tile : null;
    }

    public static Tile FindNearest(Vector3 worldPosition)
    {
        Tile nearest = null;
        float nearestSqrDistance = float.MaxValue;

        foreach (Tile tile in Tile.All)
        {
            Vector3 flatOffset = tile.transform.position - worldPosition;
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

    public static List<Tile> GetNeighbors(Tile tile)
    {
        Rebuild();

        List<Tile> neighbors = new();

        if (tile == null || !coordByTile.TryGetValue(tile, out Vector2Int coord))
        {
            return neighbors;
        }

        for (int deltaCol = -1; deltaCol <= 1; deltaCol++)
        {
            for (int deltaRow = -1; deltaRow <= 1; deltaRow++)
            {
                if (deltaCol == 0 && deltaRow == 0)
                {
                    continue;
                }

                TryAddNeighbor(neighbors, new Vector2Int(coord.x + deltaCol, coord.y + deltaRow));
            }
        }

        return neighbors;
    }

    // Straight up/left/down/right only, no diagonals - for pieces that move
    // like a rook one square at a time rather than a king.
    public static List<Tile> GetOrthogonalNeighbors(Tile tile)
    {
        Rebuild();

        List<Tile> neighbors = new();

        if (tile == null || !coordByTile.TryGetValue(tile, out Vector2Int coord))
        {
            return neighbors;
        }

        TryAddNeighbor(neighbors, new Vector2Int(coord.x, coord.y + 1));
        TryAddNeighbor(neighbors, new Vector2Int(coord.x, coord.y - 1));
        TryAddNeighbor(neighbors, new Vector2Int(coord.x - 1, coord.y));
        TryAddNeighbor(neighbors, new Vector2Int(coord.x + 1, coord.y));

        return neighbors;
    }

    // The four diagonal neighbors only, no orthogonals - for pieces that
    // capture diagonally but advance straight, like a chess pawn.
    public static List<Tile> GetDiagonalNeighbors(Tile tile)
    {
        Rebuild();

        List<Tile> neighbors = new();

        if (tile == null || !coordByTile.TryGetValue(tile, out Vector2Int coord))
        {
            return neighbors;
        }

        TryAddNeighbor(neighbors, new Vector2Int(coord.x + 1, coord.y + 1));
        TryAddNeighbor(neighbors, new Vector2Int(coord.x + 1, coord.y - 1));
        TryAddNeighbor(neighbors, new Vector2Int(coord.x - 1, coord.y + 1));
        TryAddNeighbor(neighbors, new Vector2Int(coord.x - 1, coord.y - 1));

        return neighbors;
    }

    private static void TryAddNeighbor(List<Tile> neighbors, Vector2Int coord)
    {
        if (tileByCoord.TryGetValue(coord, out Tile neighbor))
        {
            neighbors.Add(neighbor);
        }
    }

    private static readonly Vector2Int[] CardinalDirections =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1)
    };

    private static void Rebuild()
    {
        if (!isDirty)
        {
            return;
        }

        isDirty = false;
        tileByCoord.Clear();
        coordByTile.Clear();

        IReadOnlyList<Tile> tiles = Tile.All;

        if (tiles.Count == 0)
        {
            return;
        }

        // Positions that differ by far less than one tile's width are the
        // same row/column, just not perfectly aligned (a slightly-off tile
        // or board) - ignoring those deltas keeps them from shrinking the
        // cell size to nothing and breaking every neighbor lookup.
        float minDelta = FindMinTileSpacing(tiles) * 0.5f;

        float cellSizeX = FindSmallestPositiveDelta(tiles, t => t.transform.position.x, minDelta);
        float cellSizeZ = FindSmallestPositiveDelta(tiles, t => t.transform.position.z, minDelta);

        if (cellSizeX <= 0f)
        {
            cellSizeX = 1f;
        }

        if (cellSizeZ <= 0f)
        {
            cellSizeZ = 1f;
        }

        // How close a candidate neighbor's real position has to be to the
        // expected spot, one cell over, to count as actually being there -
        // generous enough for a tile that's close to, but not pixel-perfect
        // on, a regular grid.
        float tolerance = Mathf.Max(cellSizeX, cellSizeZ) * 0.4f;

        // Coordinates are flood-filled outward, tile by tile, rather than
        // computed in one shot from a single global origin - each tile's
        // coordinate comes from whichever neighbor actually discovered it,
        // a short hop away, so nothing accumulates over a long or irregular
        // layout (a corridor connecting two rooms, say). A single global
        // formula measured every tile's distance from one origin tile, so a
        // tiny bit of imprecision multiplied by that distance eventually
        // rounded a far-off tile to the wrong coordinate, breaking its
        // neighbor lookups - despite it looking perfectly normal.
        HashSet<Tile> visited = new();
        Queue<Tile> queue = new();

        foreach (Tile start in tiles)
        {
            if (visited.Contains(start))
            {
                continue;
            }

            // Each disconnected group of tiles gets its own local (0, 0) -
            // fine, since every neighbor query only ever compares a tile
            // against its own already-assigned coordinate.
            visited.Add(start);
            tileByCoord[Vector2Int.zero] = start;
            coordByTile[start] = Vector2Int.zero;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                Tile current = queue.Dequeue();
                Vector2Int currentCoord = coordByTile[current];
                Vector3 currentPosition = current.transform.position;

                foreach (Vector2Int direction in CardinalDirections)
                {
                    Vector3 expectedPosition = currentPosition;
                    expectedPosition.x += direction.x * cellSizeX;
                    expectedPosition.z += direction.y * cellSizeZ;

                    Tile neighbor = FindClosestUnvisited(tiles, visited, expectedPosition, tolerance);

                    if (neighbor == null)
                    {
                        continue;
                    }

                    Vector2Int neighborCoord = currentCoord + direction;
                    visited.Add(neighbor);
                    tileByCoord[neighborCoord] = neighbor;
                    coordByTile[neighbor] = neighborCoord;
                    queue.Enqueue(neighbor);
                }
            }
        }
    }

    private static Tile FindClosestUnvisited(IReadOnlyList<Tile> tiles, HashSet<Tile> visited, Vector3 expectedPosition, float tolerance)
    {
        Tile closest = null;
        float closestSqrDistance = tolerance * tolerance;

        foreach (Tile tile in tiles)
        {
            if (visited.Contains(tile))
            {
                continue;
            }

            Vector3 offset = tile.transform.position - expectedPosition;
            offset.y = 0f;
            float sqrDistance = offset.sqrMagnitude;

            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closest = tile;
            }
        }

        return closest;
    }

    // Smallest ground-plane distance between any two distinct tiles.
    private static float FindMinTileSpacing(IReadOnlyList<Tile> tiles)
    {
        float smallestSqr = float.MaxValue;

        for (int i = 0; i < tiles.Count; i++)
        {
            for (int j = i + 1; j < tiles.Count; j++)
            {
                Vector3 offset = tiles[i].transform.position - tiles[j].transform.position;
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;

                if (sqr > 0.000001f && sqr < smallestSqr)
                {
                    smallestSqr = sqr;
                }
            }
        }

        return smallestSqr == float.MaxValue ? 0f : Mathf.Sqrt(smallestSqr);
    }

    private static float FindSmallestPositiveDelta(IReadOnlyList<Tile> tiles, System.Func<Tile, float> selector, float minDelta)
    {
        float epsilon = Mathf.Max(0.001f, minDelta);
        float smallest = float.MaxValue;

        for (int i = 0; i < tiles.Count; i++)
        {
            for (int j = i + 1; j < tiles.Count; j++)
            {
                float delta = Mathf.Abs(selector(tiles[i]) - selector(tiles[j]));

                if (delta > epsilon && delta < smallest)
                {
                    smallest = delta;
                }
            }
        }

        return smallest == float.MaxValue ? 0f : smallest;
    }
}
