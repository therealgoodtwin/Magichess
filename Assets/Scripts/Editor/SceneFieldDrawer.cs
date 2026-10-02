using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws a SceneField as a scene asset slot, keeping its stored name in step
/// with the asset (including after a rename). Shows an "Add to Build" button
/// next to it while the scene is missing from the build's scene list.
/// </summary>
[CustomPropertyDrawer(typeof(SceneField))]
public class SceneFieldDrawer : PropertyDrawer
{
    private const float ButtonWidth = 90f;
    private const float Spacing = 4f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty assetProperty = property.FindPropertyRelative("sceneAsset");
        SerializedProperty nameProperty = property.FindPropertyRelative("sceneName");

        EditorGUI.BeginProperty(position, label, property);

        SceneAsset scene = assetProperty.objectReferenceValue as SceneAsset;
        string path = scene != null ? AssetDatabase.GetAssetPath(scene) : null;
        bool missingFromBuild = scene != null && !IsInBuild(path);

        Rect fieldRect = position;

        if (missingFromBuild)
        {
            fieldRect.width -= ButtonWidth + Spacing;
        }

        EditorGUI.BeginChangeCheck();
        scene = (SceneAsset)EditorGUI.ObjectField(fieldRect, label, scene, typeof(SceneAsset), false);

        if (EditorGUI.EndChangeCheck())
        {
            assetProperty.objectReferenceValue = scene;
        }

        string expectedName = scene != null ? scene.name : "";

        if (nameProperty.stringValue != expectedName)
        {
            nameProperty.stringValue = expectedName;
        }

        if (missingFromBuild)
        {
            Rect buttonRect = new Rect(fieldRect.xMax + Spacing, position.y, ButtonWidth, EditorGUIUtility.singleLineHeight);
            GUIContent buttonLabel = new GUIContent("Add to Build", "This scene isn't in the build's scene list (File > Build Profiles), so it can't be loaded. Click to add it.");

            if (GUI.Button(buttonRect, buttonLabel))
            {
                AddToBuild(path);
            }
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }

    private static bool IsInBuild(string path)
    {
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (buildScene.path == path && buildScene.enabled)
            {
                return true;
            }
        }

        return false;
    }

    private static void AddToBuild(string path)
    {
        List<EditorBuildSettingsScene> scenes = new(EditorBuildSettings.scenes);
        int existing = scenes.FindIndex(buildScene => buildScene.path == path);

        if (existing >= 0)
        {
            scenes[existing].enabled = true;
        }
        else
        {
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
