using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class VehicleHoverDustTests
{
    private GameObject vehicleObject;
    private GameObject ground;
    private Component vehicle;
    private Type vehicleType;
    private object power;
    private ParticleSystem dust;
    private readonly Vector3 origin = new Vector3(12000f, 0f, 12000f);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        vehicleType = Type.GetType("Vehicle, Assembly-CSharp");
        Assert.That(vehicleType, Is.Not.Null);
        vehicleObject = new GameObject("Test Vehicle");
        vehicleObject.transform.position = origin;
        vehicle = vehicleObject.AddComponent(vehicleType);
        power = vehicleType.GetField("poweredOn", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(vehicle);
        Invoke("CreateHoverDust");
        dust = vehicleObject.GetComponentInChildren<ParticleSystem>();
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        UnityEngine.Object.Destroy(ground);
        UnityEngine.Object.Destroy(vehicleObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DustFollowsPowerAndUsesGround()
    {
        ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.transform.position = origin + Vector3.down * 0.5f;
        ground.transform.localScale = new Vector3(20f, 1f, 20f);
        Physics.SyncTransforms();
        SetPower(true);
        EmitDust();

        Assert.That(dust.particleCount, Is.GreaterThan(0));
        ParticleSystem.Particle[] particles = new ParticleSystem.Particle[dust.particleCount];
        int count = dust.GetParticles(particles);
        for (int i = 0; i < count; i++)
        {
            Assert.That(particles[i].position.y, Is.InRange(0.05f, 0.3f));
            Assert.That(particles[i].position.x - origin.x, Is.InRange(-1.1f, 1.1f));
            Assert.That(particles[i].position.z - origin.z, Is.InRange(-2.6f, 2.6f));
        }

        SetPower(false);
        Invoke("UpdateHoverDust");
        Assert.That(dust.particleCount, Is.Zero);
        Assert.That(dust.isPlaying, Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DustDoesNotAppearWithoutGround()
    {
        SetPower(true);
        EmitDust();
        Assert.That(dust.particleCount, Is.Zero);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DustGrowsAndFadesOverItsLifetime()
    {
        ParticleSystemRenderer renderer = dust.GetComponent<ParticleSystemRenderer>();
        Assert.That(renderer.enableGPUInstancing, Is.False);
        Assert.That(renderer.sharedMaterial.GetFloat("_Surface"), Is.EqualTo(1f));
        Assert.That(renderer.sharedMaterial.GetFloat("_ZWrite"), Is.Zero);

        dust.Play();
        dust.Emit(new ParticleSystem.EmitParams { startLifetime = 1f, startSize = 0.2f }, 1);
        ParticleSystem.Particle[] particles = new ParticleSystem.Particle[1];
        dust.Simulate(0.1f, true, false, false);
        Assert.That(dust.GetParticles(particles), Is.EqualTo(1));
        float initialSize = particles[0].GetCurrentSize(dust);
        byte initialAlpha = particles[0].GetCurrentColor(dust).a;

        dust.Simulate(0.65f, true, false, false);
        Assert.That(dust.GetParticles(particles), Is.EqualTo(1));
        Assert.That(particles[0].GetCurrentSize(dust), Is.GreaterThan(initialSize));
        Assert.That(particles[0].GetCurrentColor(dust).a, Is.LessThan(initialAlpha));
        yield return null;
    }

    private void EmitDust()
    {
        vehicleType.GetField("dustEmissionRemainder", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(vehicle, 1f);
        Invoke("UpdateHoverDust");
    }

    private void SetPower(bool value)
    {
        power.GetType().GetProperty("Value").SetValue(power, value);
    }

    private void Invoke(string method)
    {
        vehicleType.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(vehicle, null);
    }
}
