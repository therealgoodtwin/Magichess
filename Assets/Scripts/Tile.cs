using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a GameObject as a walkable chessboard tile, records its color, and
/// plays a "clickable" highlight (a small lift plus a brightness pulse)
/// while <see cref="SetHighlighted"/> is on, or an "incoming" highlight (the
/// same lift, but swapping to a warning material and pulsing its High
/// Intensity property instead) while <see cref="SetEnemyDestinationHighlighted"/>
/// is on. Both can be active at once - the enemy highlight wins the material
/// slot when they overlap, since a warning is the more urgent thing to show.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class Tile : MonoBehaviour
{
    public enum TileColor
    {
        White,
        Black
    }

    [SerializeField] private TileColor color;

    public TileColor SquareColor => color;

    [Tooltip("Whether enemy pawns are allowed to spawn on this tile. Paint these via Tools > Regicide 2 > Paint Spawn Tiles instead of checking them by hand.")]
    [SerializeField] private bool isSpawnTile;

    public bool IsSpawnTile => isSpawnTile;

    // The tile's resting (unlifted) world position, regardless of whatever
    // the highlight lift animation currently has transform.position at -
    // lets anything standing on the tile compute a fixed height above its
    // true surface rather than above wherever it happens to be mid-lift.
    public Vector3 BaseWorldPosition => transform.parent != null
        ? transform.parent.TransformPoint(baseLocalPosition)
        : baseLocalPosition;

    [Header("Highlight")]
    [SerializeField] private float liftHeight = 0.2f;
    [SerializeField] private float liftSpeed = 6f;
    [SerializeField] private float pulseSpeed = 3f;
    [SerializeField, Range(0f, 1f)] private float pulseBrightAmount = 0.4f;
    [SerializeField, Range(0f, 1f)] private float pulseDarkAmount = 0.4f;

    [Header("Enemy Move Highlight")]
    [Tooltip("Material swapped in while a pawn is en route to this tile (e.g. the EnemyTile material).")]
    [SerializeField] private Material enemyDestinationMaterial;
    [SerializeField] private float enemyPulseMin = 1f;
    [SerializeField] private float enemyPulseMax = 3f;

    private static readonly int HighIntensityId = Shader.PropertyToID("_HighIntensity");

    public static readonly List<Tile> All = new();

    private Vector3 baseLocalPosition;
    private Renderer tileRenderer;
    private Material originalMaterial;
    private Material enemyMaterialInstance;
    private Color baseColor;
    private Color brightColor;
    private Color darkColor;
    private bool isHighlighted;
    private float pulseT;
    private bool isEnemyDestinationHighlighted;
    private float enemyPulseT;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
        tileRenderer = GetComponent<Renderer>();
        originalMaterial = tileRenderer.material;
        baseColor = originalMaterial.color;
        brightColor = Color.Lerp(baseColor, Color.white, pulseBrightAmount);
        darkColor = Color.Lerp(baseColor, Color.black, pulseDarkAmount);

        if (enemyDestinationMaterial != null)
        {
            enemyMaterialInstance = new Material(enemyDestinationMaterial);
        }
    }

    private void OnEnable()
    {
        All.Add(this);
        TileGrid.MarkDirty();
    }

    private void OnDisable()
    {
        All.Remove(this);
        TileGrid.MarkDirty();
    }

    public void SetHighlighted(bool highlighted)
    {
        if (isHighlighted == highlighted)
        {
            return;
        }

        isHighlighted = highlighted;
        pulseT = 0f;

        if (!highlighted)
        {
            originalMaterial.color = baseColor;
        }
    }

    public void SetEnemyDestinationHighlighted(bool highlighted)
    {
        if (isEnemyDestinationHighlighted == highlighted)
        {
            return;
        }

        isEnemyDestinationHighlighted = highlighted;
        enemyPulseT = 0f;

        if (highlighted && enemyMaterialInstance != null)
        {
            tileRenderer.material = enemyMaterialInstance;
        }
        else if (!highlighted)
        {
            tileRenderer.material = originalMaterial;
        }
    }

    private void Update()
    {
        Vector3 targetLocalPosition = baseLocalPosition;

        if (isHighlighted || isEnemyDestinationHighlighted)
        {
            targetLocalPosition.y += liftHeight;
        }

        transform.localPosition = Vector3.MoveTowards(transform.localPosition, targetLocalPosition, liftSpeed * Time.deltaTime);

        if (isEnemyDestinationHighlighted)
        {
            if (enemyMaterialInstance != null)
            {
                enemyPulseT += Time.deltaTime * pulseSpeed;
                float wave = (Mathf.Sin(enemyPulseT) + 1f) * 0.5f;
                enemyMaterialInstance.SetFloat(HighIntensityId, Mathf.Lerp(enemyPulseMin, enemyPulseMax, wave));
            }

            return;
        }

        if (!isHighlighted)
        {
            return;
        }

        pulseT += Time.deltaTime * pulseSpeed;
        float playerWave = (Mathf.Sin(pulseT) + 1f) * 0.5f;
        originalMaterial.color = Color.Lerp(darkColor, brightColor, playerWave);
    }
}
