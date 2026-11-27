using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NameplateBackground))]
public sealed class UnifiedNameplate : MonoBehaviour
{
    private TMP_Text text;
    private NameplateBackground background;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
        background = GetComponent<NameplateBackground>();
        if (text == null)
        {
            Debug.LogError("UnifiedNameplate requires TMP_Text.", this);
            enabled = false;
            return;
        }

        text.color = Color.white;
        Refresh();
    }

    public void Refresh()
    {
        if (text == null || background == null) return;
        background.Refresh();
    }
}
