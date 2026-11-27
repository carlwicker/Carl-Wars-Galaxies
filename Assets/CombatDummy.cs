using TMPro;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class CombatDummy : NetworkBehaviour
{
    public const float MaximumPool = 100f;
    private const float RespawnSeconds = 8f;
    private readonly NetworkVariable<float> health = new NetworkVariable<float>(MaximumPool);
    private readonly NetworkVariable<float> action = new NetworkVariable<float>(MaximumPool);
    private readonly NetworkVariable<float> mind = new NetworkVariable<float>(MaximumPool);
    private readonly NetworkVariable<bool> defeated = new NetworkVariable<bool>();
    private GameObject body;
    private float respawnAt;

    public float Health => health.Value;
    public float Action => action.Value;
    public float Mind => mind.Value;
    public bool IsDefeated => defeated.Value;

    private void Awake()
    {
        Transform existing = transform.Find("Training Dummy Capsule");
        if (existing != null)
        {
            body = existing.gameObject;
        }
        else
        {
            body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Training Dummy Capsule";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = Vector3.up;
        }
        body.GetComponent<Renderer>().material.color = new Color(0.75f, 0.25f, 0.2f);

        GameObject label = new GameObject("Dummy nameplate");
        label.transform.SetParent(body.transform, false);
        label.transform.localPosition = Vector3.up * 1.5f;
        label.transform.localScale = Vector3.one * 0.08f;
        TextMeshPro text = label.AddComponent<TextMeshPro>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 32;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(20f, 3f);
        text.text = "Training Dummy";
        label.AddComponent<UnifiedNameplate>();
        label.AddComponent<LookAtCamera>();
    }

    private void OnGUI()
    {
        if (!IsSpawned || defeated.Value) return;
        NetworkObject localPlayer = NetworkManager.Singleton.LocalClient?.PlayerObject;
        if (localPlayer == null) return;
        PlayerInteraction interaction = localPlayer.GetComponent<PlayerInteraction>();
        if (interaction == null || interaction.SelectedTarget != transform) return;
        Camera hudCamera = Camera.main;
        if (hudCamera == null) return;

        Vector3 screenPosition = hudCamera.WorldToScreenPoint(transform.position + Vector3.up * 3.6f);
        if (screenPosition.z <= 0f) return;
        PlayerHAM.DrawHamBars(screenPosition, health.Value, action.Value, mind.Value);
    }

    private void Start()
    {
        UnevenGround ground = FindAnyObjectByType<UnevenGround>();
        if (ground != null && ground.GetComponent<Collider>().Raycast(
            new Ray(transform.position + Vector3.up * 100f, Vector3.down), out RaycastHit hit, 200f))
            transform.position = hit.point;
        else
            Debug.LogError("Could not place the training dummy on the ground.", this);
    }

    public override void OnNetworkSpawn()
    {
        defeated.OnValueChanged += OnDefeatedChanged;
        body.SetActive(!defeated.Value);
    }

    public override void OnNetworkDespawn()
    {
        defeated.OnValueChanged -= OnDefeatedChanged;
    }

    private void OnDefeatedChanged(bool previous, bool current)
    {
        body.SetActive(!current);
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer || !defeated.Value || Time.time < respawnAt) return;
        health.Value = MaximumPool;
        action.Value = MaximumPool;
        mind.Value = MaximumPool;
        defeated.Value = false;
    }

    public void ApplyDamage(float healthDamage, float actionDamage, float mindDamage)
    {
        ApplyDamageFrom(healthDamage, actionDamage, mindDamage, transform.position - transform.forward);
    }

    public void ApplyDamageFrom(float healthDamage, float actionDamage, float mindDamage, Vector3 attackerPosition)
    {
        if (!IsServer)
        {
            Debug.LogError("Dummy damage can only be applied by the server.", this);
            return;
        }
        if (defeated.Value) return;

        health.Value = Mathf.Max(0f, health.Value - healthDamage);
        action.Value = Mathf.Max(0f, action.Value - actionDamage);
        mind.Value = Mathf.Max(0f, mind.Value - mindDamage);
        Vector3 bodyCenter = body.GetComponent<Collider>().bounds.center;
        Vector3 impactNormal = Vector3.ProjectOnPlane(attackerPosition - bodyCenter, Vector3.up).normalized;
        if (impactNormal.sqrMagnitude < 0.001f)
            impactNormal = -transform.forward;
        Vector3 impactPosition = bodyCenter + impactNormal * 0.52f;
        PlayHitSparksClientRpc(impactPosition, impactNormal);
        if (health.Value <= 0f || action.Value <= 0f || mind.Value <= 0f)
        {
            respawnAt = Time.time + RespawnSeconds;
            defeated.Value = true;
        }
    }

    [ClientRpc]
    private void PlayHitSparksClientRpc(Vector3 position, Vector3 normal)
    {
        GameObject sparksObject = new GameObject("Combat Hit Sparks");
        sparksObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(normal));
        ParticleSystem sparks = sparksObject.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = sparks.main;
        main.duration = 0.35f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
        main.startColor = new Color(4f, 3.5f, 1.5f);
        main.gravityModifier = 1.5f;
        main.maxParticles = 16;

        ParticleSystem.EmissionModule emission = sparks.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

        ParticleSystem.ShapeModule shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.025f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = sparks.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.55f), 0f),
                new GradientColorKey(new Color(1f, 0.3f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

        ParticleSystemRenderer sparksRenderer = sparks.GetComponent<ParticleSystemRenderer>();
        sparksRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        sparksRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sparksRenderer.receiveShadows = false;

        GameObject flashObject = new GameObject("Combat Hit Flash");
        flashObject.transform.position = position;
        Light flash = flashObject.AddComponent<Light>();
        flash.color = new Color(1f, 0.92f, 0.68f);
        flash.intensity = 12f;
        flash.range = 3f;
        flash.shadows = LightShadows.None;

        sparks.Play();
        Destroy(flashObject, 0.12f);
        Destroy(sparksObject, 1f);
    }
}
