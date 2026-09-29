/// <summary>
/// A player-controlled piece that PieceSelectionManager can select and
/// move. Implemented by both the King (PlayerController) and White Pawns
/// (WhitePawnController).
/// </summary>
public interface ISelectablePiece
{
    Tile CurrentTile { get; }
    bool IsMoving { get; }

    // True from the moment this piece completes a move until the turn
    // resets - PieceSelectionManager refuses to (re)select an activated
    // piece, so it can't be clicked and moved again mid-turn.
    bool HasBeenActivated { get; }

    void Select();
    void Deselect();
    bool TryMoveTo(Tile tile);
}
