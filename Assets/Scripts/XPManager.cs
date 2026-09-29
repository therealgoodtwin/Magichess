/// <summary>
/// Tracks the player's total collected XP and notifies listeners (e.g. XPText)
/// whenever it changes.
/// </summary>
public static class XPManager
{
    public static int TotalXP { get; private set; }

    public static event System.Action<int> XPChanged;

    public static void AddXP(int amount)
    {
        TotalXP += amount;
        XPChanged?.Invoke(TotalXP);
    }

    public static void SpendXP(int amount)
    {
        TotalXP = System.Math.Max(0, TotalXP - amount);
        XPChanged?.Invoke(TotalXP);
    }
}
