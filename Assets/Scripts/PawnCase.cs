using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A case one side's pieces wait in before a battle. As the scene starts it
/// fills its Pawn Holders: each holder takes one piece of that holder's own
/// side - any of the player's except the King, or any of the enemy's - off
/// the board, to stand on it. With several cases, the pieces fill one after
/// another. From there BattleSetup deploys them onto the board, onto the
/// rows of tiles nearest their holders.
///
/// Its Pawn Holders are its children. Any child with "Pawn Holder" in its
/// name is made one automatically. This case's Side is a quick way to set
/// all of them at once: change it and every holder in the case follows. A
/// holder changed on its own afterwards keeps its own setting.
/// </summary>
// After every piece's own Start, so they've all worked out where they are
// before being moved, and before BattleSetup's, which counts what's waiting.
[DefaultExecutionOrder(50)]
public class PawnCase : MonoBehaviour
{
    public enum Side
    {
        Player,
        Enemy
    }

    private const string HolderName = "Pawn Holder";

    [Tooltip("Whose case this is: the player's (all pieces but the King) or the enemy's. Changing it sets every Pawn Holder in the case to match.")]
    [SerializeField] private Side side;

    // The Side this case's holders were last set to. While it's the same as
    // Side there's nothing to pass on, so holders changed one by one keep
    // their own setting.
    [SerializeField, HideInInspector] private Side appliedSide;

    public Side CaseSide => side;

    private readonly List<PawnHolder> holders = new();

    private void Awake()
    {
        AddMissingHolders();

        // A Side its holders haven't been given yet - one chosen before the
        // holders had a setting of their own, say.
        if (side != appliedSide)
        {
            ApplySideToHolders();
        }

        foreach (PawnHolder holder in GetComponentsInChildren<PawnHolder>())
        {
            if (!holder.IsDuplicate)
            {
                holders.Add(holder);
            }
        }

        holders.Sort(PawnHolder.CompareOrder);

        if (holders.Count == 0)
        {
            Debug.LogWarning($"PawnCase: '{name}' has no Pawn Holders - add the Pawn Holder script to its slots, or name them \"{HolderName}\".", this);
        }
    }

    private void Start()
    {
        PawnHolder.Fill(holders);

        if (!BattleSetup.IsActive)
        {
            Debug.LogWarning($"PawnCase: '{name}' has taken its pieces off the board, but the scene has no Battle Setup to deploy them again.", this);
        }
    }

    private void ApplySideToHolders()
    {
        appliedSide = side;

        foreach (PawnHolder holder in GetComponentsInChildren<PawnHolder>(true))
        {
            holder.SetSide(side);
        }
    }

    // Makes a Pawn Holder, on this case's side, of every child that's named
    // like one but doesn't have the script yet.
    private void AddMissingHolders()
    {
        foreach (Transform child in FindChildrenMissingHolder())
        {
            child.gameObject.AddComponent<PawnHolder>().SetSide(side);
        }
    }

    private List<Transform> FindChildrenMissingHolder()
    {
        List<Transform> missing = new();

        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != transform && child.name.Contains(HolderName) && !child.TryGetComponent(out PawnHolder _))
            {
                missing.Add(child);
            }
        }

        return missing;
    }

#if UNITY_EDITOR
    // Runs when this script is first added to an object in the Editor: a
    // case with "Enemy" in its name starts out as the enemy's, and its
    // holders get their script, and their side, straight away.
    private void Reset()
    {
        side = name.Contains("Enemy") ? Side.Enemy : Side.Player;

        foreach (Transform child in FindChildrenMissingHolder())
        {
            UnityEditor.Undo.AddComponent<PawnHolder>(child.gameObject);
        }

        ApplySideToHoldersInEditor();
    }

    // Runs when Side is changed in the Inspector (and as scripts reload):
    // the holders follow, there and then.
    private void OnValidate()
    {
        if (side != appliedSide)
        {
            ApplySideToHoldersInEditor();
        }
    }

    private void ApplySideToHoldersInEditor()
    {
        ApplySideToHolders();
        MarkChanged(this);

        foreach (PawnHolder holder in GetComponentsInChildren<PawnHolder>(true))
        {
            MarkChanged(holder);
        }
    }

    // So the change is saved with the scene or prefab it's in.
    private static void MarkChanged(Object changed)
    {
        UnityEditor.EditorUtility.SetDirty(changed);

        if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(changed))
        {
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(changed);
        }
    }
#endif
}
