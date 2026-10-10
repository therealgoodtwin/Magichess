using System.Collections;
using UnityEngine;

/// <summary>
/// Puts a working King on the tile this object stands over when the scene
/// starts, e.g. at an encounter scene's entrance. Adds a Piece Selection
/// Manager as well if the scene doesn't have one, so the King can be
/// clicked and moved.
/// </summary>
public class PlayerSpawner : MonoBehaviour
{
    [Tooltip("The King prefab to spawn (Prefabs/Pawns/White/King).")]
    [SerializeField] private PlayerController kingPrefab;

    [Tooltip("On: no turns - the King stays selected and can move as often as he likes, like in the Overworld. Off: normal turns - the scene then needs its own End Turn setup (Turn Activation Controller), or the King can only move once. Ignored in a scene with an Initiative Turn Manager, which runs the turns itself.")]
    [SerializeField] private bool freeMovement = true;

    public PlayerController King { get; private set; }

    // Whether freeMovement actually applies in this scene.
    private bool isFreeMovement;

    private void Awake()
    {
        if (kingPrefab == null)
        {
            Debug.LogWarning("PlayerSpawner: no King prefab set.", this);
            return;
        }

        // Every object in the scene exists by now, whether or not its own
        // Awake has run yet.
        isFreeMovement = freeMovement && FindFirstObjectByType<InitiativeTurnManager>() == null;

        if (isFreeMovement)
        {
            TurnManager.DisableTurns(this);
        }

        King = Instantiate(kingPrefab, transform.position, kingPrefab.transform.rotation);

        Tile tile = FindTileBelow();

        if (tile != null)
        {
            PiecePivotUtility.CenterOnTile(King.transform, tile);
        }
        else
        {
            Debug.LogWarning("PlayerSpawner: no tile found to put the King on.", this);
        }

        if (FindFirstObjectByType<PieceSelectionManager>() == null)
        {
            gameObject.AddComponent<PieceSelectionManager>();
        }
    }

    private IEnumerator Start()
    {
        if (!isFreeMovement || King == null)
        {
            yield break;
        }

        // A frame's wait, so the King's own Start has worked out which tile
        // he's on before being asked to light up the ones around it. With
        // turns off he doesn't light them up by himself. In a battle, not
        // until both sides have deployed (see BattleSetup).
        yield return null;

        while (BattleSetup.IsActive)
        {
            yield return null;
        }

        if (King != null)
        {
            King.Select();
        }
    }

    private void OnDestroy()
    {
        TurnManager.RestoreTurns(this);
    }

    // Tile.All isn't reliably filled in yet during Awake, so the tiles are
    // looked up directly instead.
    private Tile FindTileBelow()
    {
        Tile nearest = null;
        float nearestSqrDistance = float.MaxValue;

        foreach (Tile tile in FindObjectsByType<Tile>(FindObjectsSortMode.None))
        {
            Vector3 flatOffset = tile.transform.position - transform.position;
            flatOffset.y = 0f;
            float sqrDistance = flatOffset.sqrMagnitude;

            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = tile;
            }
        }

        return nearest;
    }
}
