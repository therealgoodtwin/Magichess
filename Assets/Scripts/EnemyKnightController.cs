using System.Collections.Generic;

/// <summary>
/// An enemy Knight. Works exactly like an enemy pawn (see
/// EnemyPawnController) - chasing the King, taking its turn, its fire,
/// taking the player's pieces, attacking the King and dying - except for how
/// it moves: in an L/Γ shape, like a chess knight, two tiles one way and one
/// to the side, jumping over anything in between. It can land anywhere no
/// other enemy is standing.
/// </summary>
public class EnemyKnightController : EnemyPawnController
{
    protected override Tile ChooseMove(Tile target)
    {
        return ChooseAlongShortestRoute(target, GetMovesFrom);
    }

    // It takes the way it moves: on any tile it can jump to.
    protected override List<Tile> GetAttackTiles()
    {
        return GetMovesFrom(CurrentTile);
    }

    private List<Tile> GetMovesFrom(Tile from)
    {
        return GetJumpMoves(from, KnightJumps);
    }
}
