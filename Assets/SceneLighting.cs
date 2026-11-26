using UnityEngine;
using UnityEngine.Rendering;

public sealed class SceneLighting : MonoBehaviour
{
    private void Awake()
    {
        SetupSceneLighting();
    }

    private void SetupSceneLighting()
    {
        // MAXIMUM BRIGHTNESS SETTINGS
        
        // Set render settings for MAXIMUM BRIGHTNESS
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;
        RenderSettings.ambientIntensity = 5f; // VERY BRIGHT
        
        // Disable reflections that might darken
        RenderSettings.reflectionIntensity = 0f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

        // Destroy ALL existing lights to avoid conflicts
        Light[] allLights = FindObjectsOfType<Light>(true);
        foreach (Light light in allLights)
        {
            if (light.gameObject.name != "Sun")
            {
                Destroy(light.gameObject);
            }
        }

        // Create or find the Sun
        Light sunLight = FindObjectOfType<Light>();
        if (sunLight == null || sunLight.gameObject.name != "Sun")
        {
            // Clear all lights first
            foreach (Light light in FindObjectsOfType<Light>())
            {
                Destroy(light.gameObject);
            }
            
            // Create new Sun
            GameObject sunObject = new GameObject("Sun");
            sunLight = sunObject.AddComponent<Light>();
        }

        // Set Sun to MAXIMUM brightness
        sunLight.type = LightType.Directional;
        sunLight.intensity = 5f; // VERY BRIGHT
        sunLight.color = Color.white;
        sunLight.transform.rotation = Quaternion.Euler(45f, 45f, 0f);
        sunLight.shadows = LightShadows.None; // Disable shadows for maximum brightness
        
        Debug.Log($"Scene lighting reset - Ambient: {RenderSettings.ambientIntensity}, Sun: {sunLight.intensity}");
    }
}
