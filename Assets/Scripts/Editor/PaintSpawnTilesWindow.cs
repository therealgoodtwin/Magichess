using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene view tool for marking which tiles enemy pawns are allowed to spawn
/// on. Open via Tools > Regicide 2 > Paint Spawn Tiles, turn painting on,
/// then left-click or drag over tiles in the Scene view to mark them -
/// hold Shift while painting to unmark instead. Currently-marked tiles are
/// drawn with an orange marker at all times, even while painting is off, so
/// you can review the layout without risking an accidental edit.
/// </summary>
public class PaintSpawnTilesWindow : EditorWindow
{
    private bool isPainting;

    [MenuItem("Tools/Regicide 2/Paint Spawn Tiles")]
    private static void Open()
    {
        GetWindow<PaintSpawnTilesWindow>("Spawn Tile Painter");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Turn painting on, then left-click or drag over tiles in the Scene view " +
            "to mark them as spawn tiles. Hold Shift while painting to unmark instead. " +
            "Marked tiles always show an orange marker, even with painting off.",
            MessageType.Info);

        EditorGUILayout.Space();

        bool nowPainting = GUILayout.Toggle(
            isPainting, isPainting ? "Painting - click to stop" : "Start Painting", "Button", GUILayout.Height(28));

        if (nowPainting != isPainting)
        {
            isPainting = nowPainting;
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Mark Selected"))
            {
                SetSelectedTiles(true);
            }

            if (GUILayout.Button("Unmark Selected"))
            {
                SetSelectedTiles(false);
            }
        }

        if (GUILayout.Button("Clear All Spawn Tiles"))
        {
            if (EditorUtility.DisplayDialog(
                "Clear All Spawn Tiles",
                "Unmark every spawn tile in the open scene?",
                "Clear", "Cancel"))
            {
                foreach (Tile tile in FindObjectsByType<Tile>(FindObjectsSortMode.None))
                {
                    SetSpawnTile(tile, false);
                }
            }
        }

        int count = 0;

        foreach (Tile tile in FindObjectsByType<Tile>(FindObjectsSortMode.None))
        {
            if (tile.IsSpawnTile)
            {
                count++;
            }
        }

        EditorGUILayout.LabelField($"{count} tile(s) currently marked.");
    }

    private static void SetSelectedTiles(bool value)
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go.TryGetComponent(out Tile tile))
            {
                SetSpawnTile(tile, value);
            }
        }

        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        DrawMarkers();

        if (!isPainting)
        {
            return;
        }

        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        Event e = Event.current;

        switch (e.GetTypeForControl(controlId))
        {
            case EventType.Layout:
                // Claims this as the default control for the tool so normal
                // object picking/selection doesn't also fire on the same click.
                HandleUtility.AddDefaultControl(controlId);
                break;

            case EventType.MouseDown:
            case EventType.MouseDrag:
                if (e.button == 0 && !e.alt)
                {
                    PaintAtMouse(e);
                    e.Use();
                }

                break;
        }
    }

    private void PaintAtMouse(Event e)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit) || !hit.collider.TryGetComponent(out Tile tile))
        {
            return;
        }

        SetSpawnTile(tile, !e.shift);
        Repaint();
    }

    private static void DrawMarkers()
    {
        Handles.color = new Color(1f, 0.55f, 0.1f, 0.85f);

        foreach (Tile tile in FindObjectsByType<Tile>(FindObjectsSortMode.None))
        {
            if (!tile.IsSpawnTile)
            {
                continue;
            }

            // Not BaseWorldPosition: that's only valid once Awake() has run
            // and cached it, which never happens in the Editor outside Play
            // mode - transform.position is always accurate here instead,
            // and tiles aren't mid-highlight-lift while not playing anyway.
            Handles.CubeHandleCap(0, tile.transform.position, Quaternion.identity, 0.5f, EventType.Repaint);
        }
    }

    // Goes through SerializedObject/SerializedProperty rather than setting
    // the private field directly, so the edit plays nicely with prefab
    // instance overrides, scene dirtying and Undo like any other Inspector
    // change would.
    private static void SetSpawnTile(Tile tile, bool value)
    {
        SerializedObject serializedTile = new(tile);
        SerializedProperty property = serializedTile.FindProperty("isSpawnTile");

        if (property == null || property.boolValue == value)
        {
            return;
        }

        property.boolValue = value;
        serializedTile.ApplyModifiedProperties();
    }
}
