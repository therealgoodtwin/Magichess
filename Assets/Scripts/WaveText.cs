using TMPro;
using UnityEngine;

/// <summary>
/// Keeps a TextMeshPro object reading "Wave: X" in sync with WaveManager's
/// current wave number.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class WaveText : MonoBehaviour
{
    private TMP_Text text;

    private void OnEnable()
    {
        text = GetComponent<TMP_Text>();
        WaveManager.WaveStarted += HandleWaveStarted;
    }

    private void OnDisable()
    {
        WaveManager.WaveStarted -= HandleWaveStarted;
    }

    // WaveManager.Start() (which fires the first WaveStarted) isn't
    // guaranteed to run before this object's own Start() - Unity only
    // guarantees every OnEnable finishes before any Start() runs, not an
    // ordering between different objects' Start() calls. Reading the
    // already-set CurrentWaveNumber here instead covers both orderings.
    private void Start()
    {
        HandleWaveStarted(WaveManager.CurrentWaveNumber);
    }

    private void HandleWaveStarted(int waveNumber)
    {
        text.text = $"Wave: {waveNumber}";
    }
}
