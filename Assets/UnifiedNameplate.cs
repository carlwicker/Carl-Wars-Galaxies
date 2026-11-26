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

    private GameObject backgroundObject;
    private MeshFilter backgroundMeshFilter;
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
        if (text == null || backgroundObject == null) return;

        text.ForceMeshUpdate();
        Bounds textBounds = text.textBounds;
        
        // Position background behind text
        backgroundObject.transform.localPosition = new Vector3(
            textBounds.center.x,
            textBounds.center.y,
            textBounds.center.z + 0.01f);
        
        // Scale background to fit text with padding
        backgroundObject.transform.localScale = new Vector3(
            Mathf.Max(textBounds.size.x + padding.x, 0.1f),
            Mathf.Max(textBounds.size.y + padding.y, 0.1f),
            1f);
        
        // Keep renderer enabled and color correct
        backgroundRenderer.enabled = !string.IsNullOrEmpty(text.text);
        if (backgroundMaterial != null)
        {
            backgroundMaterial.color = backgroundColor;
        }
    }

    private void EnsureBackground()
    {
        if (backgroundObject != null) return;

        // Check if already exists
        Transform existing = transform.Find("Nameplate Background");
        if (existing != null)
        {
            backgroundObject = existing.gameObject;
            backgroundRenderer = backgroundObject.GetComponent<MeshRenderer>();
            backgroundMeshFilter = backgroundObject.GetComponent<MeshFilter>();
            if (backgroundRenderer != null && backgroundMeshFilter != null) return;
        }

        // Create new background object
        backgroundObject = new GameObject("Nameplate Background");
        backgroundObject.transform.SetParent(transform, false);
        backgroundObject.transform.localPosition = Vector3.zero;
        backgroundObject.transform.localRotation = Quaternion.identity;

        // Add mesh filter and renderer
        backgroundMeshFilter = backgroundObject.AddComponent<MeshFilter>();
        backgroundRenderer = backgroundObject.AddComponent<MeshRenderer>();

        // Create a simple quad mesh
        Mesh quadMesh = new Mesh();
        quadMesh.name = "Nameplate Quad";
        quadMesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };
        quadMesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        quadMesh.uv = new Vector2[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        quadMesh.RecalculateNormals();
        backgroundMeshFilter.mesh = quadMesh;

        // Configure renderer
        backgroundRenderer.shadowCastingMode = ShadowCastingMode.Off;
        backgroundRenderer.receiveShadows = false;

        // Create material with black color
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        
        backgroundMaterial = new Material(shader);
        backgroundMaterial.name = "Nameplate Background Material";
        backgroundMaterial.color = backgroundColor;
        backgroundMaterial.renderQueue = 3000;
        
        backgroundRenderer.sharedMaterial = backgroundMaterial;
    }

    private void OnDestroy()
    {
        if (backgroundMaterial != null)
        {
            Destroy(backgroundMaterial);
        }
        if (backgroundObject != null)
        {
            Destroy(backgroundObject);
        }
    }
}
