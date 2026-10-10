using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One slot in a Pawn Case: a piece waiting to be deployed stands on it,
/// centred. Its Side says whose pieces it takes - the player's (any but the
/// King) or the enemy's - and it takes one at a time. As a battle starts,
/// every piece is stood on a free holder of its own side (see Fill), and
/// BattleSetup empties the holders again as the pieces are deployed onto the
/// board.
///
/// A Pawn Case sets Side on all of its holders at once. Change it on one
/// holder afterwards and that holder keeps its own.
/// </summary>
[DisallowMultipleComponent]
public class PawnHolder : MonoBehaviour
{
    [Tooltip("Whose pieces wait on this holder: the player's (all but the King) or the enemy's. Its Pawn Case sets this for all of its holders at once - change it here for just this one.")]
    [SerializeField] private PawnCase.Side side;

    public PawnCase.Side Side => side;

    public static readonly List<PawnHolder> All = new();

    // The piece standing on this holder, or null.
    public IDeployablePiece Occupant { get; private set; }

    // A piece that's been destroyed doesn't hold its place.
    public bool IsFree => Occupant == null || (Occupant is Object occupant && occupant == null);

    // A second Pawn Holder script on the same object would put a second
    // piece on the same spot, so only the first one counts.
    public bool IsDuplicate => GetComponent<PawnHolder>() != this;

    // Where the base of a piece goes: the middle of this holder's top face,
    // or just its position if it has nothing drawn.
    public Vector3 StandingPoint => TryGetComponent(out Renderer holderRenderer)
        ? PiecePivotUtility.TopCenter(holderRenderer.bounds)
        : transform.position;

    // The holder a piece is waiting on, or null if it isn't on one.
    public static PawnHolder FindHolding(IDeployablePiece piece)
    {
        foreach (PawnHolder holder in All)
        {
            if (holder.Occupant == piece)
            {
                return holder;
            }
        }

        return null;
    }

    // Every holder in the scene, filled in a steady order: case by case,
    // from one end of each to the other.
    public static void FillAll()
    {
        List<PawnHolder> holders = new(All);
        holders.Sort(CompareOrder);
        Fill(holders);
    }

    // Takes every piece that isn't waiting on a holder yet off the board and
    // stands it on a free holder of its own side, using the holders in the
    // order given. A piece with no holder left for it stays where it is.
    public static void Fill(IEnumerable<PawnHolder> holders)
    {
        Queue<IDeployablePiece> playerPieces = null;
        Queue<IDeployablePiece> enemyPieces = null;

        foreach (PawnHolder holder in holders)
        {
            if (holder == null || holder.IsDuplicate || !holder.IsFree)
            {
                continue;
            }

            Queue<IDeployablePiece> pieces;

            if (holder.side == PawnCase.Side.Enemy)
            {
                pieces = enemyPieces ??= FindPiecesWithoutHolder(PawnCase.Side.Enemy);
            }
            else
            {
                pieces = playerPieces ??= FindPiecesWithoutHolder(PawnCase.Side.Player);
            }

            if (pieces.Count > 0)
            {
                holder.Seat(pieces.Dequeue());
            }
        }
    }

    // Holders of the same case together, each case's from one end to the
    // other.
    public static int CompareOrder(PawnHolder a, PawnHolder b)
    {
        Transform parentA = a.transform.parent;
        Transform parentB = b.transform.parent;

        if (parentA != parentB)
        {
            int idA = parentA != null ? parentA.GetInstanceID() : 0;
            int idB = parentB != null ? parentB.GetInstanceID() : 0;
            return idA.CompareTo(idB);
        }

        Vector3 positionA = a.transform.localPosition;
        Vector3 positionB = b.transform.localPosition;

        if (Mathf.Abs(positionA.x - positionB.x) > 0.01f)
        {
            return positionA.x.CompareTo(positionB.x);
        }

        return positionA.z.CompareTo(positionB.z);
    }

    private static Queue<IDeployablePiece> FindPiecesWithoutHolder(PawnCase.Side side)
    {
        Queue<IDeployablePiece> pieces = new();

        foreach (IDeployablePiece piece in FindPieces(side))
        {
            if (FindHolding(piece) == null)
            {
                pieces.Enqueue(piece);
            }
        }

        return pieces;
    }

    // Every piece of a side in the scene - the King never waits on a
    // holder.
    private static List<IDeployablePiece> FindPieces(PawnCase.Side side)
    {
        List<IDeployablePiece> pieces = new();

        if (side == PawnCase.Side.Enemy)
        {
            pieces.AddRange(EnemyPawnController.All);
            return pieces;
        }

        pieces.AddRange(WhitePawnController.All);
        pieces.AddRange(RookController.All);
        pieces.AddRange(KnightController.All);
        pieces.AddRange(BishopController.All);
        return pieces;
    }

    private void OnEnable()
    {
        if (IsDuplicate)
        {
            Debug.LogWarning($"PawnHolder: '{name}' has the Pawn Holder script on it more than once - only the first one is used. Remove the extra.", this);
            return;
        }

        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    public void SetSide(PawnCase.Side newSide)
    {
        side = newSide;
    }

    // Takes the piece off the board and stands it on this holder. False if
    // another piece is already here.
    public bool Seat(IDeployablePiece piece)
    {
        if (piece == null || !IsFree)
        {
            return false;
        }

        piece.LeaveBoard();
        PiecePivotUtility.StandOn(piece.transform, StandingPoint);
        Occupant = piece;
        return true;
    }

    // Called as its piece leaves for the board.
    public void Release()
    {
        Occupant = null;
    }

    // Whether a ray (a click) passes through this holder.
    public bool IsHitBy(Ray ray, out float distance)
    {
        distance = 0f;
        return TryGetComponent(out Renderer holderRenderer) && holderRenderer.bounds.IntersectRay(ray, out distance);
    }

#if UNITY_EDITOR
    // Runs when this script is first added to an object in the Editor: it
    // starts out on the side of the Pawn Case it's in, or, outside any case,
    // as the enemy's if "Enemy" is in its name.
    private void Reset()
    {
        PawnCase pawnCase = GetComponentInParent<PawnCase>(true);

        if (pawnCase != null)
        {
            side = pawnCase.CaseSide;
        }
        else
        {
            side = name.Contains("Enemy") ? PawnCase.Side.Enemy : PawnCase.Side.Player;
        }
    }
#endif
}
