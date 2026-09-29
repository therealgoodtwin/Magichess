using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns which player-controlled piece - the King, or a selected White Pawn -
/// currently receives clicks. The King is selected by default. Clicking a
/// piece's model or the tile it's standing on selects it and shows its
/// reachable tiles; clicking one of those reachable tiles moves whichever
/// piece is currently selected. Clicking the King always hands control back
/// to it.
/// </summary>
public class PieceSelectionManager : MonoBehaviour
{
    private PlayerController king;
    private ISelectablePiece current;

    private void Start()
    {
        king = FindFirstObjectByType<PlayerController>();
        current = king;
    }

    private void Update()
    {
        if (TurnManager.IsEnemyTurn || current == null || current.IsMoving || PawnSummonController.IsPlacing || ArsenalTeleporter.IsTeleporting)
        {
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        // A click on a menu button or window isn't a click on the board behind it.
        if (SettingsWindowController.IsPointerOverUI(mouse.position.ReadValue()))
        {
            return;
        }

        Camera cam = Camera.main;

        if (cam == null)
        {
            return;
        }

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            return;
        }

        ISelectablePiece clickedPiece = IdentifyClickedPiece(hit.collider);

        if (clickedPiece != null)
        {
            SelectPiece(clickedPiece);
            return;
        }

        Tile tile = hit.collider.GetComponent<Tile>();

        if (tile == null)
        {
            return;
        }

        // Clicking the tile a piece stands on selects it, same as clicking
        // the piece's model directly - it just didn't win the raycast.
        ISelectablePiece pieceOnTile = FindPieceOnTile(tile);

        if (pieceOnTile != null)
        {
            SelectPiece(pieceOnTile);
            return;
        }

        current.TryMoveTo(tile);
    }

    private ISelectablePiece IdentifyClickedPiece(Collider hitCollider)
    {
        WhitePawnController pawn = hitCollider.GetComponentInParent<WhitePawnController>();

        if (pawn != null)
        {
            return pawn;
        }

        RookController rook = hitCollider.GetComponentInParent<RookController>();

        if (rook != null)
        {
            return rook;
        }

        KnightController knight = hitCollider.GetComponentInParent<KnightController>();

        if (knight != null)
        {
            return knight;
        }

        BishopController bishop = hitCollider.GetComponentInParent<BishopController>();

        if (bishop != null)
        {
            return bishop;
        }

        if (king != null && hitCollider.GetComponentInParent<PlayerController>() == king)
        {
            return king;
        }

        return null;
    }

    private ISelectablePiece FindPieceOnTile(Tile tile)
    {
        if (king != null && king.CurrentTile == tile)
        {
            return king;
        }

        foreach (WhitePawnController pawn in WhitePawnController.All)
        {
            if (pawn.CurrentTile == tile)
            {
                return pawn;
            }
        }

        foreach (RookController rook in RookController.All)
        {
            if (rook.CurrentTile == tile)
            {
                return rook;
            }
        }

        foreach (KnightController knight in KnightController.All)
        {
            if (knight.CurrentTile == tile)
            {
                return knight;
            }
        }

        foreach (BishopController bishop in BishopController.All)
        {
            if (bishop.CurrentTile == tile)
            {
                return bishop;
            }
        }

        return null;
    }

    private void SelectPiece(ISelectablePiece piece)
    {
        // Already activated this turn (clicked on and moved) - locked out
        // until TurnManager.EnemyTurnEnded resets it, so the click is
        // simply ignored rather than reselecting it.
        if (piece.HasBeenActivated)
        {
            return;
        }

        if (!TurnManager.TurnsEnabled && piece != king)
        {
            return;
        }

        // Only the King plus one more piece (a Pawn, Rook or Knight) may act
        // each turn, no matter how many exist - once any one of them has
        // moved, every other not-yet-activated one is locked out too, until
        // the turn resets.
        if ((piece is WhitePawnController || piece is RookController || piece is KnightController || piece is BishopController) &&
            PieceActivationLimit.AnyActivatedThisTurn)
        {
            return;
        }

        // Re-select unconditionally, even if this piece is already
        // "current" - it may have been deselected (activated, then reset)
        // without current ever pointing elsewhere in between, in which case
        // it still needs its highlights switched back on.
        if (current != piece)
        {
            current?.Deselect();
            current = piece;
        }

        current.Select();
    }
}
