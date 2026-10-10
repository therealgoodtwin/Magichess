using UnityEngine;

/// <summary>
/// Tracks how many enemies are currently mid-move so the player can be
/// locked out of clicking until every enemy has finished responding, and
/// owns the player-turn-to-enemy-turn handoff: EndPlayerTurn() tells every
/// enemy pawn to carry out its already-planned move at the same time, and
/// EnemyTurnEnded fires once they've all finished moving - or immediately,
/// if it turns out none of them had anywhere to go this turn.
/// </summary>
public static class TurnManager
{
    private static int enemiesMoving;

    public static bool IsEnemyTurn => enemiesMoving > 0;

    // Off in scenes with no turn structure (the Main Menu): the King can then
    // move as often as it likes and White Pawns can't be selected at all.
    public static bool TurnsEnabled { get; set; } = true;

    // Whichever object last switched turns off through DisableTurns. Only
    // that same object can switch them back on, so a scene being torn down
    // can't undo the setting of the scene replacing it - with an async load
    // the new scene can start up before the old one is destroyed.
    private static Object turnsDisabledBy;

    public static void DisableTurns(Object owner)
    {
        turnsDisabledBy = owner;
        TurnsEnabled = false;
    }

    public static void RestoreTurns(Object owner)
    {
        if (!ReferenceEquals(turnsDisabledBy, owner))
        {
            return;
        }

        turnsDisabledBy = null;
        TurnsEnabled = true;
    }

    public static event System.Action EnemyTurnStarted;
    public static event System.Action EnemyTurnEnded;

    public static void EndPlayerTurn()
    {
        // Initiative turns have no End Turn - each piece's turn ends on its
        // own, and every enemy moving at once would skip the turn order.
        // Nor is there a turn to end while a battle is still being set up.
        if (InitiativeTurnManager.IsRunning || BattleSetup.IsActive)
        {
            return;
        }

        EnemyTurnStarted?.Invoke();

        // Nothing actually started moving in response (no enemies, or none
        // of them had a valid planned move) - the enemy turn is already
        // over, so say so now rather than waiting for an EndEnemyMove()
        // that's never coming.
        if (enemiesMoving == 0)
        {
            EnemyTurnEnded?.Invoke();
        }
    }

    public static void BeginEnemyMove()
    {
        enemiesMoving++;
    }

    public static void EndEnemyMove()
    {
        enemiesMoving = Mathf.Max(0, enemiesMoving - 1);

        // Under initiative turns one enemy finishing isn't the end of the
        // turn - EndRound is.
        if (enemiesMoving == 0 && !InitiativeTurnManager.IsRunning)
        {
            EnemyTurnEnded?.Invoke();
        }
    }

    // Initiative turns: every piece has had its turn this round. Raised
    // through the same EnemyTurnEnded the End Turn system uses, so everything
    // that resets or counts once per turn (pieces' activation, wave timers)
    // does so once per round.
    public static void EndRound()
    {
        EnemyTurnEnded?.Invoke();
    }
}
