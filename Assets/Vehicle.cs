using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class Vehicle : NetworkBehaviour
{
    public const ulong NoRider = ulong.MaxValue;
    public const float HoverHeight = 1.5f;
    public const float MaximumEnergy = 100f;
    private const float ReferenceMass = 4f;
    private const float IdleEnergyPerSecond = 0.05f;
    private const float EnergyPerMeter = 0.005f;
    private const float EnergySaveInterval = 5f;

    [SerializeField] private string vehicleId;
    [SerializeField] private string vehicleName = "Vehicle";
    [Tooltip("Width (X) x Height (Y) x Length (Z), in cube units.")]
    [SerializeField] private Vector3Int dimensions = new Vector3Int(1, 1, 4);
    [SerializeField, Min(0)] private int cargoSlotCount = 4;
    [SerializeField] private Color color = new Color(0.08f, 0.28f, 0.38f);
    [SerializeField, Min(0.1f)] private float hoverRiseSpeed = 3f;
    [SerializeField, Min(0.1f)] private float hoverDropSpeed = 2f;

    private readonly NetworkVariable<bool> ready = new NetworkVariable<bool>();
    private readonly NetworkVariable<bool> engineInstalled = new NetworkVariable<bool>();
    private readonly NetworkVariable<int> engineQuality = new NetworkVariable<int>();
    private readonly NetworkVariable<int> engineSpeed = new NetworkVariable<int>();
    private readonly NetworkVariable<int> engineExperimentPoints = new NetworkVariable<int>();
    private readonly NetworkVariable<bool> powerInstalled = new NetworkVariable<bool>();
    private readonly NetworkVariable<int> powerQuality = new NetworkVariable<int>();
    private readonly NetworkVariable<float> powerEnergyUsed = new NetworkVariable<float>();
    private readonly NetworkVariable<bool> poweredOn = new NetworkVariable<bool>();
    private readonly NetworkVariable<ulong> riderId = new NetworkVariable<ulong>(NoRider);
    private readonly NetworkVariable<Vector3> parkedPosition = new NetworkVariable<Vector3>();
    private readonly NetworkVariable<float> parkedYaw = new NetworkVariable<float>();
    private readonly NetworkList<InventorySlot> cargo = new NetworkList<InventorySlot>();
    private Transform geometry;
    private Transform visuals;
    private BoxCollider bodyCollider;
    private Rigidbody vehicleRigidbody;
    private NetworkObject collisionIgnoredRider;
    private Collider[] ignoredRiderColliders;
    private TextMeshPro nameplate;
    private UnifiedNameplate nameplateStyle;
    private Light hoverLight;
    private ParticleSystem hoverDust;
    private readonly RaycastHit[] dustGroundHits = new RaycastHit[32];
    private float dustEmissionRemainder;
    private Material material;
    private float nextPositionSave;
    private Vector3 lastSavedPosition;
    private float lastSavedYaw;
    private float currentHoverHeight;
    private float hoverEffectAmount;
    private float nextEnergySave;
    private bool energySavePending;
    private bool hasRiderSample;
    private Vector3 previousRiderPosition;

    public string VehicleName => vehicleName;
    public bool IsReady => ready.Value;
    public bool HasEngine => engineInstalled.Value;
    public float EngineSpeedMultiplier => engineSpeed.Value > 0 ? engineSpeed.Value / 100f : 1f;
    public bool HasPower => powerInstalled.Value;
    public int PowerQuality => powerQuality.Value;
    public float Mass => (float)dimensions.x * dimensions.y * dimensions.z;
    public float EnergyRemaining => HasPower ? Mathf.Max(0f, MaximumEnergy - powerEnergyUsed.Value) : 0f;
    public bool IsOn => poweredOn.Value;
    public bool IsOccupied => riderId.Value != NoRider;
    public float RiderHeight => dimensions.y + currentHoverHeight;
    public int CargoSlotCount => cargoSlotCount;
    public NetworkList<InventorySlot> Cargo => cargo;

    private void Awake()
    {
        GameObject geometryObject = new GameObject("Vehicle Geometry");
        geometry = geometryObject.transform;
        geometry.SetParent(transform, false);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogError("Could not find a shader for vehicle geometry.", this);
            enabled = false;
            return;
        }

        material = new Material(shader) { color = color };
        visuals = new GameObject("Vehicle Visuals").transform;
        visuals.SetParent(geometry, false);
        bodyCollider = geometryObject.AddComponent<BoxCollider>();

        vehicleRigidbody = geometryObject.AddComponent<Rigidbody>();
        vehicleRigidbody.isKinematic = true;
        vehicleRigidbody.useGravity = false;

        GameObject label = new GameObject("Vehicle Nameplate");
        label.transform.SetParent(visuals, false);
        nameplate = label.AddComponent<TextMeshPro>();
        nameplate.font = TMP_Settings.defaultFontAsset;
        nameplate.alignment = TextAlignmentOptions.Center;
        nameplate.fontSize = 32;
        nameplate.color = Color.white;
        nameplate.rectTransform.sizeDelta = new Vector2(20f, 5f);
        label.AddComponent<LookAtCamera>();
        nameplateStyle = label.AddComponent<UnifiedNameplate>();

        RebuildGeometry();
    }

    public bool ConfigureFromAsset(string id, string displayName, Vector3Int assetDimensions,
        int assetCargoSlotCount, Color assetColor, float assetHoverRiseSpeed, float assetHoverDropSpeed)
    {
        if (IsSpawned)
        {
            Debug.LogError("Vehicle assets must be applied before the vehicle is network-spawned.", this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName)
            || assetDimensions.x <= 0 || assetDimensions.y <= 0 || assetDimensions.z <= 0
            || assetCargoSlotCount < 0 || assetHoverRiseSpeed < 0.1f || assetHoverDropSpeed < 0.1f)
        {
            Debug.LogError("Vehicle asset contains invalid configuration values.", this);
            return false;
        }

        vehicleId = id;
        vehicleName = displayName;
        dimensions = assetDimensions;
        cargoSlotCount = assetCargoSlotCount;
        color = assetColor;
        hoverRiseSpeed = assetHoverRiseSpeed;
        hoverDropSpeed = assetHoverDropSpeed;

        if (visuals == null || bodyCollider == null || material == null || nameplate == null)
        {
            Debug.LogError("Vehicle geometry has not been initialized.", this);
            return false;
        }

        material.color = color;
        RebuildGeometry();
        return true;
    }

    private void RebuildGeometry()
    {
        vehicleRigidbody.mass = Mass;

        for (int i = visuals.childCount - 1; i >= 0; i--)
        {
            Transform child = visuals.GetChild(i);
            if (child != nameplate.transform)
                Destroy(child.gameObject);
        }

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Vehicle Body";
        body.transform.SetParent(visuals, false);
        body.transform.localPosition = new Vector3(0f, dimensions.y * 0.5f, 0f);
        body.transform.localScale = dimensions;
        Collider bodyPrimitiveCollider = body.GetComponent<Collider>();
        bodyPrimitiveCollider.enabled = false;
        Destroy(bodyPrimitiveCollider);
        body.GetComponent<Renderer>().sharedMaterial = material;

        bodyCollider.center = new Vector3(0f, dimensions.y * 0.5f, 0f);
        bodyCollider.size = dimensions;
        nameplate.text = vehicleName;
        nameplateStyle.Refresh();
        nameplate.transform.localPosition = new Vector3(0f, dimensions.y + 1.5f, 0f);
        nameplate.transform.localScale = Vector3.one * 0.1f;
    }

    public override void OnNetworkSpawn()
    {
        riderId.OnValueChanged += OnRiderChanged;
        RefreshRiderCollision();
        if (visuals != null && hoverLight == null)
        {
            GameObject lightObject = new GameObject("Hover Glow");
            lightObject.transform.SetParent(visuals, false);
            lightObject.transform.localPosition = Vector3.up * 0.1f;
            lightObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hoverLight = lightObject.AddComponent<Light>();
            hoverLight.type = LightType.Spot;
            hoverLight.color = new Color(0.2f, 0.8f, 1f);
            hoverLight.range = 5f;
            hoverLight.spotAngle = 120f;
            hoverLight.shadows = LightShadows.None;
            hoverLight.enabled = false;
        }

        if (IsClient && hoverDust == null && material != null)
            CreateHoverDust();

        if (!IsServer) return;
        if (geometry == null || bodyCollider == null || string.IsNullOrWhiteSpace(vehicleId)
            || dimensions.x <= 0 || dimensions.y <= 0 || dimensions.z <= 0 || cargoSlotCount < 0)
        {
            Debug.LogError("Vehicle needs a unique ID and positive dimensions.", this);
            return;
        }

        try
        {
            HostInventoryStore.SavedVehicle saved = HostInventoryStore.LoadVehicle(vehicleId);
            if (saved == null)
            {
                Vector3 position = transform.position;
                if (!TryGetGroundHeight(position, out float groundHeight))
                    throw new InvalidDataException("Could not find ground beneath the vehicle.");
                position.y = groundHeight;
                parkedPosition.Value = position;
                parkedYaw.Value = transform.eulerAngles.y;
            }
            else
            {
                if (saved.isOn && (!saved.engineInstalled || !saved.powerInstalled))
                    throw new InvalidDataException("A powered vehicle is missing a required cube.");
                if (!IsValidEnergyUsed(saved.powerEnergyUsed)
                    || (saved.isOn && saved.powerEnergyUsed >= MaximumEnergy))
                    throw new InvalidDataException("Vehicle power cube energy is invalid.");
                if (saved.cargo == null || saved.cargo.Count > cargoSlotCount)
                    throw new InvalidDataException("Vehicle cargo is invalid.");
                foreach (HostInventoryStore.SavedItem item in saved.cargo)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.itemId)
                        || string.IsNullOrWhiteSpace(item.itemName) || item.count <= 0
                        || item.count > PlayerInventory.MaximumStackSize || !IsValidEnergyUsed(item.energyUsed))
                        throw new InvalidDataException("Vehicle cargo contains an invalid item.");
                }

                parkedPosition.Value = new Vector3(saved.x, saved.y, saved.z);
                parkedYaw.Value = saved.yaw;
                ApplySave(saved);
            }
            lastSavedPosition = parkedPosition.Value;
            lastSavedYaw = parkedYaw.Value;
            ready.Value = true;
        }
        catch (IOException exception) { ReportSaveError("load", exception); }
        catch (UnauthorizedAccessException exception) { ReportSaveError("load", exception); }
        catch (InvalidDataException exception) { ReportSaveError("load", exception); }
        catch (ArgumentException exception) { ReportSaveError("load", exception); }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && ready.Value && energySavePending)
            TrySave(CreateSave());
        riderId.OnValueChanged -= OnRiderChanged;
        ClearRiderCollisionIgnore();
        if (hoverLight != null) hoverLight.enabled = false;
        if (hoverDust != null) hoverDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        dustEmissionRemainder = 0f;
        hoverEffectAmount = 0f;
        if (visuals != null) visuals.localPosition = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (!IsSpawned || !ready.Value || geometry == null) return;

        RefreshRiderCollision();
        float distanceTravelled = 0f;
        if (IsOccupied && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(riderId.Value, out NetworkObject rider))
        {
            if (IsServer)
            {
                if (hasRiderSample)
                {
                    Vector3 displacement = rider.transform.position - previousRiderPosition;
                    displacement.y = 0f;
                    distanceTravelled = displacement.magnitude;
                }
                previousRiderPosition = rider.transform.position;
                hasRiderSample = true;
            }
            CharacterController controller = rider.GetComponent<CharacterController>();
            float footOffset = controller != null ? controller.height * 0.5f - controller.center.y : 1f;
            transform.position = rider.transform.position - Vector3.up * footOffset;
            transform.rotation = Quaternion.Euler(0f, rider.transform.eulerAngles.y, 0f);
            if (IsServer)
            {
                parkedPosition.Value = transform.position;
                parkedYaw.Value = transform.eulerAngles.y;
                if (Time.time >= nextPositionSave
                    && ((parkedPosition.Value - lastSavedPosition).sqrMagnitude > 0.01f
                        || Mathf.Abs(Mathf.DeltaAngle(lastSavedYaw, parkedYaw.Value)) > 1f))
                {
                    nextPositionSave = Time.time + 1f;
                    if (TrySave(CreateSave()))
                    {
                        lastSavedPosition = parkedPosition.Value;
                        lastSavedYaw = parkedYaw.Value;
                    }
                }
            }
        }
        else
        {
            hasRiderSample = false;
            transform.SetPositionAndRotation(parkedPosition.Value, Quaternion.Euler(0f, parkedYaw.Value, 0f));
        }

        if (IsServer)
        {
            if (poweredOn.Value)
            {
                ConsumeEnergy(Time.deltaTime, distanceTravelled);
            }
            if (energySavePending && Time.time >= nextEnergySave)
            {
                nextEnergySave = Time.time + EnergySaveInterval;
                if (TrySave(CreateSave())) energySavePending = false;
            }
        }

        float targetHoverHeight = poweredOn.Value ? HoverHeight : 0f;
        if (Mathf.Abs(currentHoverHeight - targetHoverHeight) > 0.001f)
        {
            float speed = targetHoverHeight > currentHoverHeight ? hoverRiseSpeed : hoverDropSpeed;
            currentHoverHeight = Mathf.MoveTowards(currentHoverHeight, targetHoverHeight, speed * Time.deltaTime);
        }
        else
        {
            currentHoverHeight = targetHoverHeight;
        }

        geometry.localPosition = Vector3.up * currentHoverHeight;
        hoverEffectAmount = Mathf.MoveTowards(hoverEffectAmount, poweredOn.Value ? 1f : 0f, 2f * Time.deltaTime);
        float swayAmount = hoverEffectAmount * Mathf.Clamp01(currentHoverHeight / HoverHeight);
        visuals.localPosition = new Vector3(
            Mathf.Sin(Time.time * 1.2f) * 0.04f * swayAmount,
            Mathf.Sin(Time.time * 1.8f) * 0.05f * swayAmount,
            0f);
        hoverLight.intensity = 5f * hoverEffectAmount;
        hoverLight.enabled = hoverEffectAmount > 0f;
        UpdateHoverDust();
    }

    private void ConsumeEnergy(float elapsed, float distanceTravelled)
    {
        if (!poweredOn.Value) return;
        float massMultiplier = Mass / ReferenceMass;
        powerEnergyUsed.Value = Mathf.Min(MaximumEnergy,
            powerEnergyUsed.Value
                + (IdleEnergyPerSecond * elapsed + EnergyPerMeter * distanceTravelled) * massMultiplier);
        energySavePending = true;
        if (powerEnergyUsed.Value >= MaximumEnergy)
        {
            poweredOn.Value = false;
            nextEnergySave = 0f;
        }
    }

    private void OnGUI()
    {
        if (!IsSpawned || !ready.Value) return;
        Camera hudCamera = Camera.main;
        if (hudCamera == null) return;
        Vector3 screenPosition = hudCamera.WorldToScreenPoint(
            transform.position + Vector3.up * (dimensions.y + currentHoverHeight + 2.7f));
        if (screenPosition.z <= 0f) return;
        PlayerHAM.DrawPool(screenPosition.x - 72f, Screen.height - screenPosition.y - 14f,
            EnergyRemaining, new Color(0.15f, 0.75f, 0.9f),
            HasPower ? $"Energy {EnergyRemaining:0}%" : "No power cube");
    }

    private static bool IsValidEnergyUsed(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= MaximumEnergy;
    }

    private void CreateHoverDust()
    {
        Material dustMaterial = Resources.Load<Material>("HoverDust");
        if (dustMaterial == null)
        {
            Debug.LogError("Could not load the HoverDust material.", this);
            return;
        }

        GameObject dustObject = new GameObject("Hover Dust");
        dustObject.transform.SetParent(transform, false);
        hoverDust = dustObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = hoverDust.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
        main.startSpeed = 0f;
        main.maxParticles = 180;

        ParticleSystem.EmissionModule emission = hoverDust.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = hoverDust.shape;
        shape.enabled = false;
        ParticleSystem.SizeOverLifetimeModule size = hoverDust.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 2f));

        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = hoverDust.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fade);

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh cubeMesh = cube.GetComponent<MeshFilter>().sharedMesh;
        cube.SetActive(false);
        Destroy(cube);

        ParticleSystemRenderer dustRenderer = dustObject.GetComponent<ParticleSystemRenderer>();
        dustRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        dustRenderer.enableGPUInstancing = false;
        dustRenderer.mesh = cubeMesh;
        dustRenderer.sharedMaterial = dustMaterial;
        dustRenderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position,
            ParticleSystemVertexStream.Normal,
            ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV
        });
        dustRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        dustRenderer.receiveShadows = false;
        hoverDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void UpdateHoverDust()
    {
        if (hoverDust == null) return;
        if (!poweredOn.Value)
        {
            if (hoverDust.isPlaying || hoverDust.particleCount > 0)
                hoverDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            dustEmissionRemainder = 0f;
            return;
        }

        if (!hoverDust.isPlaying) hoverDust.Play();
        float rate = Mathf.Min(120f, 45f + (float)dimensions.x * dimensions.z * 2f);
        dustEmissionRemainder += rate * Time.deltaTime;
        int count = Mathf.Min(Mathf.FloorToInt(dustEmissionRemainder), 120);
        dustEmissionRemainder -= Mathf.Floor(dustEmissionRemainder);
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = new Vector3(
                UnityEngine.Random.Range(-dimensions.x * 0.5f - 0.6f, dimensions.x * 0.5f + 0.6f),
                0f,
                UnityEngine.Random.Range(-dimensions.z * 0.5f - 0.6f, dimensions.z * 0.5f + 0.6f));
            Vector3 sample = transform.position + transform.rotation * offset;
            int hitCount = Physics.RaycastNonAlloc(sample + Vector3.up * 3f, Vector3.down,
                dustGroundHits, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit ground = default;
            bool found = false;
            for (int j = 0; j < hitCount; j++)
            {
                RaycastHit hit = dustGroundHits[j];
                if (hit.collider.GetComponentInParent<Vehicle>() != null
                    || hit.collider.GetComponentInParent<PlayerInteraction>() != null
                    || hit.collider.GetComponentInParent<WorldPickup>() != null)
                    continue;
                if (!found || hit.distance < ground.distance)
                {
                    ground = hit;
                    found = true;
                }
            }
            if (!found) continue;

            Vector3 outward = Vector3.ProjectOnPlane(transform.rotation * offset, ground.normal).normalized;
            if (outward.sqrMagnitude < 0.01f) outward = transform.right;
            ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams
            {
                position = ground.point + ground.normal * 0.12f,
                velocity = outward * UnityEngine.Random.Range(2f, 4f) + ground.normal * 0.15f
            };
            hoverDust.Emit(particle, 1);
        }
    }

    public HostInventoryStore.SavedVehicle CreateSave()
    {
        HostInventoryStore.SavedVehicle state = new HostInventoryStore.SavedVehicle
        {
            vehicleId = vehicleId,
            x = parkedPosition.Value.x,
            y = parkedPosition.Value.y,
            z = parkedPosition.Value.z,
            yaw = parkedYaw.Value,
            engineInstalled = engineInstalled.Value,
            engineQuality = engineQuality.Value,
            engineSpeed = engineSpeed.Value,
            engineExperimentPoints = engineExperimentPoints.Value,
            powerInstalled = powerInstalled.Value,
            powerQuality = powerQuality.Value,
            powerEnergyUsed = powerEnergyUsed.Value,
            isOn = poweredOn.Value
        };
        foreach (InventorySlot slot in cargo)
        {
            state.cargo.Add(new HostInventoryStore.SavedItem
            {
                itemId = slot.ItemId.ToString(),
                itemName = slot.ItemName.ToString(),
                count = slot.Count,
                quality = slot.Quality,
                conductivity = slot.Conductivity,
                speed = slot.Speed,
                experimentPoints = slot.ExperimentPoints,
                energyUsed = slot.EnergyUsed,
                potentialEnergy = slot.PotentialEnergy,
                coldResistance = slot.ColdResistance,
                decayResistance = slot.DecayResistance,
                entangleResistance = slot.EntangleResistance
            });
        }
        return state;
    }

    public void ApplySave(HostInventoryStore.SavedVehicle state)
    {
        engineInstalled.Value = state.engineInstalled;
        engineQuality.Value = state.engineQuality;
        engineSpeed.Value = state.engineSpeed;
        engineExperimentPoints.Value = state.engineExperimentPoints;
        powerInstalled.Value = state.powerInstalled;
        powerQuality.Value = state.powerQuality;
        powerEnergyUsed.Value = state.powerEnergyUsed;
        poweredOn.Value = state.isOn;
        cargo.Clear();
        foreach (HostInventoryStore.SavedItem item in state.cargo)
            cargo.Add(new InventorySlot(item.itemId, item.itemName, item.count, item.quality,
                item.speed, item.experimentPoints, item.conductivity, item.energyUsed,
                item.potentialEnergy, item.coldResistance, item.decayResistance, item.entangleResistance));
        energySavePending = false;
    }

    public bool TrySetPower(bool value, ulong requesterId)
    {
        if (!IsServer || !IsReady || (IsOccupied && riderId.Value != requesterId)
            || (value && (!HasEngine || !HasPower || EnergyRemaining <= 0f))) return false;
        HostInventoryStore.SavedVehicle state = CreateSave();
        state.isOn = value;
        if (!TrySave(state)) return false;
        poweredOn.Value = value;
        energySavePending = false;
        return true;
    }

    public bool TryMount(PlayerInteraction player)
    {
        if (!IsServer || !IsReady || IsOccupied) return false;
        riderId.Value = player.NetworkObjectId;
        return true;
    }

    public bool Dismount(PlayerInteraction player)
    {
        if (!IsServer || riderId.Value != player.NetworkObjectId) return false;
        CharacterController controller = player.GetComponent<CharacterController>();
        float footOffset = controller.height * 0.5f - controller.center.y;
        parkedPosition.Value = player.transform.position - Vector3.up * footOffset;
        parkedYaw.Value = player.transform.eulerAngles.y;
        bool saved = TrySave(CreateSave());
        riderId.Value = NoRider;
        return saved;
    }

    private void OnRiderChanged(ulong previous, ulong current)
    {
        hasRiderSample = false;
        RefreshRiderCollision();
    }

    public void RefreshRiderCollision()
    {
        NetworkObject rider = null;
        if (IsOccupied && NetworkManager != null)
            NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(riderId.Value, out rider);

        if (collisionIgnoredRider != rider)
        {
            ClearRiderCollisionIgnore();
            collisionIgnoredRider = rider;
            if (rider != null) ignoredRiderColliders = rider.GetComponents<Collider>();
        }

        if (bodyCollider == null || !bodyCollider.enabled || ignoredRiderColliders == null) return;
        foreach (Collider collider in ignoredRiderColliders)
        {
            if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy
                && !Physics.GetIgnoreCollision(collider, bodyCollider))
                Physics.IgnoreCollision(collider, bodyCollider, true);
        }
    }

    private void ClearRiderCollisionIgnore()
    {
        if (bodyCollider != null && ignoredRiderColliders != null)
        {
            foreach (Collider collider in ignoredRiderColliders)
            {
                if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
                    Physics.IgnoreCollision(collider, bodyCollider, false);
            }
        }
        collisionIgnoredRider = null;
        ignoredRiderColliders = null;
    }

    public Quaternion ConstrainRiderRotation(CharacterController rider, Quaternion desiredRotation)
    {
        Physics.SyncTransforms();
        Quaternion current = Quaternion.Euler(0f, rider.transform.eulerAngles.y, 0f);
        float angle = Mathf.DeltaAngle(current.eulerAngles.y, desiredRotation.eulerAngles.y);
        int steps = Mathf.CeilToInt(Mathf.Abs(angle));
        Vector3 position = RiderGeometryPosition(rider);
        Vector3 halfExtents = Vector3.Scale(bodyCollider.size, geometry.lossyScale) * 0.5f;

        for (int i = 1; i <= steps; i++)
        {
            Quaternion candidate = Quaternion.Euler(0f, current.eulerAngles.y + angle * i / steps, 0f);
            Vector3 center = position + candidate * Vector3.Scale(bodyCollider.center, geometry.lossyScale);
            Collider[] overlaps = Physics.OverlapBox(center, halfExtents, candidate,
                ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider obstacle in overlaps)
            {
                if (!IsBlockingCollider(obstacle, rider)) continue;
                if (Physics.ComputePenetration(bodyCollider, position, candidate,
                        obstacle, obstacle.transform.position, obstacle.transform.rotation,
                        out _, out float candidateDepth)
                    && (!Physics.ComputePenetration(bodyCollider, position, current,
                            obstacle, obstacle.transform.position, obstacle.transform.rotation,
                            out _, out float currentDepth)
                        || candidateDepth > currentDepth + 0.001f))
                    return Quaternion.Euler(0f, current.eulerAngles.y + angle * (i - 1) / steps, 0f);
            }
        }
        return desiredRotation;
    }

    public Vector3 ConstrainRiderMovement(CharacterController rider, Vector3 movement)
    {
        float distance = movement.magnitude;
        if (distance <= 0f) return movement;

        Physics.SyncTransforms();
        Quaternion rotation = Quaternion.Euler(0f, rider.transform.eulerAngles.y, 0f);
        Vector3 position = RiderGeometryPosition(rider);
        Vector3 halfExtents = Vector3.Scale(bodyCollider.size, geometry.lossyScale) * 0.5f;
        Vector3 center = position + rotation * Vector3.Scale(bodyCollider.center, geometry.lossyScale);
        Vector3 direction = movement / distance;
        const float clearance = 0.01f;
        RaycastHit[] hits = Physics.BoxCastAll(center, halfExtents, direction, rotation,
            distance + clearance, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (!IsBlockingCollider(hit.collider, rider)) continue;
            if (hit.distance == 0f)
            {
                Physics.ComputePenetration(bodyCollider, position, rotation,
                    hit.collider, hit.collider.transform.position, hit.collider.transform.rotation,
                    out _, out float currentDepth);
                bool penetratesAhead = Physics.ComputePenetration(bodyCollider,
                    position + direction * Mathf.Min(distance, clearance), rotation,
                    hit.collider, hit.collider.transform.position, hit.collider.transform.rotation,
                    out _, out float nextDepth);
                if (!penetratesAhead || nextDepth <= currentDepth + 0.0001f) continue;
            }
            distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - clearance));
        }
        return direction * distance;
    }

    public bool IsRiderPositionClear(CharacterController rider, Vector3 previousPosition)
    {
        Quaternion rotation = Quaternion.Euler(0f, rider.transform.eulerAngles.y, 0f);
        Vector3 position = RiderGeometryPosition(rider);
        Vector3 previousGeometryPosition = position + previousPosition - rider.transform.position;
        Vector3 halfExtents = Vector3.Scale(bodyCollider.size, geometry.lossyScale) * 0.5f;
        Vector3 center = position + rotation * Vector3.Scale(bodyCollider.center, geometry.lossyScale);
        Collider[] overlaps = Physics.OverlapBox(center, halfExtents, rotation,
            ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider obstacle in overlaps)
        {
            if (!IsBlockingCollider(obstacle, rider)) continue;
            if (Physics.ComputePenetration(bodyCollider, position, rotation,
                    obstacle, obstacle.transform.position, obstacle.transform.rotation,
                    out _, out float newDepth)
                && (!Physics.ComputePenetration(bodyCollider, previousGeometryPosition, rotation,
                        obstacle, obstacle.transform.position, obstacle.transform.rotation,
                        out _, out float previousDepth)
                    || newDepth > previousDepth + 0.001f))
                return false;
        }
        return true;
    }

    private Vector3 RiderGeometryPosition(CharacterController rider)
    {
        float footOffset = rider.height * 0.5f - rider.center.y;
        return rider.transform.position - Vector3.up * footOffset + Vector3.up * currentHoverHeight;
    }

    private bool IsBlockingCollider(Collider obstacle, CharacterController rider)
    {
        return obstacle != null && obstacle != bodyCollider
            && !obstacle.transform.IsChildOf(transform)
            && !obstacle.transform.IsChildOf(rider.transform)
            && !Physics.GetIgnoreLayerCollision(bodyCollider.gameObject.layer, obstacle.gameObject.layer)
            && !Physics.GetIgnoreCollision(bodyCollider, obstacle);
    }

    public Vector3 GetMountPosition()
    {
        Vector3 position = parkedPosition.Value;
        if (TryGetGroundHeight(position, out float groundHeight)) position.y = groundHeight;
        return position + Vector3.up;
    }

    public Vector3 GetExitPosition()
    {
        Vector3 position = parkedPosition.Value + Quaternion.Euler(0f, parkedYaw.Value, 0f) * Vector3.right
            * (dimensions.x * 0.5f + 1.5f);
        if (TryGetGroundHeight(position, out float groundHeight))
            position.y = groundHeight + 1f;
        else
            position.y += 1f;
        return position;
    }

    public bool IsWithinRange(Vector3 position, float range)
    {
        return bodyCollider != null
            && (bodyCollider.ClosestPoint(position) - position).sqrMagnitude <= range * range;
    }

    private bool TryGetGroundHeight(Vector3 position, out float height)
    {
        RaycastHit[] hits = Physics.RaycastAll(position + Vector3.up * 100f, Vector3.down, 200f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        bool found = false;
        height = float.NegativeInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<Vehicle>() == null
                && hit.collider.GetComponentInParent<PlayerInteraction>() == null
                && hit.collider.GetComponentInParent<WorldPickup>() == null
                && hit.collider.GetComponentInParent<CraftingTerminal>() == null
                && hit.point.y > height)
            {
                height = hit.point.y;
                found = true;
            }
        }
        if (found) return true;
        height = position.y;
        Debug.LogError($"Could not find ground beneath {vehicleName}.", this);
        return false;
    }

    private bool TrySave(HostInventoryStore.SavedVehicle state)
    {
        try
        {
            HostInventoryStore.SaveVehicle(state);
            return true;
        }
        catch (IOException exception) { ReportSaveError("save", exception); }
        catch (UnauthorizedAccessException exception) { ReportSaveError("save", exception); }
        catch (InvalidDataException exception) { ReportSaveError("save", exception); }
        catch (ArgumentException exception) { ReportSaveError("save", exception); }
        return false;
    }

    private void ReportSaveError(string action, Exception exception)
    {
        Debug.LogError($"Could not {action} vehicle {vehicleId}: {exception.Message}", this);
    }

    public override void OnDestroy()
    {
        cargo.Dispose();
        if (material != null) Destroy(material);
        base.OnDestroy();
    }
}
