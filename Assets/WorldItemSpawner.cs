using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class ResourceConcentrationGrid
{
    public struct Hotspot
    {
        public string ResourceId { get; }
        public string DisplayName { get; }
        public Vector3 Position { get; }
        public int Concentration { get; }
        public int OverallQuality { get; }
        public int Conductivity { get; }
        public int PotentialEnergy { get; }
        public int ColdResistance { get; }
        public int DecayResistance { get; }
        public int EntangleResistance { get; }

        public Hotspot(string resourceId, string displayName, Vector3 position,
            int concentration, int overallQuality, int conductivity, int potentialEnergy = 0,
            int coldResistance = 0, int decayResistance = 0, int entangleResistance = 0)
        {
            ResourceId = resourceId;
            DisplayName = displayName;
            Position = position;
            Concentration = concentration;
            OverallQuality = overallQuality;
            Conductivity = conductivity;
            PotentialEnergy = potentialEnergy;
            ColdResistance = coldResistance;
            DecayResistance = decayResistance;
            EntangleResistance = entangleResistance;
        }
    }

    private struct ResourceDefinition
    {
        public string Id;
        public string Name;

        public ResourceDefinition(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    private static readonly ResourceDefinition[] ResourceTypes =
    {
        new ResourceDefinition("metal", "Metal"),
        new ResourceDefinition("petrochemicals", "Petrochemicals"),
        new ResourceDefinition("energy", "Energy"),
        new ResourceDefinition("radioactive", "Radioactive")
    };

    private static readonly List<Hotspot> hotspots = new List<Hotspot>();

    public static IReadOnlyList<Hotspot> Hotspots => hotspots;

    public static void Generate(int seed, Vector2 minimum, Vector2 maximum, int hotspotsPerResource)
    {
        if (minimum.x >= maximum.x || minimum.y >= maximum.y)
            throw new ArgumentException("Resource grid bounds must have positive area.");
        if (hotspotsPerResource <= 0)
            throw new ArgumentOutOfRangeException(nameof(hotspotsPerResource));

        System.Random random = new System.Random(seed);
        hotspots.Clear();
        foreach (ResourceDefinition resource in ResourceTypes)
        {
            for (int i = 0; i < hotspotsPerResource; i++)
            {
                Vector3 position = new Vector3(
                    Mathf.Lerp(minimum.x, maximum.x, (float)random.NextDouble()),
                    0f,
                    Mathf.Lerp(minimum.y, maximum.y, (float)random.NextDouble()));
                int overallQuality = random.Next(100, 1001);
                int concentration = random.Next(55, 101);
                if (resource.Id == "radioactive")
                {
                    hotspots.Add(new Hotspot(resource.Id, resource.Name, position, concentration,
                        overallQuality, 0, random.Next(100, 1001), random.Next(100, 1001),
                        random.Next(100, 1001), random.Next(100, 1001)));
                }
                else
                {
                    hotspots.Add(new Hotspot(resource.Id, resource.Name, position, concentration,
                        overallQuality, random.Next(100, 1001)));
                }
            }
        }
    }

    public static bool TryFindNearest(Vector3 position, float range, out Hotspot result, out float distance)
    {
        result = default;
        distance = 0f;
        if (float.IsNaN(position.x) || float.IsInfinity(position.x)
            || float.IsNaN(position.z) || float.IsInfinity(position.z)
            || float.IsNaN(range) || float.IsInfinity(range) || range <= 0f) return false;

        float nearestDistanceSquared = range * range;
        bool found = false;
        foreach (Hotspot hotspot in hotspots)
        {
            Vector2 offset = new Vector2(position.x - hotspot.Position.x, position.z - hotspot.Position.z);
            float distanceSquared = offset.sqrMagnitude;
            if (distanceSquared > nearestDistanceSquared) continue;

            nearestDistanceSquared = distanceSquared;
            result = hotspot;
            found = true;
        }

        if (found) distance = Mathf.Sqrt(nearestDistanceSquared);
        return found;
    }
}

public sealed class WorldItemSpawner : MonoBehaviour
{
    private static readonly Vector2[] PowerCubePositions =
    {
        new Vector2(3f, 4f), new Vector2(11f, -4f),
        new Vector2(15f, 12f), new Vector2(16f, 25f),
        new Vector2(10f, 30f), new Vector2(4f, -12f)
    };

    private void Start()
    {
        for (int i = 0; i < PowerCubePositions.Length; i++)
        {
            Vector3 origin = new Vector3(PowerCubePositions[i].x, 100f, PowerCubePositions[i].y);
            if (!TryFindGround(origin, out Vector3 ground, $"power cube {i}")) continue;
            const string itemId = "power_cube";
            const string itemName = "Power Cube";
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = $"Pickup - {itemName}";
            cube.transform.position = ground + Vector3.up * 0.38f;
            cube.transform.localScale = Vector3.one * 0.76f;
            cube.GetComponent<Renderer>().material.color = new Color(0.2f, 0.8f, 1f);
            cube.AddComponent<WorldPickup>().Initialize($"vehicle-part-{i:00}", itemId, itemName, 1);
        }

        DateTime utcDate = DateTime.UtcNow.Date;
        string dateId = utcDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        int seed = utcDate.Year * 1000 + utcDate.DayOfYear;
        ResourceConcentrationGrid.Generate(seed, new Vector2(-22f, -14f), new Vector2(22f, 34f), 3);

        for (int hotspotIndex = 0; hotspotIndex < ResourceConcentrationGrid.Hotspots.Count; hotspotIndex++)
        {
            ResourceConcentrationGrid.Hotspot hotspot = ResourceConcentrationGrid.Hotspots[hotspotIndex];
            Vector2[] nodeOffsets = hotspot.ResourceId == "energy"
                ? new[]
                {
                    new Vector2(-1.5f, 0f), new Vector2(1.5f, 0f),
                    new Vector2(0f, -1.5f), new Vector2(0f, 1.5f)
                }
                : new[] { new Vector2(-0.55f, 0f), new Vector2(0.55f, 0f) };

            for (int nodeIndex = 0; nodeIndex < nodeOffsets.Length; nodeIndex++)
            {
                Vector3 offset = new Vector3(nodeOffsets[nodeIndex].x, 0f, nodeOffsets[nodeIndex].y);
                Vector3 origin = hotspot.Position + offset + Vector3.up * 100f;
                if (!TryFindGround(origin, out Vector3 ground, $"{hotspot.DisplayName} resource")) continue;

                string itemName = hotspot.ResourceId == "energy"
                    ? "Energy Cube"
                    : $"{hotspot.DisplayName} resource";
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = itemName;
                cube.transform.position = ground + Vector3.up * 0.38f;
                cube.transform.localScale = Vector3.one * 0.76f;
                cube.GetComponent<Renderer>().material.color = GetResourceColor(hotspot.ResourceId);
                int count = hotspot.ResourceId == "metal" || hotspot.ResourceId == "radioactive" ? 5 : 3;
                string resourceCode = hotspot.ResourceId == "metal" ? "m"
                    : hotspot.ResourceId == "petrochemicals" ? "p"
                    : hotspot.ResourceId == "energy" ? "e" : "a";
                string pickupId = $"r{dateId}{resourceCode}{hotspotIndex:00}{nodeIndex}";
                cube.AddComponent<WorldPickup>().Initialize(pickupId, hotspot.ResourceId,
                    itemName, count, hotspot.OverallQuality, 0, 0, hotspot.Conductivity,
                    0f, hotspot.PotentialEnergy, hotspot.ColdResistance,
                    hotspot.DecayResistance, hotspot.EntangleResistance);
            }
        }
    }

    private bool TryFindGround(Vector3 origin, out Vector3 ground, string itemName)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 200f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponent<UnevenGround>() == null) continue;
            ground = hit.point;
            return true;
        }

        Debug.LogError($"Could not find ground for {itemName}.", this);
        ground = Vector3.zero;
        return false;
    }

    private static Color GetResourceColor(string resourceId)
    {
        switch (resourceId)
        {
            case "metal": return Color.gray;
            case "petrochemicals": return new Color(0.55f, 0.3f, 0.1f);
            case "energy": return new Color(0.95f, 0.82f, 0.18f);
            case "radioactive": return new Color(0.35f, 0.9f, 0.25f);
            default: return Color.white;
        }
    }
}
