using TMPro;
using UnityEngine;

/// <summary>
/// Keeps a TextMeshPro object in sync with XPManager's running total.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class XPText : MonoBehaviour
{
    private TMP_Text text;

    private void OnEnable()
    {
        text = GetComponent<TMP_Text>();
        XPManager.XPChanged += HandleXPChanged;
    }

    private void OnDisable()
    {
        XPManager.XPChanged -= HandleXPChanged;
    }

    // XPManager's total may already have been updated by something whose
    // Start() ran before this object's - reading it directly here (rather
    // than relying solely on the event) covers both orderings.
    private void Start()
    {
        HandleXPChanged(XPManager.TotalXP);
    }

    private void HandleXPChanged(int total)
    {
        text.text = total.ToString();
    }
}
