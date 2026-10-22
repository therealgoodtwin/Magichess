using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Plays a particle effect wherever the player left-clicks. Each click gets a
/// fresh copy of the chosen effect - so quick clicks don't cut each other's
/// effect short - placed on whatever is under the cursor (a tile, the
/// terrain...), or a set distance in front of the camera where there's
/// nothing. Every copy removes itself once it has finished playing.
/// </summary>
public class ClickParticles : MonoBehaviour
{
    [Tooltip("The effect to play on each click: a particle system on a child of this object (or anywhere else in the scene), or a prefab. One in the scene is only used as the original to copy, so it's switched off when the scene starts rather than playing by itself.")]
    [SerializeField] private ParticleSystem effect;

    [Tooltip("On: the effect appears on whatever is under the cursor. Off: always Distance From Camera away, right under the cursor and in front of everything.")]
    [SerializeField] private bool placeOnSurface = true;

    [Tooltip("How far in front of the camera the effect appears when it isn't placed on a surface, or there's nothing under the cursor.")]
    [SerializeField, Min(0.01f)] private float distanceFromCamera = 10f;

    private void Awake()
    {
        if (effect == null)
        {
            Debug.LogWarning($"ClickParticles: '{name}' has no effect set.", this);
            return;
        }

        // Every copy would carry this script along too, and start making
        // copies of its own.
        if (effect.GetComponentInChildren<ClickParticles>(true) != null)
        {
            Debug.LogWarning($"ClickParticles: the effect can't be on the same object as this script - put it on a child of '{name}' instead.", this);
            effect = null;
            return;
        }

        if (effect.gameObject.scene.IsValid())
        {
            effect.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        Camera cam = Camera.main;

        if (effect == null || mouse == null || cam == null || !mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

        Vector3 position = placeOnSurface && Physics.Raycast(ray, out RaycastHit hit)
            ? hit.point
            : ray.GetPoint(distanceFromCamera);

        Play(position);
    }

    private void Play(Vector3 position)
    {
        ParticleSystem copy = Instantiate(effect, position, effect.transform.rotation);
        copy.gameObject.SetActive(true);
        copy.Play(true);
        Destroy(copy.gameObject, GetPlayTime(copy));
    }

    // How long until every part of the effect - its child systems included -
    // has finished, its last particles faded out too.
    private static float GetPlayTime(ParticleSystem system)
    {
        float longest = 0f;

        foreach (ParticleSystem part in system.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = part.main;
            longest = Mathf.Max(longest, main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
        }

        return longest;
    }
}
