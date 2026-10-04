using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// The encounters an Encounter Spawn Tile can roll, and the battle maps a
/// battle encounter can lead to. Point it at the folders once and the lists
/// fill themselves in, keeping up as prefabs and scenes are added, moved or
/// deleted (see EncounterPoolUpdater). Folders can't be looked inside while
/// the game is running, which is why the lists are stored here rather than
/// read from the folders on the fly.
/// </summary>
[CreateAssetMenu(fileName = "Encounter Pool", menuName = "Magichess/Encounter Pool")]
public class EncounterPool : ScriptableObject
{
    private const string DefaultBattleEncountersFolder = "Assets/Prefabs/Overworld/Encounters/Battle Encounters";
    private const string DefaultOtherEncountersFolder = "Assets/Prefabs/Overworld/Encounters/Merchant + Rest";
    private const string DefaultBattleMapsFolder = "Assets/Scenes/Battle Maps";

    [Tooltip("Every prefab in this folder (and its subfolders) can be rolled, and leads to a random battle map.")]
    [SerializeField] private Object battleEncountersFolder;

    [Tooltip("Every prefab in this folder (and its subfolders) can be rolled too, but keeps whatever scene its own Overworld Encounter is set to.")]
    [SerializeField] private Object otherEncountersFolder;

    [Tooltip("Every scene in this folder (and its subfolders) can be picked as a battle encounter's destination.")]
    [SerializeField] private Object battleMapsFolder;

    [SerializeField] private List<GameObject> battleEncounters = new();
    [SerializeField] private List<GameObject> otherEncounters = new();
    [SerializeField] private List<string> battleMapPaths = new();

    /// <summary>
    /// A kind of encounter (Elite, say) made up of one or more prefabs - its
    /// variants. Its weight is how often it's picked compared with the other
    /// groups, shared between its prefabs, so adding another variant adds
    /// variety without making the kind itself any more common. It can also
    /// guarantee a minimum number on every map, and cap how many of its
    /// prefabs can be on one map, both counted together. A prefab in more
    /// than one group has to fit under all of their limits.
    /// </summary>
    [System.Serializable]
    public class EncounterGroup
    {
        [Tooltip("Just a label, e.g. Elite.")]
        [SerializeField] private string name = "";

        [Tooltip("How often this group is picked compared with the others - 10 against 2 is five times as often. Shared between its prefabs. 0 means it's never picked, apart from its Min Count.")]
        [SerializeField, Min(0f)] private float weight = 1f;

        [Tooltip("At least this many of this group's prefabs are on every map, counted together, on random tiles. Any more than that are down to Weight. Can't go above Max Count.")]
        [SerializeField, Min(0)] private int minCount;

        [Tooltip("Whether there's a cap on how many of this group's prefabs can be on one map.")]
        [SerializeField] private bool hasLimit = true;

        [Tooltip("At most this many of this group's prefabs can be on one map, counted together. 0 means they never appear.")]
        [SerializeField, Min(0)] private int maxCount = 1;

        [Tooltip("The encounter prefabs in this group.")]
        [SerializeField] private List<GameObject> encounters = new();

        public string Name => name;
        public float Weight => weight;
        public bool HasLimit => hasLimit;
        public int MaxCount => maxCount;
        public IReadOnlyList<GameObject> Encounters => encounters;

        // The limit wins if the two disagree.
        public int MinCount => hasLimit ? Mathf.Min(minCount, maxCount) : minCount;
    }

    [Tooltip("Kinds of encounter, with how often each is picked and how many can be on one map.")]
    [FormerlySerializedAs("limits")]
    [SerializeField] private List<EncounterGroup> groups = new();

    [Tooltip("How often encounters that aren't in any group are picked, compared with the groups. Together they count as one group of their own, sharing this weight, with no limit.")]
    [SerializeField, Min(0f)] private float ungroupedWeight = 1f;

    public bool HasEncounters => AllEncounters().Count > 0;

    // Every group's Min Count added up - how many tiles the minimums need.
    public int MinimumTotal
    {
        get
        {
            int total = 0;

            foreach (EncounterGroup group in groups)
            {
                total += group.MinCount;
            }

            return total;
        }
    }

    // Picks a group by weight, then one of its prefabs, each equally likely -
    // leaving out any prefab that's hit one of its limits. alreadyRolled is
    // what's been rolled on the map so far, by prefab name, and tilesLeft
    // how many tiles (this one included) still have to roll. Null if
    // everything left has hit its limit (or has no weight).
    public GameObject PickEncounter(IReadOnlyList<string> alreadyRolled, int tilesLeft)
    {
        List<GameObject> allowed = AllEncounters().FindAll(prefab => IsUnderLimits(prefab, alreadyRolled));
        int stillNeeded = CountStillNeeded(alreadyRolled);

        // Minimums go on tiles spread at random across the map, not just the
        // last ones to roll: each tile takes one with a chance of stillNeeded
        // in tilesLeft, which becomes certain exactly when there are no
        // spare tiles left.
        if (stillNeeded > 0 && (stillNeeded >= tilesLeft || Random.value * tilesLeft < stillNeeded))
        {
            GameObject required = PickWeighted(allowed, alreadyRolled);

            if (required != null)
            {
                return required;
            }
        }

        return PickWeighted(allowed, null);
    }

    // With belowMinimumOf set, only groups still short of their Min Count
    // (given what's been rolled) are considered.
    private GameObject PickWeighted(List<GameObject> allowed, IReadOnlyList<string> belowMinimumOf)
    {
        List<string> labels = new();
        List<float> weights = new();
        List<List<GameObject>> choices = new();

        CollectChoices(allowed, belowMinimumOf, labels, weights, choices);

        if (choices.Count == 0)
        {
            return null;
        }

        float total = 0f;

        foreach (float weight in weights)
        {
            total += weight;
        }

        float roll = Random.value * total;
        int picked = choices.Count - 1;

        for (int i = 0; i < choices.Count; i++)
        {
            roll -= weights[i];

            if (roll < 0f)
            {
                picked = i;
                break;
            }
        }

        List<GameObject> prefabs = choices[picked];
        return prefabs[Random.Range(0, prefabs.Count)];
    }

    // Each group's chance of being picked on a roll that isn't filling a
    // minimum, before any limit has been reached - shown in the pool's
    // Inspector.
    public List<KeyValuePair<string, float>> GetChances()
    {
        List<string> labels = new();
        List<float> weights = new();
        List<List<GameObject>> choices = new();

        CollectChoices(AllEncounters(), null, labels, weights, choices);

        float total = 0f;

        foreach (float weight in weights)
        {
            total += weight;
        }

        List<KeyValuePair<string, float>> chances = new();

        for (int i = 0; i < labels.Count; i++)
        {
            chances.Add(new KeyValuePair<string, float>(labels[i], total > 0f ? weights[i] / total : 0f));
        }

        return chances;
    }

    // One choice per group that has weight and at least one of the given
    // prefabs, plus one for the given prefabs that aren't in any group. With
    // belowMinimumOf set, only groups still short of their Min Count count -
    // and a group with no weight still gets an even share there, since its
    // minimum has to be met regardless.
    private void CollectChoices(List<GameObject> prefabs, IReadOnlyList<string> belowMinimumOf, List<string> labels, List<float> weights, List<List<GameObject>> choices)
    {
        foreach (EncounterGroup group in groups)
        {
            List<GameObject> members = prefabs.FindAll(prefab => Covers(group, prefab.name));

            if (members.Count == 0)
            {
                continue;
            }

            float weight = group.Weight;

            if (belowMinimumOf != null)
            {
                if (CountRolled(group, belowMinimumOf) >= group.MinCount)
                {
                    continue;
                }

                if (weight <= 0f)
                {
                    weight = 1f;
                }
            }

            if (weight > 0f)
            {
                labels.Add(string.IsNullOrEmpty(group.Name) ? "(unnamed group)" : group.Name);
                weights.Add(weight);
                choices.Add(members);
            }
        }

        if (belowMinimumOf != null)
        {
            return;
        }

        List<GameObject> ungrouped = prefabs.FindAll(prefab => !IsInAnyGroup(prefab.name));

        if (ungroupedWeight > 0f && ungrouped.Count > 0)
        {
            labels.Add("Not in any group");
            weights.Add(ungroupedWeight);
            choices.Add(ungrouped);
        }
    }

    private List<GameObject> AllEncounters()
    {
        List<GameObject> all = new();
        AddExisting(all, battleEncounters);
        AddExisting(all, otherEncounters);
        return all;
    }

    private bool IsUnderLimits(GameObject prefab, IReadOnlyList<string> alreadyRolled)
    {
        foreach (EncounterGroup group in groups)
        {
            if (!group.HasLimit || !Covers(group, prefab.name))
            {
                continue;
            }

            if (CountRolled(group, alreadyRolled) >= group.MaxCount)
            {
                return false;
            }
        }

        return true;
    }

    // How many more encounters the groups' minimums still need, all together.
    private int CountStillNeeded(IReadOnlyList<string> alreadyRolled)
    {
        int needed = 0;

        foreach (EncounterGroup group in groups)
        {
            needed += Mathf.Max(0, group.MinCount - CountRolled(group, alreadyRolled));
        }

        return needed;
    }

    private static int CountRolled(EncounterGroup group, IReadOnlyList<string> alreadyRolled)
    {
        int count = 0;

        foreach (string rolled in alreadyRolled)
        {
            if (Covers(group, rolled))
            {
                count++;
            }
        }

        return count;
    }

    private bool IsInAnyGroup(string prefabName)
    {
        foreach (EncounterGroup group in groups)
        {
            if (Covers(group, prefabName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Covers(EncounterGroup group, string prefabName)
    {
        foreach (GameObject encounter in group.Encounters)
        {
            if (encounter != null && encounter.name == prefabName)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsBattleEncounter(GameObject prefab)
    {
        return prefab != null && battleEncounters.Contains(prefab);
    }

    public GameObject FindEncounter(string prefabName)
    {
        foreach (GameObject prefab in battleEncounters)
        {
            if (prefab != null && prefab.name == prefabName)
            {
                return prefab;
            }
        }

        foreach (GameObject prefab in otherEncounters)
        {
            if (prefab != null && prefab.name == prefabName)
            {
                return prefab;
            }
        }

        return null;
    }

    // A random battle map that can actually be loaded - one missing from the
    // build's scene list can't be - or null if there are none.
    public string PickBattleMap()
    {
        List<string> loadable = new();

        foreach (string path in battleMapPaths)
        {
            string sceneName = Path.GetFileNameWithoutExtension(path);

            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                loadable.Add(sceneName);
            }
        }

        return loadable.Count > 0 ? loadable[Random.Range(0, loadable.Count)] : null;
    }

    public bool CanLoadBattleMap(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            return false;
        }

        foreach (string path in battleMapPaths)
        {
            if (Path.GetFileNameWithoutExtension(path) == sceneName)
            {
                return true;
            }
        }

        return false;
    }

    private static void AddExisting(List<GameObject> into, List<GameObject> from)
    {
        foreach (GameObject prefab in from)
        {
            if (prefab != null)
            {
                into.Add(prefab);
            }
        }
    }

#if UNITY_EDITOR
    public IReadOnlyList<string> BattleMapPaths => battleMapPaths;

    // A new pool starts out pointed at the project's usual folders.
    private void Reset()
    {
        battleEncountersFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultBattleEncountersFolder);
        otherEncountersFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultOtherEncountersFolder);
        battleMapsFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultBattleMapsFolder);
        Refresh();
    }

    private void OnValidate()
    {
        // Looking things up in the AssetDatabase isn't safe in the middle of
        // OnValidate, so it waits until the Editor is done with it.
        EditorApplication.delayCall += () =>
        {
            if (this != null)
            {
                Refresh();
            }
        };
    }

    // Re-reads the folders. Only marks the pool changed (so it gets saved)
    // if something in them actually changed.
    public void Refresh()
    {
        List<GameObject> battle = FindPrefabs(battleEncountersFolder);
        List<GameObject> other = FindPrefabs(otherEncountersFolder);
        List<string> maps = FindScenePaths(battleMapsFolder);

        if (SameItems(battle, battleEncounters) && SameItems(other, otherEncounters) && SameItems(maps, battleMapPaths))
        {
            return;
        }

        battleEncounters = battle;
        otherEncounters = other;
        battleMapPaths = maps;
        EditorUtility.SetDirty(this);
    }

    private static List<GameObject> FindPrefabs(Object folder)
    {
        List<GameObject> prefabs = new();
        string folderPath = GetFolderPath(folder);

        if (folderPath == null)
        {
            return prefabs;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folderPath }))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));

            if (prefab != null)
            {
                prefabs.Add(prefab);
            }
        }

        return prefabs;
    }

    private static List<string> FindScenePaths(Object folder)
    {
        List<string> paths = new();
        string folderPath = GetFolderPath(folder);

        if (folderPath == null)
        {
            return paths;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { folderPath }))
        {
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        }

        return paths;
    }

    private static string GetFolderPath(Object folder)
    {
        if (folder == null)
        {
            return null;
        }

        string path = AssetDatabase.GetAssetPath(folder);
        return AssetDatabase.IsValidFolder(path) ? path : null;
    }

    private static bool SameItems<T>(List<T> a, List<T> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }
#endif
}
