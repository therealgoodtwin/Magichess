using UnityEngine;

/// <summary>
/// Plays a "piece moving" sound once whenever this piece begins a move -
/// but only if it's actually visible on screen right now (via the
/// renderer's built-in, camera-culling-driven isVisible flag), so a bunch
/// of off-screen enemy pawns moving at once doesn't stack up a wall of
/// noise the player never even sees the cause of.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PieceMoveSound : MonoBehaviour
{
    [Tooltip("Sound played once each time this piece starts moving.")]
    [SerializeField] private AudioClip moveClip;

    [Tooltip("Any renderer on this piece, used to check whether it's currently visible to a camera. Auto-found on this object or its children if left empty.")]
    [SerializeField] private Renderer visibilityRenderer;

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (visibilityRenderer == null)
        {
            visibilityRenderer = GetComponentInChildren<Renderer>();
        }
    }

    public void PlayIfVisible()
    {
        if (moveClip != null && visibilityRenderer != null && visibilityRenderer.isVisible)
        {
            audioSource.PlayOneShot(moveClip);
        }
    }
}
