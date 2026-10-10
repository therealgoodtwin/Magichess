using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Initiative turns, for battle scenes. Every piece - the King, White Pawns,
/// Rooks, Knights, Bishops and enemy pawns - has an Initiative stat, and
/// each round they act one at a time, highest Initiative first. Every piece
/// starts locked. A player piece whose turn comes up is selected
/// automatically and waits for the player to move it (or pass, with the
/// Pass Key); an enemy pawn whose turn comes up lights up where it's going
/// and moves on its own, towards the King. Once every piece has had its
/// turn the round ends and the next one starts again from the top.
///
/// Ties: player pieces always go before enemy pawns on the same Initiative.
/// Pieces of the same team on the same Initiative go in an order drawn at
/// random when the scene starts (or when a piece first appears, for one
/// summoned or spawned later), which then stays the same every round. A
/// piece that appears mid-round joins from the next round.
///
/// Put this on one object in a battle scene. Scenes without one keep the
/// old End Turn system.
/// </summary>
// Starts after every piece's own Start, so they all know their tiles.
[DefaultExecutionOrder(100)]
public class InitiativeTurnManager : MonoBehaviour
{
    [Tooltip("Seconds an enemy pawn shows the tile it's about to move to before moving, so the player can follow what's happening.")]
    [SerializeField] private float enemyTurnDelay = 0.3f;

    [Tooltip("Lets the player piece whose turn it is pass without moving. A piece with nowhere to go passes on its own.")]
    [SerializeField] private Key passKey = Key.Space;

    [Tooltip("Writes each round's turn order to the Console - handy for checking Initiative values.")]
    [SerializeField] private bool logTurnOrder;

    private static InitiativeTurnManager instance;

    public static bool IsRunning => instance != null;

    // The player piece whose turn it is, or null while an enemy pawn's turn
    // is playing out (or between turns).
    public static ISelectablePiece ActivePlayerPiece { get; private set; }

    private struct Entry
    {
        public MonoBehaviour Piece;
        public bool IsPlayer;
        public int Initiative;
        public float TieBreak;
    }

    // Each piece's place among same-team pieces on the same Initiative,
    // drawn the first time it's seen and kept for the rest of the scene.
    private readonly Dictionary<MonoBehaviour, float> tieBreaks = new();

    private PieceSelectionManager selectionManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        ActivePlayerPiece = null;
    }

    private void Awake()
    {
        instance = this;

        // A Player Spawner may add one too - whichever runs first, the other
        // finds it.
        selectionManager = FindFirstObjectByType<PieceSelectionManager>();

        if (selectionManager == null)
        {
            selectionManager = gameObject.AddComponent<PieceSelectionManager>();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            ActivePlayerPiece = null;
        }
    }

    private void Start()
    {
        StartCoroutine(RunRounds());
    }

    private IEnumerator RunRounds()
    {
        // One frame, so pieces spawned during Awake have had their Start too.
        yield return null;

        // Pieces may have lit up their moves by themselves as the scene
        // started - all of them start locked.
        foreach (Entry entry in CollectPieces())
        {
            if (entry.IsPlayer)
            {
                ((ISelectablePiece)entry.Piece).Deselect();
            }
        }

        // Nothing moves until the scene's reveal has finished, and both
        // sides have deployed their pieces (see BattleSetup).
        while (ScreenWipe.IsBusy || BattleSetup.IsActive)
        {
            yield return null;
        }

        while (true)
        {
            List<Entry> order = CollectPieces();
            order.Sort(CompareTurnOrder);

            if (!order.Exists(entry => entry.IsPlayer))
            {
                // Nothing left on the player's side to play for.
                yield break;
            }

            if (logTurnOrder)
            {
                LogOrder(order);
            }

            for (int i = 0; i < order.Count; i++)
            {
                Entry entry = order[i];

                yield return WaitUntilSettled();

                if (!IsInPlay(entry.Piece))
                {
                    continue;
                }

                ShowUpcomingPlans(order, i);

                if (entry.IsPlayer)
                {
                    yield return PlayerTurn((ISelectablePiece)entry.Piece);
                }
                else
                {
                    yield return EnemyTurn((EnemyPawnController)entry.Piece);
                }
            }

            yield return WaitUntilSettled();
            TurnManager.EndRound();

            // At least a frame per round, however quickly it went.
            yield return null;
        }
    }

    // Only two enemy pawns show the tile they're about to move to: the one
    // whose turn it is (if this turn is an enemy's) and the next one due to
    // act after it. Everyone else's plan stays hidden.
    private void ShowUpcomingPlans(List<Entry> order, int index)
    {
        EnemyPawnController current = order[index].IsPlayer ? null : (EnemyPawnController)order[index].Piece;
        EnemyPawnController next = FindNextEnemy(order, index);

        foreach (EnemyPawnController enemy in EnemyPawnController.All)
        {
            enemy.SetPlanVisible(enemy == current || enemy == next);
        }
    }

    // The next enemy pawn to act after the given turn: later this round if
    // there is one, otherwise whichever enemy goes first next round.
    private EnemyPawnController FindNextEnemy(List<Entry> order, int index)
    {
        for (int i = index + 1; i < order.Count; i++)
        {
            if (!order[i].IsPlayer && IsInPlay(order[i].Piece))
            {
                return (EnemyPawnController)order[i].Piece;
            }
        }

        List<Entry> nextRound = CollectPieces();
        nextRound.Sort(CompareTurnOrder);

        foreach (Entry entry in nextRound)
        {
            if (!entry.IsPlayer)
            {
                return (EnemyPawnController)entry.Piece;
            }
        }

        return null;
    }

    private IEnumerator PlayerTurn(ISelectablePiece piece)
    {
        ActivePlayerPiece = piece;
        selectionManager.ActivatePiece(piece);

        if (piece.HasReachableTiles)
        {
            // Done once it's moved - its HasBeenActivated stays on until the
            // round ends - or passed, or gone.
            while (IsInPlay((MonoBehaviour)piece) && !piece.HasBeenActivated)
            {
                if (!piece.IsMoving && !PawnSummonController.IsPlacing && WasPassPressed())
                {
                    break;
                }

                yield return null;
            }
        }

        ActivePlayerPiece = null;

        if (IsInPlay((MonoBehaviour)piece))
        {
            piece.Deselect();
            piece.EndInitiativeTurn();
        }
    }

    private IEnumerator EnemyTurn(EnemyPawnController enemy)
    {
        if (enemy.PlanInitiativeTurn())
        {
            if (enemyTurnDelay > 0f)
            {
                yield return new WaitForSeconds(enemyTurnDelay);
            }

            if (IsInPlay(enemy))
            {
                bool done = false;
                enemy.TakeInitiativeTurn(() => done = true);

                while (!done && IsInPlay(enemy))
                {
                    yield return null;
                }
            }
        }

        // Moved or not, its turn is used up for this round.
        if (enemy != null)
        {
            enemy.EndInitiativeTurn();
        }
    }

    // Lets anything still playing out - an enemy's move, a piece sliding
    // back from an attack, a teleport's camera glide, a summon being placed,
    // a scene transition - finish before the next turn starts.
    private static IEnumerator WaitUntilSettled()
    {
        while (TurnManager.IsEnemyTurn || BoardPieces.AnyPlayerPieceMoving() || ArsenalTeleporter.IsTeleporting || PawnSummonController.IsPlacing || ScreenWipe.IsBusy)
        {
            yield return null;
        }
    }

    private bool WasPassPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard[passKey].wasPressedThisFrame;
    }

    private List<Entry> CollectPieces()
    {
        List<Entry> pieces = new();

        foreach (PlayerController king in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            AddPlayerPiece(pieces, king);
        }

        foreach (WhitePawnController pawn in WhitePawnController.All)
        {
            AddPlayerPiece(pieces, pawn);
        }

        foreach (RookController rook in RookController.All)
        {
            AddPlayerPiece(pieces, rook);
        }

        foreach (KnightController knight in KnightController.All)
        {
            AddPlayerPiece(pieces, knight);
        }

        foreach (BishopController bishop in BishopController.All)
        {
            AddPlayerPiece(pieces, bishop);
        }

        // A piece on no tile is still waiting in its Pawn Case (it never got
        // deployed), so it has no turn.
        foreach (EnemyPawnController enemy in EnemyPawnController.All)
        {
            if (IsInPlay(enemy) && enemy.CurrentTile != null)
            {
                pieces.Add(new Entry { Piece = enemy, IsPlayer = false, Initiative = enemy.Initiative, TieBreak = GetTieBreak(enemy) });
            }
        }

        return pieces;
    }

    private void AddPlayerPiece<T>(List<Entry> pieces, T piece) where T : MonoBehaviour, ISelectablePiece
    {
        if (IsInPlay(piece) && piece.CurrentTile != null)
        {
            pieces.Add(new Entry { Piece = piece, IsPlayer = true, Initiative = piece.Initiative, TieBreak = GetTieBreak(piece) });
        }
    }

    private float GetTieBreak(MonoBehaviour piece)
    {
        if (!tieBreaks.TryGetValue(piece, out float tieBreak))
        {
            tieBreak = Random.value;
            tieBreaks[piece] = tieBreak;
        }

        return tieBreak;
    }

    // Highest Initiative first; on a tie, player pieces before enemy pawns,
    // then each team's own drawn order.
    private static int CompareTurnOrder(Entry a, Entry b)
    {
        int byInitiative = b.Initiative.CompareTo(a.Initiative);

        if (byInitiative != 0)
        {
            return byInitiative;
        }

        if (a.IsPlayer != b.IsPlayer)
        {
            return a.IsPlayer ? -1 : 1;
        }

        return a.TieBreak.CompareTo(b.TieBreak);
    }

    // Still on the board: not destroyed, switched off (how player pieces
    // are taken out when they die) or out of HP.
    private static bool IsInPlay(MonoBehaviour piece)
    {
        if (piece == null || !piece.isActiveAndEnabled)
        {
            return false;
        }

        return !piece.TryGetComponent(out Health health) || health.CurrentHP > 0;
    }

    private static void LogOrder(List<Entry> order)
    {
        List<string> names = new();

        foreach (Entry entry in order)
        {
            names.Add($"{entry.Piece.name} ({entry.Initiative})");
        }

        Debug.Log("InitiativeTurnManager: this round's turn order - " + string.Join(", ", names));
    }
}
