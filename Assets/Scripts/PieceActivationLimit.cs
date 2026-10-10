/// <summary>
/// Only the King plus one more piece - a White Pawn or a Rook, whichever
/// activates first - may act each turn. Shared between every piece type that
/// counts against that one extra slot, so activating a Rook locks out the
/// Pawns and vice versa. See PieceSelectionManager.SelectPiece, which reads
/// AnyActivatedThisTurn, and WhitePawnController/RookController, which call
/// MarkActivated when they finish a move and ResetForNewTurn when the turn
/// comes back around.
/// </summary>
public static class PieceActivationLimit
{
    public static bool AnyActivatedThisTurn { get; private set; }

    // Every piece type that shares the limit subscribes to this to switch
    // its own fire off the moment any one of them uses up the shared slot -
    // not just its own type.
    public static event System.Action Activated;

    public static void MarkActivated()
    {
        // Initiative turns give every piece its own turn, so there's no
        // shared slot to use up.
        if (InitiativeTurnManager.IsRunning)
        {
            return;
        }

        AnyActivatedThisTurn = true;
        Activated?.Invoke();
    }

    public static void ResetForNewTurn()
    {
        AnyActivatedThisTurn = false;
    }
}
