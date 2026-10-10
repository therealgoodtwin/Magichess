using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a GameObject as a walkable chessboard tile, records its color, and
/// plays a "clickable" highlight (a small lift plus a pulse towards deep blue)
/// while <see cref="SetHighlighted"/> is on, or an "incoming" highlight (the
/// same lift, but swapping to a warning material and pulsing its High
/// Intensity property instead) while <see cref="SetEnemyDestinationHighlighted"/>
/// is on. Both can be active at once - the enemy highlight wins the material
/// slot when they overlap, since a warning is the more urgent thing to show.
/// A third, <see cref="SetDeployHighlighted"/>, flashes the tile green with
/// no lift: somewhere the player can deploy a piece before a battle.
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

    [Tooltip("Whether enemy pawns are allowed to spawn on this tile (used by the Wave Manager).")]
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

    [Tooltip("The colour a highlighted tile pulses towards, reached at the peak of each pulse.")]
    [SerializeField] private Color highlightColor = new Color(0.1f, 0.2f, 0.75f);

    [Tooltip("How far the tile's own colour turns into Highlight Color at the peak of the pulse - 1 is all the way.")]
    [SerializeField, Range(0f, 1f)] private float highlightColorAmount = 0.6f;

    [Header("Deployment Highlight")]
    [Tooltip("The colour a tile flashes towards while it's somewhere the player can deploy a piece, in a battle's setup phase.")]
    [SerializeField] private Color deployHighlightColor = new Color(0.1f, 0.85f, 0.2f);

    [Tooltip("How far the tile's own colour turns into Deploy Highlight Color at the peak of the flash - 1 is all the way.")]
    [SerializeField, Range(0f, 1f)] private float deployHighlightColorAmount = 0.75f;

    [Header("Enemy Move Highlight")]
    [Tooltip("Material swapped in while a pawn is en route to this tile (e.g. the EnemyTile material).")]
    [SerializeField] private Material enemyDestinationMaterial;
    [SerializeField] private float enemyPulseMin = 1f;
    [SerializeField] private float enemyPulseMax = 3f;

    private static readonly int HighIntensityId = Shader.PropertyToID("_HighIntensity");

    public static readonly List<Tile> All = new();

    // Sunk tiles (see Sink) have dropped out of play: they never highlight,
    // and the King won't count them as somewhere he can move.
    public bool IsSunk { get; private set; }

    private Vector3 baseLocalPosition;
    private Renderer tileRenderer;
    private Material originalMaterial;
    private Material enemyMaterialInstance;
    private Color baseColor;
    private Color peakColor;
    private Color deployPeakColor;
    private bool isHighlighted;
    private bool isDeployHighlighted;
    private float pulseT;
    private bool isEnemyDestinationHighlighted;
    private float enemyPulseT;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
        tileRenderer = GetComponent<Renderer>();
        originalMaterial = tileRenderer.material;
        baseColor = originalMaterial.color;
        peakColor = Color.Lerp(baseColor, highlightColor, highlightColorAmount);
        deployPeakColor = Color.Lerp(baseColor, deployHighlightColor, deployHighlightColorAmount);

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

    // Lowers the tile's resting position by depth world units and takes it
    // out of play for good. It slides down at the same speed as a highlight
    // lift, or jumps straight there if instant (restoring a saved board).
    public void Sink(float depth, bool instant)
    {
        if (IsSunk)
        {
            return;
        }

        SetHighlighted(false);
        SetEnemyDestinationHighlighted(false);
        IsSunk = true;

        Vector3 sunkWorldPosition = BaseWorldPosition + Vector3.down * depth;
        baseLocalPosition = transform.parent != null
            ? transform.parent.InverseTransformPoint(sunkWorldPosition)
            : sunkWorldPosition;

        if (instant)
        {
            transform.localPosition = baseLocalPosition;
        }
    }

    public void SetHighlighted(bool highlighted)
    {
        if (highlighted && IsSunk)
        {
            return;
        }

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

    // The setup phase's "you can deploy here": the tile flashes green, but
    // stays where it is - no lift - so a piece can be stood on it as it
    // flashes. A move highlight on the same tile takes over the colour for
    // as long as it's on.
    public void SetDeployHighlighted(bool highlighted)
    {
        if (highlighted && IsSunk)
        {
            return;
        }

        if (isDeployHighlighted == highlighted)
        {
            return;
        }

        isDeployHighlighted = highlighted;
        pulseT = 0f;

        if (!highlighted && !isHighlighted)
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

        if (!isHighlighted && !isDeployHighlighted)
        {
            return;
        }

        pulseT += Time.deltaTime * pulseSpeed;
        float playerWave = (Mathf.Sin(pulseT) + 1f) * 0.5f;
        originalMaterial.color = Color.Lerp(baseColor, isHighlighted ? peakColor : deployPeakColor, playerWave);
    }
}
