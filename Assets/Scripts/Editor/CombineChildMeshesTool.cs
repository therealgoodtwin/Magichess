using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Bakes a GameObject built from several separate mesh children into a
/// single mesh on that GameObject itself, replacing the multi-child
/// hierarchy with one MeshFilter/MeshRenderer pair.
/// </summary>
public static class CombineChildMeshesTool
{
    [MenuItem("Tools/Regicide 2/Combine Selected Children Into One Mesh")]
    private static void CombineSelected()
    {
        GameObject root = Selection.activeGameObject;

        if (root == null)
        {
            Debug.LogWarning("CombineChildMeshesTool: select the parent GameObject first.");
            return;
        }

        MeshFilter[] childFilters = root
            .GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter.gameObject != root)
            .ToArray();

        if (childFilters.Length == 0)
        {
            Debug.LogWarning($"CombineChildMeshesTool: no child meshes found under '{root.name}'.", root);
            return;
        }

        // Group each child's mesh (in the root's local space) by material,
        // so multiple materials survive the combine as separate submeshes.
        Dictionary<Material, List<CombineInstance>> byMaterial = new();

        foreach (MeshFilter filter in childFilters)
        {
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();

            if (renderer == null || filter.sharedMesh == null)
            {
                continue;
            }

            Matrix4x4 localMatrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;

            Material[] materials = renderer.sharedMaterials;

            for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
            {
                Material material = materials.Length > submesh ? materials[submesh] : materials.LastOrDefault();

                if (!byMaterial.TryGetValue(material, out List<CombineInstance> combines))
                {
                    combines = new List<CombineInstance>();
                    byMaterial[material] = combines;
                }

                combines.Add(new CombineInstance
                {
                    mesh = filter.sharedMesh,
                    subMeshIndex = submesh,
                    transform = localMatrix
                });
            }
        }

        List<Material> materialOrder = byMaterial.Keys.ToList();
        Mesh[] perMaterialMeshes = new Mesh[materialOrder.Count];

        for (int i = 0; i < materialOrder.Count; i++)
        {
            Mesh combined = new();
            combined.CombineMeshes(byMaterial[materialOrder[i]].ToArray(), true, true);
            perMaterialMeshes[i] = combined;
        }

        Mesh finalMesh = new() { name = $"{root.name} (Combined)" };

        CombineInstance[] finalCombines = perMaterialMeshes
            .Select(mesh => new CombineInstance { mesh = mesh })
            .ToArray();

        finalMesh.CombineMeshes(finalCombines, false, false);

        const string folder = "Assets/CombinedMeshes";

        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "CombinedMeshes");
        }

        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{root.name}.asset");
        AssetDatabase.CreateAsset(finalMesh, assetPath);
        AssetDatabase.SaveAssets();

        Undo.SetCurrentGroupName("Combine Child Meshes");
        int undoGroup = Undo.GetCurrentGroup();

        MeshFilter rootFilter = Undo.AddComponent<MeshFilter>(root);
        rootFilter.sharedMesh = finalMesh;

        MeshRenderer rootRenderer = Undo.AddComponent<MeshRenderer>(root);
        rootRenderer.sharedMaterials = materialOrder.ToArray();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log(
            $"CombineChildMeshesTool: combined {childFilters.Length} child mesh(es) into " +
            $"'{Path.GetFileName(assetPath)}' and added it to '{root.name}'. " +
            "Review it, then delete or disable the original child pieces.",
            root
        );
    }
}
