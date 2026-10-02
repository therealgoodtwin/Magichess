using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An encounter on the Overworld (the Placeholder pawn). When the King moves
/// onto its tile, OverworldController saves the Overworld and sends the
/// player to this encounter's scene through the screen wipe. An encounter
/// that's already been taken stays gone when the player comes back.
/// </summary>
public class OverworldEncounter : MonoBehaviour
{
    [Tooltip("The scene the player is sent to when the King takes this encounter. Drag the scene asset in.")]
    [SerializeField] private SceneField scene;

    public static readonly List<OverworldEncounter> All = new();

    public string SceneName => scene != null ? scene.SceneName : null;

    // Where this encounter was placed in the scene - what OverworldState
    // remembers it by once it's been taken.
    public Vector3 HomePosition { get; private set; }

    private Tile tile;

    // The tile this encounter stands on, found once tiles exist.
    public Tile Tile
    {
        get
        {
            if (tile == null)
            {
                tile = TileGrid.FindNearest(HomePosition);
            }

            return tile;
        }
    }

    private void Awake()
    {
        HomePosition = transform.position;

        if (OverworldState.IsEncounterCleared(gameObject.scene.name, HomePosition))
        {
            // Deactivated as well as destroyed, so none of its scripts get
            // a Start or an Update in before it's actually gone.
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    public static OverworldEncounter FindOnTile(Tile target)
    {
        if (target == null)
        {
            return null;
        }

        foreach (OverworldEncounter encounter in All)
        {
            if (encounter.Tile == target)
            {
                return encounter;
            }
        }

        return null;
    }

    // An Enemy Pawn Controller already blows this pawn up when the King
    // lands on it. Anything else used as an encounter is just removed.
    public void Clear()
    {
        if (!TryGetComponent(out EnemyPawnController _))
        {
            Destroy(gameObject);
        }
    }
}
