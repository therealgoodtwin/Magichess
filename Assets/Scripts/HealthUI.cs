using RayFire;
using UnityEngine;

/// <summary>
/// Shows some Health's HP as a row of icons: the first CurrentHP entries of
/// <see cref="hpIcons"/> stay standing, the rest get demolished with a
/// small burst of force - so losing HP destroys icons from the highest
/// slot down. There's no un-destroying them, so gaining HP back (nothing
/// does that yet) wouldn't restore any already-lost icon.
/// </summary>
public class HealthUI : MonoBehaviour
{
    [Tooltip("Health this displays. Leave empty to use a Health on this same object, else the King's.")]
    [SerializeField] private Health health;

    [Tooltip("HP icons in order, slot 1 first. The first CurrentHP of these stay standing.")]
    [SerializeField] private GameObject[] hpIcons;

    [Tooltip("Force applied to an icon's fragments, along its own forward direction, when it's destroyed.")]
    [SerializeField] private float explosionForce = 5f;

    [Tooltip("Extra upward force added on top of the explosion push.")]
    [SerializeField] private float explosionUpwardForce = 5f;

    private void OnEnable()
    {
        // Unwired: prefer a Health on this same object (e.g. the Portal's own
        // HealthUI) before falling back to the King's, so a HealthUI sitting
        // next to some other Health can never silently track the King's.
        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (health == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            health = player != null ? player.GetComponent<Health>() : null;
        }

        if (health != null)
        {
            health.OnHealthChanged += HandleHealthChanged;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnHealthChanged -= HandleHealthChanged;
        }
    }

    // Health.CurrentHP is only valid once Health's own Awake() has run.
    // Unity pairs each object's Awake+OnEnable together (not "every Awake,
    // then every OnEnable" scene-wide), so reading it back in OnEnable here
    // risked catching Health before its Awake ran - which read as HP 0 and
    // destroyed every icon instantly. Start() is the one callback Unity
    // guarantees runs after every object's Awake has completed.
    private void Start()
    {
        if (health != null)
        {
            HandleHealthChanged(health.CurrentHP, health.MaxHP);
        }
    }

    private void HandleHealthChanged(int currentHP, int maxHP)
    {
        for (int i = 0; i < hpIcons.Length; i++)
        {
            if (hpIcons[i] == null || i < currentHP)
            {
                continue;
            }

            DestroyIcon(hpIcons[i]);
        }
    }

    private void DestroyIcon(GameObject icon)
    {
        if (!icon.TryGetComponent(out RayfireRigid rigid))
        {
            icon.SetActive(false);
            return;
        }

        rigid.Demolish();

        if (!rigid.HasFragments)
        {
            return;
        }

        Vector3 force = icon.transform.forward * explosionForce + Vector3.up * explosionUpwardForce;

        foreach (RayfireRigid fragment in rigid.fragments)
        {
            if (fragment != null && fragment.physics.rb != null)
            {
                fragment.physics.rb.AddForce(force, ForceMode.Impulse);
            }
        }
    }
}
