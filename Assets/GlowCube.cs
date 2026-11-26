using UnityEngine;

public sealed class GlowCube : MonoBehaviour
{
    [SerializeField] private string cubeId = "glow-cube";
    [SerializeField, ColorUsage(true, true)] private Color glowColor = new Color(0.2f, 0.8f, 1f, 1f);
    [SerializeField] private float glowIntensity = 4f;
    [SerializeField] private float lightIntensity = 5f;
    [SerializeField] private float lightRange = 9f;

    private Renderer cubeRenderer;
    private Light glowLight;
    private Material[] runtimeMaterials;
    private bool isOn;

    public string CubeId => cubeId;
    public bool IsOn => isOn;
    public bool CanToggle => cubeRenderer != null && isActiveAndEnabled && gameObject.activeInHierarchy;

    private void Awake()
    {
        cubeRenderer = GetComponent<Renderer>();
        if (cubeRenderer == null)
        {
            Debug.LogError("GlowCube requires a Renderer.", this);
            return;
        }

        glowLight = GetComponent<Light>();
        if (glowLight == null) glowLight = gameObject.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = glowColor;
        glowLight.intensity = lightIntensity;
        glowLight.range = lightRange;
        glowLight.enabled = false;

        runtimeMaterials = cubeRenderer.materials;
        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            if (runtimeMaterials[i].HasProperty("_EmissionColor"))
            {
                runtimeMaterials[i].EnableKeyword("_EMISSION");
                runtimeMaterials[i].SetColor("_EmissionColor", Color.black);
            }
        }
    }

    public void Initialize(string newCubeId, Color newGlowColor)
    {
        cubeId = newCubeId;
        glowColor = newGlowColor;
        if (glowLight != null) glowLight.color = glowColor;
        SetGlow(false);
    }

    public void SetGlow(bool enabled)
    {
        isOn = enabled;
        if (glowLight != null) glowLight.enabled = enabled;
        if (runtimeMaterials == null) return;

        Color emission = enabled ? glowColor * glowIntensity : Color.black;
        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            if (runtimeMaterials[i].HasProperty("_EmissionColor"))
            {
                runtimeMaterials[i].SetColor("_EmissionColor", emission);
            }
        }
    }

    private void OnDestroy()
    {
        if (runtimeMaterials == null) return;

        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            if (runtimeMaterials[i] != null)
            {
                Destroy(runtimeMaterials[i]);
            }
        }
    }
}
