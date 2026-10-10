using UnityEngine;

/// <summary>
/// Who's standing where - shared lookups so every piece agrees on what
/// counts as blocked, and who's being attacked.
/// </summary>
public static class BoardPieces
{
    // Whether any of the player's pieces - the King, a White Pawn, Rook,
    // Knight or Bishop - other than except is standing on the tile. Pieces
    // that have died drop out of their lists, so they never count.
    public static bool IsPlayerPieceOn(Tile tile, Object except = null)
    {
        return FindPlayerPieceOn(tile, except) != null;
    }

    // The player's piece standing on the tile, other than except, or null.
    public static ISelectablePiece FindPlayerPieceOn(Tile tile, Object except = null)
    {
        if (tile == null)
        {
            return null;
        }

        foreach (PlayerController king in PlayerController.All)
        {
            if (king != except && king.CurrentTile == tile)
            {
                return king;
            }
        }

        foreach (WhitePawnController pawn in WhitePawnController.All)
        {
            if (pawn != except && pawn.CurrentTile == tile)
            {
                return pawn;
            }
        }

        foreach (RookController rook in RookController.All)
        {
            if (rook != except && rook.CurrentTile == tile)
            {
                return rook;
            }
        }

        foreach (KnightController knight in KnightController.All)
        {
            if (knight != except && knight.CurrentTile == tile)
            {
                return knight;
            }
        }

        foreach (BishopController bishop in BishopController.All)
        {
            if (bishop != except && bishop.CurrentTile == tile)
            {
                return bishop;
            }
        }

        return null;
    }

    // The piece of the player's that draws an enemy standing on enemyTile
    // away from the King, or null if none does. A piece draws it if its
    // Threat is at least the enemy's Threat Response and the enemy is within
    // that piece's Threat Range - and the enemy can see it, so not one
    // standing in Grass. With several, the biggest Threat wins, then the
    // nearest. The King never counts: he's who it goes for otherwise.
    public static ISelectablePiece FindThreatTo(Tile enemyTile, int threatResponse)
    {
        ISelectablePiece best = null;
        int bestDistance = int.MaxValue;

        foreach (WhitePawnController pawn in WhitePawnController.All)
        {
            ConsiderThreat(pawn, enemyTile, threatResponse, ref best, ref bestDistance);
        }

        foreach (RookController rook in RookController.All)
        {
            ConsiderThreat(rook, enemyTile, threatResponse, ref best, ref bestDistance);
        }

        foreach (KnightController knight in KnightController.All)
        {
            ConsiderThreat(knight, enemyTile, threatResponse, ref best, ref bestDistance);
        }

        foreach (BishopController bishop in BishopController.All)
        {
            ConsiderThreat(bishop, enemyTile, threatResponse, ref best, ref bestDistance);
        }

        return best;
    }

    private static void ConsiderThreat(ISelectablePiece piece, Tile enemyTile, int threatResponse, ref ISelectablePiece best, ref int bestDistance)
    {
        Tile tile = piece.CurrentTile;

        if (tile == null || piece.Threat < threatResponse || Grass.Hides(tile))
        {
            return;
        }

        int distance = TileGrid.GetDistance(enemyTile, tile);

        if (distance > piece.ThreatRange)
        {
            return;
        }

        if (best == null || piece.Threat > best.Threat || (piece.Threat == best.Threat && distance < bestDistance))
        {
            best = piece;
            bestDistance = distance;
        }
    }

    // Whether one of the player's pieces is standing on the tile out of the
    // enemy's sight, in Grass. The enemy neither heads for it nor takes it,
    // and can't step onto its tile.
    public static bool IsHiddenPlayerPieceOn(Tile tile)
    {
        return Grass.Hides(tile) && IsPlayerPieceOn(tile);
    }

    // Whether an enemy piece is standing on the tile.
    public static bool IsEnemyOn(Tile tile)
    {
        if (tile == null)
        {
            return false;
        }

        foreach (EnemyPawnController enemy in EnemyPawnController.All)
        {
            if (enemy.CurrentTile == tile)
            {
                return true;
            }
        }

        return false;
    }

    // Whether an enemy piece the player can see is standing on the tile. One
    // in Grass can't be seen, so the player's pieces work out where they can
    // move as if it weren't there - and find it by running into it.
    public static bool IsVisibleEnemyOn(Tile tile)
    {
        return !Grass.Hides(tile) && IsEnemyOn(tile);
    }

    // The first tile along the straight or diagonal line from one tile to
    // another - neither end counted - where an enemy stands hidden in Grass,
    // or null. A piece sliding along that line runs into it there.
    public static Tile FindHiddenEnemyTileBetween(Tile from, Tile to)
    {
        if (from == null || to == null ||
            !TileGrid.TryGetCoord(from, out Vector2Int fromCoord) ||
            !TileGrid.TryGetCoord(to, out Vector2Int toCoord))
        {
            return null;
        }

        Vector2Int delta = toCoord - fromCoord;

        // Not a line a sliding piece could be on.
        if (delta.x != 0 && delta.y != 0 && Mathf.Abs(delta.x) != Mathf.Abs(delta.y))
        {
            return null;
        }

        Vector2Int step = new(System.Math.Sign(delta.x), System.Math.Sign(delta.y));

        for (Vector2Int coord = fromCoord + step; coord != toCoord; coord += step)
        {
            Tile tile = TileGrid.GetTileAt(coord);

            if (tile != null && Grass.Hides(tile) && IsEnemyOn(tile))
            {
                return tile;
            }
        }

        return null;
    }

    // Whether any of the player's pieces is still sliding - e.g. back from
    // an attack that didn't kill.
    public static bool AnyPlayerPieceMoving()
    {
        foreach (PlayerController king in PlayerController.All)
        {
            if (king.IsMoving)
            {
                return true;
            }
        }

        foreach (WhitePawnController pawn in WhitePawnController.All)
        {
            if (pawn.IsMoving)
            {
                return true;
            }
        }

        foreach (RookController rook in RookController.All)
        {
            if (rook.IsMoving)
            {
                return true;
            }
        }

        foreach (KnightController knight in KnightController.All)
        {
            if (knight.IsMoving)
            {
                return true;
            }
        }

        foreach (BishopController bishop in BishopController.All)
        {
            if (bishop.IsMoving)
            {
                return true;
            }
        }

        return false;
    }
}
