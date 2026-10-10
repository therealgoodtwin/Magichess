using UnityEngine;

/// <summary>
/// A piece that can wait off the board in a Pawn Case and be put onto a tile
/// during a battle's setup phase (see BattleSetup): every piece of the
/// player's except the King, and every enemy piece.
/// </summary>
public interface IDeployablePiece
{
    // The piece's own Transform - every piece is a MonoBehaviour, so it has
    // this already.
    Transform transform { get; }

    Tile CurrentTile { get; }

    // Takes the piece off the board, to stand wherever it's then put. It's
    // on no tile, so it can't move and nothing counts it as in the way.
    void LeaveBoard();

    // Stands the piece on a tile, centred, as if it had started there.
    void PlaceOn(Tile tile);
}
