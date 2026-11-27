using TMPro;
using UnityEngine;

public sealed class CraftingTerminal : MonoBehaviour
{
    public const float InteractionRange = 5f;

    private void Awake()
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Terminal body";
        body.transform.SetParent(transform, false);
        body.transform.localPosition = Vector3.up;
        body.transform.localScale = new Vector3(1.2f, 2f, 0.8f);
        body.GetComponent<Renderer>().material.color = new Color(0.2f, 0.5f, 0.7f);

        GameObject label = new GameObject("Terminal nameplate");
        label.transform.SetParent(transform, false);
        label.transform.localPosition = Vector3.up * 2.5f;
        label.transform.localScale = Vector3.one * 0.08f;
        TextMeshPro text = label.AddComponent<TextMeshPro>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 32;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(20, 3);
        text.text = "Crafting Terminal";
        label.AddComponent<UnifiedNameplate>();
        label.AddComponent<LookAtCamera>();
    }

    public bool IsWithinRange(Vector3 position)
    {
        return (position - transform.position).sqrMagnitude <= InteractionRange * InteractionRange;
    }
}
