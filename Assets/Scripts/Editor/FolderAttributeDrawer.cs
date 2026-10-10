using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws a [Folder] field as a slot that accepts folders and nothing else.
/// </summary>
[CustomPropertyDrawer(typeof(FolderAttribute))]
public class FolderAttributeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ObjectReference)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();

        Object folder = EditorGUI.ObjectField(position, label, property.objectReferenceValue, typeof(DefaultAsset), false);

        if (EditorGUI.EndChangeCheck() && (folder == null || AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(folder))))
        {
            property.objectReferenceValue = folder;
        }

        EditorGUI.EndProperty();
    }
}
