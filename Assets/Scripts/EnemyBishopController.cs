using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An enemy Bishop. Works exactly like an enemy pawn (see
/// EnemyPawnController) - chasing the King, taking its turn, its fire,
/// taking the player's pieces, attacking the King and dying - except for how
/// it moves: diagonally, like a chess bishop, up to Max Range tiles,
/// stopping at the first piece in its way (one of the player's can be
/// taken, but never passed).
/// </summary>
public class EnemyBishopController : EnemyPawnController
{
    [Header("Movement")]
    [Tooltip("Furthest this bishop may travel in one move, in tiles.")]
    [SerializeField] private int maxRange = 10;

    protected override Tile ChooseMove(Tile target)
    {
        return ChooseAlongShortestRoute(target, GetMovesFrom);
    }

    // It takes the way it moves: whatever's first along each diagonal.
    protected override List<Tile> GetAttackTiles()
    {
        return GetMovesFrom(CurrentTile);
    }

    private List<Tile> GetMovesFrom(Tile from)
    {
        return GetSlideMoves(from, DiagonalDirections, maxRange);
    }
}
