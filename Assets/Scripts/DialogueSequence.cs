using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A reusable typewriter dialogue box. Put this on any object with a
/// TMP_Text component (works with both the UI and the 3D world-space
/// TextMeshPro, like the pop-up text boxes in the Tutorial scene) and fill in
/// Lines - each one types itself out a character at a time, then waits for
/// the player to left click (anywhere - not a specific button) before moving
/// on to the next. Clicking while a line is still typing instantly completes
/// that line instead of advancing, same as most dialogue systems; a second
/// click then moves on.
///
/// With Play On Enable checked (the default), just SetActive(true) the
/// object to run its whole line sequence, and it deactivates itself again
/// once the last line has been advanced past - so a tutorial step can simply
/// turn a pop-up on and listen for SequenceCompleted to know when it's done.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class DialogueSequence : MonoBehaviour
{
    private enum EndBehaviour
    {
        Nothing,
        Disable
    }

    [Tooltip("Lines played in order. Each one types out, then waits for a left click before the next one starts.")]
    [TextArea(2, 4)]
    [SerializeField] private string[] lines;

    [Tooltip("Characters revealed per second while a line is typing.")]
    [SerializeField] private float charactersPerSecond = 30f;

    [Tooltip("Starts the sequence automatically as soon as this object becomes active. Leave off to start it yourself by calling Play().")]
    [SerializeField] private bool playOnEnable = true;

    [Tooltip("What this object does once the last line has been advanced past.")]
    [SerializeField] private EndBehaviour endBehaviour = EndBehaviour.Disable;

    // Fired once, right after the last line is advanced past.
    public event System.Action SequenceCompleted;

    // Fired every time a new line starts typing, with its index into Lines.
    public event System.Action<int> LineStarted;

    public bool IsPlaying { get; private set; }

    private TMP_Text text;
    private Coroutine typingRoutine;
    private int lineIndex = -1;
    private bool isTyping;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        typingRoutine = null;
        isTyping = false;
    }

    private void Update()
    {
        if (!IsPlaying)
        {
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Advance();
        }
    }

    // Starts the sequence from its first line. Safe to call while it's
    // already playing (e.g. Play On Enable already started it the instant
    // this object was activated, and something else also calls Play()
    // right after) - a second call is simply ignored instead of resetting
    // and colliding with the line already typing itself out.
    public void Play()
    {
        if (IsPlaying || lines == null || lines.Length == 0)
        {
            return;
        }

        gameObject.SetActive(true);
        IsPlaying = true;
        lineIndex = -1;
        Advance();
    }

    // Ends the sequence immediately, wherever it currently is - completes
    // the line in progress (if any) without waiting for it, and fires
    // SequenceCompleted the same as reaching the end normally. For a
    // scripted moment that needs this dialogue gone right now, rather than
    // waiting for the player to click through the rest of it - works even
    // while this component is disabled (e.g. after it's been locked out to
    // stop clicks from advancing further).
    public void FinishNow()
    {
        if (!IsPlaying)
        {
            return;
        }

        CompleteLine();
        Finish();
    }

    // While a line is still typing, finishes it instantly instead of
    // advancing. Otherwise moves on to the next line, or ends the sequence
    // once the last one has already been shown in full.
    public void Advance()
    {
        if (!IsPlaying)
        {
            return;
        }

        if (isTyping)
        {
            CompleteLine();
            return;
        }

        lineIndex++;

        if (lineIndex >= lines.Length)
        {
            Finish();
            return;
        }

        LineStarted?.Invoke(lineIndex);
        typingRoutine = StartCoroutine(TypeLine(lines[lineIndex]));
    }

    private IEnumerator TypeLine(string line)
    {
        isTyping = true;
        text.text = line;
        text.maxVisibleCharacters = 0;
        text.ForceMeshUpdate();

        int totalCharacters = text.textInfo.characterCount;
        float secondsPerCharacter = charactersPerSecond > 0f ? 1f / charactersPerSecond : 0f;
        float elapsed = 0f;

        while (text.maxVisibleCharacters < totalCharacters)
        {
            elapsed += Time.deltaTime;
            int visible = secondsPerCharacter > 0f ? Mathf.FloorToInt(elapsed / secondsPerCharacter) : totalCharacters;
            text.maxVisibleCharacters = Mathf.Min(visible, totalCharacters);
            yield return null;
        }

        isTyping = false;
        typingRoutine = null;
    }

    // Snaps the current line's full text into view immediately, whether
    // called mid-type (by Advance) or any other time it's needed.
    private void CompleteLine()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        if (lineIndex >= 0 && lineIndex < lines.Length)
        {
            text.maxVisibleCharacters = text.textInfo.characterCount;
        }

        isTyping = false;
    }

    private void Finish()
    {
        IsPlaying = false;
        SequenceCompleted?.Invoke();

        if (endBehaviour == EndBehaviour.Disable)
        {
            gameObject.SetActive(false);
        }
    }
}
