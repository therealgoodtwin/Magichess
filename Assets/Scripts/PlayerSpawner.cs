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

    [Tooltip("On: no turns - the King stays selected and can move as often as he likes, like in the Overworld. Off: normal turns - the scene then needs its own End Turn setup (Turn Activation Controller), or the King can only move once.")]
    [SerializeField] private bool freeMovement = true;

    public PlayerController King { get; private set; }

    private void Awake()
    {
        if (kingPrefab == null)
        {
            Debug.LogWarning("PlayerSpawner: no King prefab set.", this);
            return;
        }

        if (freeMovement)
        {
            TurnManager.DisableTurns(this);
        }

        King = Instantiate(kingPrefab, transform.position, kingPrefab.transform.rotation);

        Tile tile = FindTileBelow();

        if (tile != null)
        {
            SitOnTile(King.transform, tile);
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
        if (!freeMovement || King == null)
        {
            yield break;
        }

        // A frame's wait, so the King's own Start has worked out which tile
        // he's on before being asked to light up the ones around it. With
        // turns off he doesn't light them up by himself.
        yield return null;

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

    // Centres the piece's model over the tile and stands it on the tile's
    // top face - going by what's actually drawn rather than by the prefab's
    // pivot, which on the King isn't at the model's visual centre.
    private static void SitOnTile(Transform piece, Tile tile)
    {
        MeshRenderer[] renderers = piece.GetComponentsInChildren<MeshRenderer>();

        if (renderers.Length == 0 || !tile.TryGetComponent(out Renderer tileRenderer))
        {
            return;
        }

        Bounds pieceBounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            pieceBounds.Encapsulate(renderers[i].bounds);
        }

        Bounds tileBounds = tileRenderer.bounds;

        piece.position += new Vector3(
            tileBounds.center.x - pieceBounds.center.x,
            tileBounds.max.y - pieceBounds.min.y,
            tileBounds.center.z - pieceBounds.center.z);
    }
}
