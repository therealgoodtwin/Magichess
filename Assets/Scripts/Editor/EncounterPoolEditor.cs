using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector for an Encounter Pool: the folders and encounter groups, each
/// group's chance per roll, the filled-in lists shown read-only, and a
/// warning (with a fix button) for any battle map missing from the build's
/// scene list.
/// </summary>
[CustomEditor(typeof(EncounterPool))]
public class EncounterPoolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EncounterPool pool = (EncounterPool)target;

        serializedObject.Update();

        EditorGUILayout.LabelField("Folders", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("battleEncountersFolder"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("otherEncountersFolder"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("battleMapsFolder"), new GUIContent("Default Battle Maps Folder"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Encounter Groups", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("groups"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ungroupedWeight"));

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Chance Per Roll (beyond the minimums, before any limit is reached)", EditorStyles.boldLabel);

        foreach (KeyValuePair<string, float> chance in pool.GetChances())
        {
            EditorGUILayout.LabelField(chance.Key, $"{chance.Value * 100f:0.#}%");
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Filled In Automatically", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("battleEncounters"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("otherEncounters"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("battleMapPaths"), new GUIContent("Default Battle Maps"), true);
        }

        List<string> missing = new();

        foreach (string path in pool.GetAllBattleMapPaths())
        {
            if (!SceneFieldDrawer.IsInBuild(path))
            {
                missing.Add(path);
            }
        }

        if (missing.Count > 0)
        {
            EditorGUILayout.HelpBox($"{missing.Count} battle map(s) aren't in the build's scene list, so they can't be picked:\n{string.Join("\n", missing)}", MessageType.Warning);

            if (GUILayout.Button("Add Them to Build"))
            {
                foreach (string path in missing)
                {
                    SceneFieldDrawer.AddToBuild(path);
                }
            }
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Refresh"))
        {
            pool.Refresh();
        }
    }
}
