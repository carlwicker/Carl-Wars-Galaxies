using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class VehicleAssetData
{
    [System.Serializable]
    public class Color32Data
    {
        public float r;
        public float g;
        public float b;
        public float a = 1f;

        public Color ToColor() => new Color(r, g, b, a);
    }

    [System.Serializable]
    public class Vector3IntData
    {
        public int width;
        public int height;
        public int length;

        public Vector3Int ToVector3Int() => new Vector3Int(width, height, length);
    }

    [System.Serializable]
    public class PhysicsData
    {
        public float hoverRiseSpeed = 3f;
        public float hoverDropSpeed = 2f;
    }

    [System.Serializable]
    public class DedicatedSlotsData
    {
        public int engine = 1;
        public int power = 1;
    }

    [System.Serializable]
    public class VehicleDefinition
    {
        public string id;
        public string name;
        public string description;
        public Vector3IntData dimensions;
        public int cargoSlots = 4;
        public Color32Data color;
        public PhysicsData physics;
        public DedicatedSlotsData dedicatedSlots;
        public string[] tags;
        public int cost = 0;
    }

    [System.Serializable]
    public class VehicleAssetCollection
    {
        public VehicleDefinition[] vehicles;
    }
}

public class VehicleAssetLoader : MonoBehaviour
{
    [SerializeField] private bool autoLoadOnStart = true;
    private static VehicleAssetData.VehicleAssetCollection assetCollection;
    private static Dictionary<string, VehicleAssetData.VehicleDefinition> vehicleCache;

    private void Start()
    {
        if (autoLoadOnStart && assetCollection == null)
            LoadAssets();
    }

    public static bool LoadAssets()
    {
        try
        {
            TextAsset jsonFile = Resources.Load<TextAsset>("VehicleAssets");
            if (jsonFile == null)
            {
                Debug.LogError("VehicleAssets.json not found in Resources folder!");
                return false;
            }

            assetCollection = JsonUtility.FromJson<VehicleAssetData.VehicleAssetCollection>(jsonFile.text);
            if (assetCollection?.vehicles == null || assetCollection.vehicles.Length == 0)
            {
                Debug.LogError("VehicleAssets.json is empty or malformed!");
                return false;
            }

            vehicleCache = new Dictionary<string, VehicleAssetData.VehicleDefinition>();
            foreach (VehicleAssetData.VehicleDefinition vehicle in assetCollection.vehicles)
            {
                if (string.IsNullOrEmpty(vehicle.id))
                {
                    Debug.LogWarning("Vehicle definition missing ID, skipping.");
                    continue;
                }
                vehicleCache[vehicle.id] = vehicle;
            }

            Debug.Log($"Loaded {vehicleCache.Count} vehicle assets.");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load vehicle assets: {e.Message}");
            return false;
        }
    }

    public static bool TryGetVehicleAsset(string vehicleId, out VehicleAssetData.VehicleDefinition asset)
    {
        asset = null;
        if (vehicleCache == null)
            LoadAssets();
        return vehicleCache != null && vehicleCache.TryGetValue(vehicleId, out asset);
    }

    public static void ApplyAssetToVehicle(Vehicle vehicle, string vehicleId)
    {
        if (!TryGetVehicleAsset(vehicleId, out VehicleAssetData.VehicleDefinition asset))
        {
            Debug.LogError($"Vehicle asset '{vehicleId}' not found!");
            return;
        }

        ApplyAssetToVehicle(vehicle, asset);
    }

    public static void ApplyAssetToVehicle(Vehicle vehicle, VehicleAssetData.VehicleDefinition asset)
    {
        if (vehicle == null || asset == null || asset.dimensions == null
            || asset.color == null || asset.physics == null)
        {
            Debug.LogError("Cannot apply an incomplete vehicle asset.", vehicle);
            return;
        }

        if (!vehicle.ConfigureFromAsset(
                asset.id,
                asset.name,
                asset.dimensions.ToVector3Int(),
                asset.cargoSlots,
                asset.color.ToColor(),
                asset.physics.hoverRiseSpeed,
                asset.physics.hoverDropSpeed))
        {
            Debug.LogError($"Could not apply vehicle asset '{asset.id}'.", vehicle);
            return;
        }

        Debug.Log($"Applied asset '{asset.id}' to vehicle '{asset.name}'.", vehicle);
    }

    public static void PrintAllAssets()
    {
        if (vehicleCache == null)
            LoadAssets();

        if (vehicleCache == null || vehicleCache.Count == 0)
        {
            Debug.Log("No vehicle assets loaded.");
            return;
        }

        Debug.Log("=== Loaded Vehicle Assets ===");
        foreach (KeyValuePair<string, VehicleAssetData.VehicleDefinition> kvp in vehicleCache)
        {
            VehicleAssetData.VehicleDefinition asset = kvp.Value;
            Debug.Log($"[{asset.id}] {asset.name} - {asset.dimensions.width}x{asset.dimensions.height}x{asset.dimensions.length} " +
                      $"({asset.cargoSlots} cargo slots, {asset.cost}cr)");
        }
    }
}
