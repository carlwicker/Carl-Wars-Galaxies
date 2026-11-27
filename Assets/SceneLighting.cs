using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Light))]
public sealed class SceneLighting : MonoBehaviour
{
    private const double DaySeconds = 20 * 60;
    private const double CycleSeconds = 2 * DaySeconds;
    private const double TransitionSeconds = 2 * 60;

    private Light sunLight;
    private float daytimeSunIntensity;
    private Color daytimeAmbient;
    private double sceneStartTime;

    private void Awake()
    {
        sunLight = GetComponent<Light>();
        if (sunLight.type != LightType.Directional)
        {
            Debug.LogError("SceneLighting requires a directional light.", this);
            enabled = false;
            return;
        }

        daytimeSunIntensity = sunLight.intensity;
        daytimeAmbient = RenderSettings.ambientSkyColor;
        sceneStartTime = Time.realtimeSinceStartupAsDouble;
        RenderSettings.ambientMode = AmbientMode.Flat;
        ApplyDaylight(1f);
    }

    private void Update()
    {
        NetworkManager manager = NetworkManager.Singleton;
        double elapsed = manager != null && manager.IsListening && (manager.IsServer || manager.IsConnectedClient)
            ? manager.ServerTime.Time
            : Time.realtimeSinceStartupAsDouble - sceneStartTime;

        // Start at midday; network sessions share the host's clock, including late joiners.
        ApplyDaylight(DaylightAtTime(elapsed + DaySeconds / 2));
    }

    private static float DaylightAtTime(double seconds)
    {
        double timeOfCycle = ((seconds % CycleSeconds) + CycleSeconds) % CycleSeconds;
        if (timeOfCycle >= DaySeconds)
        {
            return 0f;
        }

        double fade = System.Math.Min(timeOfCycle / TransitionSeconds, (DaySeconds - timeOfCycle) / TransitionSeconds);
        return Mathf.SmoothStep(0f, 1f, (float)System.Math.Min(fade, 1d));
    }

    private void ApplyDaylight(float daylight)
    {
        sunLight.intensity = daytimeSunIntensity * daylight;
        RenderSettings.ambientLight = Color.Lerp(Color.black, daytimeAmbient, daylight);
    }
}
