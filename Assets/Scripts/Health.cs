using System;
using UnityEngine;

/// <summary>
/// Tracks hit points for whatever it's attached to. Reaching 0 fires
/// OnDeath once; how the owner reacts (disable, explode, etc.) is up to
/// whoever listens, not this component.
/// </summary>
public class Health : MonoBehaviour
{
    [Tooltip("HP this has when the game starts. On a piece, the HP field on its controller sets this instead.")]
    [SerializeField] private int startingHP = 1;

    [Tooltip("The most HP this can have. On a piece, the HP field on its controller sets this instead.")]
    [SerializeField] private int maxHP = 1;

    // (currentHP, maxHP)
    public event Action<int, int> OnHealthChanged;
    public event Action OnDeath;

    public int CurrentHP { get; private set; }
    public int MaxHP => maxHP;
    public int StartingHP => startingHP;

    // Gives this a fresh, full hp - used by a piece's own HP setting. Sets
    // the starting value too, so it holds whether or not this component's
    // own Awake has run yet.
    public void SetHP(int hp)
    {
        startingHP = hp;
        maxHP = hp;
        CurrentHP = hp;
    }

    // Where the last TakeDamage call came from - lets an OnDeath listener
    // react toward/away from whatever dealt the fatal hit.
    public Vector3 LastDamageSourcePosition { get; private set; }

    private void Awake()
    {
        CurrentHP = Mathf.Clamp(startingHP, 0, maxHP);
    }

    private void Start()
    {
        OnHealthChanged?.Invoke(CurrentHP, maxHP);
    }

    public void TakeDamage(int amount, Vector3 sourcePosition)
    {
        if (amount <= 0 || CurrentHP <= 0)
        {
            return;
        }

        LastDamageSourcePosition = sourcePosition;
        CurrentHP = Mathf.Max(0, CurrentHP - amount);
        OnHealthChanged?.Invoke(CurrentHP, maxHP);

        if (CurrentHP == 0)
        {
            OnDeath?.Invoke();
        }
    }

    // An unconditional, amount-independent death - e.g. contact damage that
    // always kills outright rather than chipping off a fixed HP amount.
    public void Kill()
    {
        if (CurrentHP <= 0)
        {
            return;
        }

        CurrentHP = 0;
        OnHealthChanged?.Invoke(CurrentHP, maxHP);
        OnDeath?.Invoke();
    }
}
