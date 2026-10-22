using UnityEngine;

/// <summary>
/// Slowly turns an object around the vertical (Y) axis while it bobs
/// smoothly up and down around where it was placed - for decorations like
/// the crystals. Each copy starts at a random point in its bob unless told
/// otherwise, so a group of them doesn't move in lockstep.
/// </summary>
public class SpinAndFloat : MonoBehaviour
{
    [Header("Spin")]
    [Tooltip("Degrees turned per second around the Y axis. Negative turns the other way; 0 doesn't turn at all.")]
    [SerializeField] private float spinSpeed = 20f;

    [Header("Float")]
    [Tooltip("How far above and below its placed height it floats, in world units.")]
    [SerializeField, Min(0f)] private float floatHeight = 0.25f;

    [Tooltip("Seconds for one full float, up and back down again.")]
    [SerializeField, Min(0.01f)] private float floatDuration = 3f;

    [Tooltip("Start each copy at a random point in its float, so a group of them doesn't rise and fall together.")]
    [SerializeField] private bool randomStart = true;

    // Where it was placed, which it floats around.
    private Vector3 startPosition;
    private float phase;

    private void Awake()
    {
        startPosition = transform.position;
        phase = randomStart ? Random.value * Mathf.PI * 2f : 0f;
    }

    private void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

        float wave = Mathf.Sin(Time.time * Mathf.PI * 2f / floatDuration + phase);
        transform.position = startPosition + Vector3.up * (wave * floatHeight);
    }
}
