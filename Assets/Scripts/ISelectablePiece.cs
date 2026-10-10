using UnityEngine;

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

    // Turn order under an InitiativeTurnManager - the higher, the earlier.
    int Initiative { get; }

    // Whether the last Select() found anywhere this piece can move.
    bool HasReachableTiles { get; }

    // Called by InitiativeTurnManager once this piece's turn is over, moved
    // or passed - its fire, if it has one, goes out until the round ends.
    void EndInitiativeTurn();

    // How much of a threat this piece is to the enemy, and how close, in
    // tiles, an enemy has to be for that to matter: an enemy within Threat
    // Range goes for this piece instead of the King if Threat is at least
    // that enemy's own Threat Response. The King has neither - he's what the
    // enemy goes for when nothing draws it away.
    int Threat { get; }
    int ThreatRange { get; }

    // HP dealt to an enemy when this piece moves onto it.
    int CaptureDamage { get; }

    // An enemy's hit landing on this piece. Returns true if it died.
    bool TakeHit(int amount, Vector3 sourcePosition);

    // This piece moved onto an enemy that survived the hit: it slides back
    // to the tile it came from. Its turn is still used up.
    void BounceBack();

    void Select();
    void Deselect();
    bool TryMoveTo(Tile tile);
}
