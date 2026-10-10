using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

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

                // An empty material slot can't be grouped by, so that part is
                // left out rather than stopping the whole combine.
                if (material == null)
                {
                    Debug.LogWarning($"CombineChildMeshesTool: '{filter.name}' has an empty material slot - that part of it was left out.", filter);
                    continue;
                }

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
        long totalVertices = 0;

        for (int i = 0; i < materialOrder.Count; i++)
        {
            List<CombineInstance> combines = byMaterial[materialOrder[i]];

            Mesh combined = new() { indexFormat = GetIndexFormat(combines.Sum(combine => (long)combine.mesh.vertexCount)) };
            combined.CombineMeshes(combines.ToArray(), true, true);
            perMaterialMeshes[i] = combined;
            totalVertices += combined.vertexCount;
        }

        Mesh finalMesh = new() { name = $"{root.name} (Combined)", indexFormat = GetIndexFormat(totalVertices) };

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

        // A parent that already has a mesh of its own - from an earlier
        // combine, say - gets it replaced rather than a second set added,
        // which Unity doesn't allow.
        bool replacedExisting = root.TryGetComponent(out MeshFilter rootFilter);

        if (replacedExisting)
        {
            Undo.RecordObject(rootFilter, "Combine Child Meshes");
        }
        else
        {
            rootFilter = Undo.AddComponent<MeshFilter>(root);
        }

        rootFilter.sharedMesh = finalMesh;

        if (root.TryGetComponent(out MeshRenderer rootRenderer))
        {
            Undo.RecordObject(rootRenderer, "Combine Child Meshes");
        }
        else
        {
            rootRenderer = Undo.AddComponent<MeshRenderer>(root);
        }

        rootRenderer.sharedMaterials = materialOrder.ToArray();

        Undo.CollapseUndoOperations(undoGroup);

        // The scene the parent is actually in - in Prefab Mode that's the
        // prefab being edited, not whichever scene is open behind it.
        EditorSceneManager.MarkSceneDirty(root.scene);

        if (replacedExisting)
        {
            Debug.Log($"CombineChildMeshesTool: '{root.name}' already had a mesh of its own - it was replaced by the combined one.", root);
        }

        Debug.Log(
            $"CombineChildMeshesTool: combined {childFilters.Length} child mesh(es) into " +
            $"'{Path.GetFileName(assetPath)}' ({finalMesh.vertexCount:N0} vertices) and added it to '{root.name}'. " +
            "Review it, then delete or disable the original child pieces.",
            root
        );
    }

    // A mesh holds 65,535 vertices at most unless it's switched to 32-bit
    // indices, which cost a little more memory - so only when it's needed.
    private static IndexFormat GetIndexFormat(long vertexCount)
    {
        return vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
    }
}
