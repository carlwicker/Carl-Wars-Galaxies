using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Unified nameplate system for all world objects (players, vehicles, pickups, etc).
/// Provides consistent styling: black background with white text.
/// </summary>
public sealed class UnifiedNameplate : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color backgroundColor = Color.black;
    [SerializeField] private Vector2 padding = new Vector2(0.5f, 0.3f);

    private Transform backgroundTransform;
    private MeshRenderer backgroundRenderer;
    private Material backgroundMaterial;

    private void Awake()
    {
        if (text == null) text = GetComponent<TMP_Text>();
        if (text == null)
        {
            Debug.LogError("UnifiedNameplate requires TMP_Text.", this);
            enabled = false;
            return;
        }

        // Ensure text has white color
        text.color = textColor;

        EnsureBackground();
        Refresh();
    }

    /// <summary>
    /// Refresh the background to match current text size.
    /// Call this after changing text.
    /// </summary>
    public void Refresh()
    {
        if (text == null || !EnsureBackground()) return;

        text.ForceMeshUpdate();
        Bounds textBounds = text.textBounds;
        backgroundTransform.localPosition = new Vector3(
            textBounds.center.x,
            textBounds.center.y,
            textBounds.center.z + 0.1f);
        backgroundTransform.localScale = new Vector3(
            Mathf.Max(textBounds.size.x + padding.x, 0.1f),
            Mathf.Max(textBounds.size.y + padding.y, 0.1f),
            1f);
        backgroundRenderer.enabled = !string.IsNullOrEmpty(text.text);
    }

    private bool EnsureBackground()
    {
        if (backgroundTransform != null) return true;

        Transform existingBackground = transform.Find("Nameplate Background");
        if (existingBackground != null)
        {
            backgroundTransform = existingBackground;
            backgroundRenderer = existingBackground.GetComponent<MeshRenderer>();
            if (backgroundRenderer != null) return true;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogError("Could not find a shader for nameplate backgrounds.", this);
            return false;
        }

        GameObject background = GameObject.CreatePrimitive(PrimitiveType.Quad);
        background.name = "Nameplate Background";
        background.transform.SetParent(transform, false);
        Collider backgroundCollider = background.GetComponent<Collider>();
        if (backgroundCollider != null) Destroy(backgroundCollider);

        backgroundTransform = background.transform;
        backgroundRenderer = background.GetComponent<MeshRenderer>();
        backgroundRenderer.shadowCastingMode = ShadowCastingMode.Off;
        backgroundRenderer.receiveShadows = false;
        backgroundMaterial = new Material(shader);
        backgroundMaterial.color = backgroundColor;
        backgroundMaterial.renderQueue = 3000;
        backgroundRenderer.sharedMaterial = backgroundMaterial;
        return true;
    }

    private void OnDestroy()
    {
        if (backgroundMaterial != null)
        {
            Destroy(backgroundMaterial);
        }
    }
}
