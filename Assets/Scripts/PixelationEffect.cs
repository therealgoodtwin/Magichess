using UnityEngine;

/// <summary>
/// Gives the camera a chunky, retro-pixelated look: the frame is rendered
/// down to a small offscreen texture, then scaled back up to fill the
/// screen with point (nearest-neighbor) filtering, so each low-res pixel
/// becomes a visible block instead of being smoothed out.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class PixelationEffect : MonoBehaviour
{
    [Tooltip("Vertical resolution the scene is rendered at before being upscaled. Lower = chunkier pixels.")]
    [SerializeField] private int pixelHeight = 180;

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        int height = Mathf.Max(1, pixelHeight);
        int width = Mathf.Max(1, Mathf.RoundToInt(height * (source.width / (float)source.height)));

        RenderTexture lowRes = RenderTexture.GetTemporary(width, height, 0, source.format);
        lowRes.filterMode = FilterMode.Point;

        Graphics.Blit(source, lowRes);
        Graphics.Blit(lowRes, destination);

        RenderTexture.ReleaseTemporary(lowRes);
    }
}
