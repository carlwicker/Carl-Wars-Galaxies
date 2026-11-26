using System;
using System.IO;
using TMPro;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class Vehicle : NetworkBehaviour
{
    public const ulong NoRider = ulong.MaxValue;
    public const float HoverHeight = 1.5f;

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
    private readonly NetworkVariable<bool> powerInstalled = new NetworkVariable<bool>();
    private readonly NetworkVariable<bool> poweredOn = new NetworkVariable<bool>();
    private readonly NetworkVariable<ulong> riderId = new NetworkVariable<ulong>(NoRider);
    private readonly NetworkVariable<Vector3> parkedPosition = new NetworkVariable<Vector3>();
    private readonly NetworkVariable<float> parkedYaw = new NetworkVariable<float>();
    private readonly NetworkList<InventorySlot> cargo = new NetworkList<InventorySlot>();
    private Transform geometry;
    private Transform visuals;
    private BoxCollider bodyCollider;
    private TextMeshPro nameplate;
    private Light hoverLight;
    private Material material;
    private float nextPositionSave;
    private Vector3 lastSavedPosition;
    private float lastSavedYaw;
    private float currentHoverHeight;
    private float hoverEffectAmount;

    public string VehicleName => vehicleName;
    public bool IsReady => ready.Value;
    public bool HasEngine => engineInstalled.Value;
    public bool HasPower => powerInstalled.Value;
    public bool IsOn => poweredOn.Value;
    public bool IsOccupied => riderId.Value != NoRider;
    public float RiderHeight => dimensions.y + HoverHeight;
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

        GameObject label = new GameObject("Vehicle Nameplate");
        label.transform.SetParent(visuals, false);
        nameplate = label.AddComponent<TextMeshPro>();
        nameplate.font = TMP_Settings.defaultFontAsset;
        nameplate.alignment = TextAlignmentOptions.Center;
        nameplate.fontSize = 32;
        nameplate.color = Color.white;
        nameplate.rectTransform.sizeDelta = new Vector2(100f, 60f);
        label.AddComponent<LookAtCamera>();
        label.AddComponent<UnifiedNameplate>();

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
        for (int i = visuals.childCount - 1; i >= 0; i--)
        {
            Transform child = visuals.GetChild(i);
            if (child != nameplate.transform)
                Destroy(child.gameObject);
        }

        for (int x = 0; x < dimensions.x; x++)
        {
            for (int y = 0; y < dimensions.y; y++)
            {
                for (int z = 0; z < dimensions.z; z++)
                {
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = "Vehicle Cube";
                    cube.transform.SetParent(visuals, false);
                    cube.transform.localPosition = new Vector3(
                        x - (dimensions.x - 1) * 0.5f,
                        y + 0.5f,
                        z - (dimensions.z - 1) * 0.5f);
                    Collider cubeCollider = cube.GetComponent<Collider>();
                    cubeCollider.enabled = false;
                    Destroy(cubeCollider);
                    cube.GetComponent<Renderer>().sharedMaterial = material;
                }
            }
        }

        bodyCollider.center = new Vector3(0f, dimensions.y * 0.5f, 0f);
        bodyCollider.size = dimensions;
        nameplate.text = vehicleName;
        nameplate.transform.localPosition = new Vector3(0f, dimensions.y + 1.5f, 0f);
        nameplate.transform.localScale = Vector3.one * 0.1f;
    }

    public override void OnNetworkSpawn()
    {
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
                if (saved.cargo == null || saved.cargo.Count > cargoSlotCount)
                    throw new InvalidDataException("Vehicle cargo is invalid.");
                foreach (HostInventoryStore.SavedItem item in saved.cargo)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.itemId)
                        || string.IsNullOrWhiteSpace(item.itemName) || item.count <= 0
                        || item.count > PlayerInventory.MaximumStackSize)
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
        if (hoverLight != null) hoverLight.enabled = false;
        hoverEffectAmount = 0f;
        if (visuals != null) visuals.localPosition = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (!IsSpawned || !ready.Value || geometry == null) return;

        if (IsOccupied && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(riderId.Value, out NetworkObject rider))
        {
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
            transform.SetPositionAndRotation(parkedPosition.Value, Quaternion.Euler(0f, parkedYaw.Value, 0f));
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
        bodyCollider.isTrigger = IsOccupied;
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
            powerInstalled = powerInstalled.Value,
            isOn = poweredOn.Value
        };
        foreach (InventorySlot slot in cargo)
        {
            state.cargo.Add(new HostInventoryStore.SavedItem
            {
                itemId = slot.ItemId.ToString(),
                itemName = slot.ItemName.ToString(),
                count = slot.Count
            });
        }
        return state;
    }

    public void ApplySave(HostInventoryStore.SavedVehicle state)
    {
        engineInstalled.Value = state.engineInstalled;
        powerInstalled.Value = state.powerInstalled;
        poweredOn.Value = state.isOn;
        cargo.Clear();
        foreach (HostInventoryStore.SavedItem item in state.cargo)
            cargo.Add(new InventorySlot(item.itemId, item.itemName, item.count));
    }

    public bool TrySetPower(bool value)
    {
        if (!IsServer || !IsReady || IsOccupied || (value && (!HasEngine || !HasPower))) return false;
        HostInventoryStore.SavedVehicle state = CreateSave();
        state.isOn = value;
        if (!TrySave(state)) return false;
        poweredOn.Value = value;
        return true;
    }

    public bool TryMount(PlayerInteraction player)
    {
        if (!IsServer || !IsReady || !IsOn || IsOccupied) return false;
        riderId.Value = player.NetworkObjectId;
        bodyCollider.isTrigger = true;
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
