using UnityEngine;

/// <summary>
/// A scene picked in the Inspector by dragging the scene asset in. The name
/// is stored alongside it, since scene assets only exist in the Editor and
/// scenes are loaded by name at runtime. The Inspector also offers to add
/// the scene to the build's scene list if it's missing, since a scene that
/// isn't in it can't be loaded.
/// </summary>
[System.Serializable]
public class SceneField
{
    [SerializeField] private Object sceneAsset;
    [SerializeField] private string sceneName = "";

    public string SceneName => sceneName;
    public bool IsSet => !string.IsNullOrEmpty(sceneName);
}
