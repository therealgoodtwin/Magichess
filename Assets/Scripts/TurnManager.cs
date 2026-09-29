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

    public static event System.Action EnemyTurnStarted;
    public static event System.Action EnemyTurnEnded;

    public static void EndPlayerTurn()
    {
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

        if (enemiesMoving == 0)
        {
            EnemyTurnEnded?.Invoke();
        }
    }
}
