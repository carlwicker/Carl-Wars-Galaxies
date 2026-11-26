using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Example: Spawning vehicles from asset definitions.
/// This shows how to integrate VehicleAssetLoader into a vehicle spawner.
/// </summary>
public class VehicleSpawnerExample : MonoBehaviour
{
    [SerializeField] private GameObject vehiclePrefab;
    [SerializeField] private string[] vehicleAssetIds = new[] { "hover_bike", "hover_car", "big_droid" };
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;

    private void Start()
    {
        // Ensure assets are loaded
        if (!VehicleAssetLoader.LoadAssets())
        {
            Debug.LogError("Failed to load vehicle assets!");
            return;
        }

        Debug.Log("Vehicle assets loaded. Ready to spawn.");
    }

    /// <summary>
    /// Spawn a vehicle by asset ID.
    /// </summary>
    public GameObject SpawnVehicleByAssetId(string assetId, Vector3? position = null)
    {
        // Get the asset data
        if (!VehicleAssetLoader.TryGetVehicleAsset(assetId, out VehicleAssetData.VehicleDefinition asset))
        {
            Debug.LogError($"Vehicle asset '{assetId}' not found!");
            return null;
        }

        return SpawnVehicleWithAsset(asset, position);
    }

    /// <summary>
    /// Spawn a vehicle using an asset definition.
    /// </summary>
    public GameObject SpawnVehicleWithAsset(VehicleAssetData.VehicleDefinition asset, Vector3? position = null)
    {
        Vector3 spawnPos = position ?? (transform.position + spawnOffset);

        // Instantiate the prefab
        GameObject vehicleObject = Instantiate(vehiclePrefab, spawnPos, Quaternion.identity);
        
        // Apply the asset data
        Vehicle vehicleComponent = vehicleObject.GetComponent<Vehicle>();
        if (vehicleComponent == null)
        {
            Debug.LogError("Vehicle prefab does not have a Vehicle component!", vehicleObject);
            Destroy(vehicleObject);
            return null;
        }

        VehicleAssetLoader.ApplyAssetToVehicle(vehicleComponent, asset);

        // If using Netcode, spawn on network
        NetworkObject netObject = vehicleObject.GetComponent<NetworkObject>();
        if (netObject != null && NetworkManager.Singleton != null)
        {
            netObject.Spawn();
            Debug.Log($"Spawned networked vehicle: {asset.name}");
        }
        else
        {
            Debug.Log($"Spawned local vehicle: {asset.name}");
        }

        return vehicleObject;
    }

    /// <summary>
    /// Spawn a vehicle at a random position from the configured list.
    /// </summary>
    public GameObject SpawnRandomVehicle(Vector3? position = null)
    {
        if (vehicleAssetIds.Length == 0)
        {
            Debug.LogError("No vehicle asset IDs configured!");
            return null;
        }

        string randomId = vehicleAssetIds[Random.Range(0, vehicleAssetIds.Length)];
        return SpawnVehicleByAssetId(randomId, position);
    }

    /// <summary>
    /// Spawn all configured vehicle types in a row.
    /// Useful for testing.
    /// </summary>
    public void SpawnAllVehicles()
    {
        Debug.Log($"Spawning {vehicleAssetIds.Length} vehicles...");
        
        for (int i = 0; i < vehicleAssetIds.Length; i++)
        {
            Vector3 offset = transform.position + (Vector3.right * i * 20f) + spawnOffset;
            SpawnVehicleByAssetId(vehicleAssetIds[i], offset);
        }
    }

    /// <summary>
    /// Debug: Print all available assets.
    /// </summary>
    public void DebugPrintAssets()
    {
        VehicleAssetLoader.PrintAllAssets();
    }

    // Example usage in OnGUI for testing
    private void OnGUI()
    {
        if (!Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label("Vehicle Spawner Test", GUI.skin.box);

        if (GUILayout.Button("Spawn Hover Bike"))
            SpawnVehicleByAssetId("hover_bike");

        if (GUILayout.Button("Spawn Hover Car"))
            SpawnVehicleByAssetId("hover_car");

        if (GUILayout.Button("Spawn Heavy Droid"))
            SpawnVehicleByAssetId("big_droid");

        if (GUILayout.Button("Spawn Random"))
            SpawnRandomVehicle();

        if (GUILayout.Button("Spawn All"))
            SpawnAllVehicles();

        if (GUILayout.Button("Print Assets"))
            DebugPrintAssets();

        GUILayout.EndArea();
    }
}
