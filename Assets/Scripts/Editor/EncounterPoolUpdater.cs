using System.IO;
using UnityEditor;

/// <summary>
/// Keeps every Encounter Pool's lists in step with its folders: whenever a
/// prefab, scene or folder is added, changed, moved or deleted, each pool
/// re-reads its folders.
/// </summary>
public class EncounterPoolUpdater : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (!AnyRelevant(importedAssets) && !AnyRelevant(deletedAssets) &&
            !AnyRelevant(movedAssets) && !AnyRelevant(movedFromAssetPaths))
        {
            return;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:EncounterPool"))
        {
            EncounterPool pool = AssetDatabase.LoadAssetAtPath<EncounterPool>(AssetDatabase.GUIDToAssetPath(guid));

            if (pool != null)
            {
                pool.Refresh();
            }
        }
    }

    // Prefabs, scenes, and folders (which have no extension).
    private static bool AnyRelevant(string[] paths)
    {
        foreach (string path in paths)
        {
            string extension = Path.GetExtension(path);

            if (extension == ".prefab" || extension == ".unity" || extension == "")
            {
                return true;
            }
        }

        return false;
    }
}
