using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public class SceneLightingTests
{
    [TestCase(0d, 0f)]
    [TestCase(60d, 0.5f)]
    [TestCase(120d, 1f)]
    [TestCase(600d, 1f)]
    [TestCase(1080d, 1f)]
    [TestCase(1140d, 0.5f)]
    [TestCase(1200d, 0f)]
    [TestCase(1800d, 0f)]
    [TestCase(2399d, 0f)]
    [TestCase(2400d, 0f)]
    [TestCase(2460d, 0.5f)]
    public void DaylightFadesWithinTwentyMinuteDayAndStaysBlackAllNight(double seconds, float expected)
    {
        Type type = Type.GetType("SceneLighting, Assembly-CSharp");
        Assert.That(type, Is.Not.Null);
        MethodInfo evaluate = type.GetMethod("DaylightAtTime", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(evaluate, Is.Not.Null);
        Assert.That((float)evaluate.Invoke(null, new object[] { seconds }), Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void NightTurnsOffEnvironmentalLightWithoutChangingLocalLights()
    {
        Type type = Type.GetType("SceneLighting, Assembly-CSharp");
        Assert.That(type, Is.Not.Null);
        AmbientMode originalMode = RenderSettings.ambientMode;
        Color originalSky = RenderSettings.ambientSkyColor;
        Color originalEquator = RenderSettings.ambientEquatorColor;
        Color originalGround = RenderSettings.ambientGroundColor;
        GameObject sunObject = new GameObject("Test sun");
        GameObject localObject = new GameObject("Test local light");
        try
        {
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 5f;
            Light local = localObject.AddComponent<Light>();
            local.type = LightType.Point;
            local.intensity = 3f;
            Component cycle = sunObject.AddComponent(type);
            MethodInfo apply = type.GetMethod("ApplyDaylight", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(apply, Is.Not.Null);

            apply.Invoke(cycle, new object[] { 0f });
            Assert.That(sun.intensity, Is.Zero);
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(Color.black));
            Assert.That(local.intensity, Is.EqualTo(3f));
            Assert.That(local.enabled, Is.True);

            apply.Invoke(cycle, new object[] { 1f });
            Assert.That(sun.intensity, Is.EqualTo(5f));
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(originalSky));
        }
        finally
        {
            Object.DestroyImmediate(sunObject);
            Object.DestroyImmediate(localObject);
            RenderSettings.ambientMode = originalMode;
            RenderSettings.ambientSkyColor = originalSky;
            RenderSettings.ambientEquatorColor = originalEquator;
            RenderSettings.ambientGroundColor = originalGround;
        }
    }
}
