using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Full-screen wipe transition that lives across scene loads. It is created
/// automatically before the first scene loads, starts fully covering the
/// screen, and reveals every scene as it loads - so every scene begins with
/// the transition played in reverse. LoadScene() covers the screen, loads the
/// scene, then reveals it.
/// </summary>
public class ScreenWipe : MonoBehaviour
{
    public enum Style
    {
        Circle,
        Diamond,
        Blinds
    }

    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int StyleId = Shader.PropertyToID("_Style");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private const float DefaultDuration = 0.8f;

    private static ScreenWipe instance;

    private Material material;
    private bool busy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        GetInstance();
    }

    private static ScreenWipe GetInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject root = new GameObject("Screen Wipe");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<ScreenWipe>();
        instance.Build();
        return instance;
    }

    // True while a wipe is covering, loading or revealing. LoadScene() is
    // ignored until it's false again.
    public static bool IsBusy => instance != null && instance.busy;

    // The default look: a black circle wipe at the standard speed both ways.
    public static void LoadScene(string sceneName)
    {
        LoadScene(sceneName, Style.Circle, DefaultDuration, DefaultDuration, Color.black);
    }

    public static void LoadScene(string sceneName, Style style, float closeDuration, float openDuration, Color color)
    {
        ScreenWipe wipe = GetInstance();

        if (!wipe.busy)
        {
            wipe.StartCoroutine(wipe.Run(sceneName, style, closeDuration, openDuration, color));
        }
    }

    private void Build()
    {
        Shader shader = Resources.Load<Shader>("ScreenWipe");
        material = new Material(shader != null ? shader : Shader.Find("UI/Default"));

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        GameObject imageObject = new GameObject("Wipe");
        imageObject.transform.SetParent(transform, false);

        RawImage image = imageObject.AddComponent<RawImage>();
        image.material = material;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        SetLook(Style.Circle, Color.black);
        SetProgress(1f);

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (instance == this)
        {
            instance = null;
        }
    }

    // A scene that wasn't loaded through LoadScene() (the very first one,
    // or one opened by other code) still starts covered and is revealed.
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!busy)
        {
            StartCoroutine(RevealOnly());
        }
    }

    private IEnumerator RevealOnly()
    {
        busy = true;
        yield return WaitFrames(3);
        yield return Animate(1f, 0f, DefaultDuration);
        busy = false;
    }

    private static IEnumerator WaitFrames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return null;
        }
    }

    private IEnumerator Run(string sceneName, Style style, float closeDuration, float openDuration, Color color)
    {
        busy = true;
        SetLook(style, color);

        yield return Animate(0f, 1f, closeDuration);

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);

        if (load != null)
        {
            yield return load;
        }
        else
        {
            Debug.LogError($"ScreenWipe: could not load scene '{sceneName}' - is it in Build Settings?");
        }

        // Let the new scene's Awake/Start calls (and the hitchy first frames
        // that follow them) finish under the cover before revealing.
        yield return WaitFrames(3);
        yield return Animate(1f, 0f, openDuration);

        busy = false;
    }

    private IEnumerator Animate(float from, float to, float duration)
    {
        SetProgress(from);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Capped: the first frames after a heavy scene load can take
            // seconds, which would otherwise finish the whole reveal in one
            // frame and make it look like there was no transition at all.
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            SetProgress(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration))));
            yield return null;
        }

        SetProgress(to);
    }

    private void SetLook(Style style, Color color)
    {
        material.SetFloat(StyleId, (float)style);
        material.SetColor(ColorId, color);
    }

    private void SetProgress(float progress)
    {
        material.SetFloat(ProgressId, progress);
        material.SetFloat(AspectId, Screen.width / (float)Mathf.Max(1, Screen.height));
    }
}
