using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class HostInventoryStore
{
    [Serializable]
    public sealed class SavedItem
    {
        public string itemId;
        public string itemName;
        public int count;
        public int quality;
        public int conductivity;
        public int speed;
        public int experimentPoints;
        public float energyUsed;
        public int potentialEnergy;
        public int coldResistance;
        public int decayResistance;
        public int entangleResistance;
    }

    [Serializable]
    public sealed class SavedPickup
    {
        public string pickupId;
        public string itemId;
        public string itemName;
        public int count;
        public int quality;
        public int conductivity;
        public int speed;
        public int experimentPoints;
        public float energyUsed;
        public int potentialEnergy;
        public int coldResistance;
        public int decayResistance;
        public int entangleResistance;
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    public sealed class SavedVehicle
    {
        public string vehicleId;
        public float x;
        public float y;
        public float z;
        public float yaw;
        public bool engineInstalled;
        public int engineQuality;
        public int engineSpeed;
        public int engineExperimentPoints;
        public bool powerInstalled;
        public int powerQuality;
        public float powerEnergyUsed;
        public bool isOn;
        public List<SavedItem> cargo = new List<SavedItem>();
    }

    [Serializable]
    private sealed class PlayerSave
    {
        public string playerId;
        public List<SavedItem> items = new List<SavedItem>();
    }

    [Serializable]
    private sealed class SaveData
    {
        public List<PlayerSave> players = new List<PlayerSave>();
        public List<string> collectedPickups = new List<string>();
        public List<SavedPickup> droppedPickups = new List<SavedPickup>();
        public List<SavedVehicle> vehicles = new List<SavedVehicle>();
    }

    private static string SavePath => Path.Combine(Application.persistentDataPath, "host-inventory.json");

    public static List<SavedItem> LoadInventory(string playerId)
    {
        SaveData data = ReadSaveData();
        for (int i = 0; i < data.players.Count; i++)
        {
            if (data.players[i] == null)
            {
                throw new InvalidDataException("The host inventory save file contains an invalid player entry.");
            }

            if (data.players[i].playerId == playerId)
            {
                return CloneItems(data.players[i].items);
            }
        }

        return new List<SavedItem>();
    }

    public static List<string> LoadCollectedPickups()
    {
        return new List<string>(ReadSaveData().collectedPickups);
    }

    public static List<SavedPickup> LoadDroppedPickups()
    {
        List<SavedPickup> pickups = ReadSaveData().droppedPickups;
        List<SavedPickup> clones = new List<SavedPickup>(pickups.Count);
        for (int i = 0; i < pickups.Count; i++)
        {
            SavedPickup pickup = pickups[i];
            if (pickup != null)
            {
                clones.Add(ClonePickup(pickup));
            }
        }

        return clones;
    }

    public static SavedVehicle LoadVehicle(string vehicleId)
    {
        foreach (SavedVehicle vehicle in ReadSaveData().vehicles)
        {
            if (vehicle == null) throw new InvalidDataException("The host save contains an invalid vehicle.");
            if (vehicle.vehicleId == vehicleId) return vehicle;
        }
        return null;
    }

    public static void SaveVehicle(SavedVehicle vehicle)
    {
        SaveData data = ReadSaveData();
        SetVehicle(data, vehicle);
        WriteSaveData(data);
    }

    public static void SaveInventory(
        string playerId,
        List<SavedItem> items,
        string collectedPickupId = null,
        SavedPickup droppedPickup = null,
        string removedDroppedPickupId = null,
        SavedVehicle vehicle = null)
    {
        SaveData data = ReadSaveData();
        PlayerSave player = null;
        for (int i = 0; i < data.players.Count; i++)
        {
            if (data.players[i] == null)
            {
                throw new InvalidDataException("The host inventory save file contains an invalid player entry.");
            }

            if (data.players[i].playerId == playerId)
            {
                player = data.players[i];
                break;
            }
        }

        if (player == null)
        {
            player = new PlayerSave { playerId = playerId };
            data.players.Add(player);
        }

        player.items = CloneItems(items);
        if (!string.IsNullOrEmpty(collectedPickupId)
            && !data.collectedPickups.Contains(collectedPickupId))
        {
            data.collectedPickups.Add(collectedPickupId);
        }

        if (!string.IsNullOrEmpty(removedDroppedPickupId))
        {
            for (int i = data.droppedPickups.Count - 1; i >= 0; i--)
            {
                if (data.droppedPickups[i] != null
                    && data.droppedPickups[i].pickupId == removedDroppedPickupId)
                {
                    data.droppedPickups.RemoveAt(i);
                }
            }
        }

        if (droppedPickup != null)
        {
            bool replaced = false;
            for (int i = 0; i < data.droppedPickups.Count; i++)
            {
                if (data.droppedPickups[i] != null
                    && data.droppedPickups[i].pickupId == droppedPickup.pickupId)
                {
                    data.droppedPickups[i] = ClonePickup(droppedPickup);
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                data.droppedPickups.Add(ClonePickup(droppedPickup));
            }
        }

        if (vehicle != null) SetVehicle(data, vehicle);
        WriteSaveData(data);
    }

    private static void SetVehicle(SaveData data, SavedVehicle vehicle)
    {
        if (vehicle == null || string.IsNullOrWhiteSpace(vehicle.vehicleId))
            throw new InvalidDataException("Vehicle save has no ID.");

        for (int i = 0; i < data.vehicles.Count; i++)
        {
            if (data.vehicles[i] == null) throw new InvalidDataException("The host save contains an invalid vehicle.");
            if (data.vehicles[i].vehicleId == vehicle.vehicleId)
            {
                data.vehicles[i] = vehicle;
                return;
            }
        }
        data.vehicles.Add(vehicle);
    }

    private static SaveData ReadSaveData()
    {
        if (!File.Exists(SavePath))
        {
            return new SaveData();
        }

        string json = File.ReadAllText(SavePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException("The host inventory save file is empty.");
        }

        SaveData data = JsonUtility.FromJson<SaveData>(json);
        if (data == null)
        {
            throw new InvalidDataException("The host inventory save file is invalid.");
        }

        if (data.players == null)
        {
            data.players = new List<PlayerSave>();
        }

        if (data.collectedPickups == null)
        {
            data.collectedPickups = new List<string>();
        }

        if (data.droppedPickups == null)
        {
            data.droppedPickups = new List<SavedPickup>();
        }

        if (data.vehicles == null)
        {
            data.vehicles = new List<SavedVehicle>();
        }

        return data;
    }

    private static void WriteSaveData(SaveData data)
    {
        Directory.CreateDirectory(Application.persistentDataPath);
        string temporaryPath = SavePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonUtility.ToJson(data));

        if (File.Exists(SavePath))
        {
            File.Replace(temporaryPath, SavePath, null);
        }
        else
        {
            File.Move(temporaryPath, SavePath);
        }
    }

    private static List<SavedItem> CloneItems(List<SavedItem> items)
    {
        List<SavedItem> clones = new List<SavedItem>();
        if (items == null) return clones;

        for (int i = 0; i < items.Count; i++)
        {
            SavedItem item = items[i];
            if (item != null)
            {
                clones.Add(new SavedItem
                {
                    itemId = item.itemId,
                    itemName = item.itemName,
                    count = item.count,
                    quality = item.quality,
                    conductivity = item.conductivity,
                    speed = item.speed,
                    experimentPoints = item.experimentPoints,
                    energyUsed = item.energyUsed,
                    potentialEnergy = item.potentialEnergy,
                    coldResistance = item.coldResistance,
                    decayResistance = item.decayResistance,
                    entangleResistance = item.entangleResistance
                });
            }
        }

        return clones;
    }

    private static SavedPickup ClonePickup(SavedPickup pickup)
    {
        return new SavedPickup
        {
            pickupId = pickup.pickupId,
            itemId = pickup.itemId,
            itemName = pickup.itemName,
            count = pickup.count,
            quality = pickup.quality,
            conductivity = pickup.conductivity,
            speed = pickup.speed,
            experimentPoints = pickup.experimentPoints,
            energyUsed = pickup.energyUsed,
            potentialEnergy = pickup.potentialEnergy,
            coldResistance = pickup.coldResistance,
            decayResistance = pickup.decayResistance,
            entangleResistance = pickup.entangleResistance,
            x = pickup.x,
            y = pickup.y,
            z = pickup.z
        };
    }
}
