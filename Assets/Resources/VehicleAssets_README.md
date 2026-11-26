# Vehicle Asset System

A JSON-based data-driven system for defining and managing vehicle assets in Carl Wars Galaxies.

## Overview

Instead of manually configuring each vehicle prefab in Unity, all vehicle definitions are stored in `VehicleAssets.json`. This approach makes it easy to:
- **Create new vehicles** by adding a JSON entry
- **Adjust vehicle stats** without touching code
- **Share vehicle definitions** across the team
- **Version control** vehicle designs easily
- **Hot-reload** vehicle data at runtime (with minimal code)

## File Structure

```
Assets/
├── Resources/
│   ├── VehicleAssets.json              # Vehicle definitions
│   └── VehicleAssets_README.md         # This file
├── Vehicle.cs                          # Base vehicle component (unchanged)
├── VehicleAssetLoader.cs               # Loads and applies assets
└── VehicleInteraction.cs               # Interaction system (unchanged)
```

## JSON Schema

Each vehicle in `VehicleAssets.json` requires:

```json
{
  "id": "unique_vehicle_id",
  "name": "Display Name",
  "description": "What this vehicle is for",
  "dimensions": {
    "width": 1,
    "height": 1,
    "length": 4
  },
  "cargoSlots": 4,
  "color": {
    "r": 0.08,
    "g": 0.28,
    "b": 0.38,
    "a": 1.0
  },
  "physics": {
    "hoverRiseSpeed": 3.0,
    "hoverDropSpeed": 2.0
  },
  "dedicatedSlots": {
    "engine": 1,
    "power": 1
  },
  "tags": ["personal", "fast", "light"],
  "cost": 50000
}
```

### Field Descriptions

| Field | Type | Purpose |
|-------|------|---------|
| `id` | string | Unique identifier (e.g., `"hover_bike"`). Used for saves and references. |
| `name` | string | Display name in game. |
| `description` | string | Lore/mechanic description. |
| `dimensions.width` | int | X-axis size in cube units. |
| `dimensions.height` | int | Y-axis size in cube units. |
| `dimensions.length` | int | Z-axis size in cube units. |
| `cargoSlots` | int | Number of inventory slots. |
| `color` | object | RGBA color (0.0–1.0 range). |
| `physics.hoverRiseSpeed` | float | Speed ascending when powered on. |
| `physics.hoverDropSpeed` | float | Speed descending when powered off. |
| `dedicatedSlots.engine` | int | Number of engine cube slots (usually 1). |
| `dedicatedSlots.power` | int | Number of power cube slots (usually 1). |
| `tags` | array | Gameplay tags for filtering/sorting. |
| `cost` | int | NPC purchase price (future shop system). |

## Usage

### 1. Add a New Vehicle Type

Edit `Assets/Resources/VehicleAssets.json` and add a new entry to the `vehicles` array:

```json
{
  "id": "racing_pod",
  "name": "Racing Pod",
  "description": "High-speed personal racer",
  "dimensions": { "width": 1, "height": 1, "length": 3 },
  "cargoSlots": 2,
  "color": { "r": 1.0, "g": 0.2, "b": 0.2, "a": 1.0 },
  "physics": {
    "hoverRiseSpeed": 5.0,
    "hoverDropSpeed": 3.0
  },
  "dedicatedSlots": { "engine": 1, "power": 1 },
  "tags": ["racing", "fast", "lightweight"],
  "cost": 180000
}
```

### 2. Apply Asset to a Vehicle at Runtime

In editor or at runtime, call:

```csharp
// Option 1: By vehicle ID
VehicleAssetLoader.ApplyAssetToVehicle(vehicleComponent, "racing_pod");

// Option 2: By asset object
VehicleAssetData.VehicleDefinition asset = ...;
VehicleAssetLoader.ApplyAssetToVehicle(vehicleComponent, asset);
```

### 3. Load All Assets Programmatically

```csharp
// Load from JSON
if (VehicleAssetLoader.LoadAssets())
{
    Debug.Log("All vehicle assets loaded successfully!");
}

// Get a specific asset
if (VehicleAssetLoader.TryGetVehicleAsset("hover_bike", out var asset))
{
    Debug.Log($"Found: {asset.name}");
}

// Print all loaded assets
VehicleAssetLoader.PrintAllAssets();
```

## Integration with Vehicle.cs

The existing `Vehicle.cs` serialized fields remain untouched:

```csharp
[SerializeField] private string vehicleId;
[SerializeField] private string vehicleName = "Vehicle";
[SerializeField] private Vector3Int dimensions = new Vector3Int(1, 1, 4);
[SerializeField, Min(0)] private int cargoSlotCount = 4;
[SerializeField] private Color color = new Color(0.08f, 0.28f, 0.38f);
[SerializeField, Min(0.1f)] private float hoverRiseSpeed = 3f;
[SerializeField, Min(0.1f)] private float hoverDropSpeed = 2f;
```

`VehicleAssetLoader.ApplyAssetToVehicle()` passes these values to `Vehicle.ConfigureFromAsset()` before network spawn. The vehicle then rebuilds its cube geometry and collider using the configured dimensions.

## Workflow Examples

### Example 1: Create a New Vehicle Prefab

1. Create an empty GameObject in your scene.
2. Add the `Vehicle` component (attach `Vehicle.cs`).
3. Set a temporary `vehicleId` (e.g., "temp_vehicle").
4. Add `VehicleAssetLoader` to any GameObject and enable `autoLoadOnStart`.
5. In the Scene, call `VehicleAssetLoader.ApplyAssetToVehicle(vehicleComp, "hover_bike")`.
6. Configure any other networking/gameplay settings.
7. Save as a prefab.

### Example 2: Bulk Update Vehicle Colors

Edit `VehicleAssets.json` and update the color values. On game restart, all vehicles will adopt the new colors.

### Example 3: Add Variants

Create variants by cloning an asset with a new ID:

```json
{
  "id": "hover_bike_military",
  "name": "Military Hover Bike",
  "description": "Armor-reinforced hover bike",
  "dimensions": { "width": 1, "height": 1, "length": 4 },
  "cargoSlots": 2,
  "color": { "r": 0.2, "g": 0.2, "b": 0.2, "a": 1.0 },
  "physics": {
    "hoverRiseSpeed": 2.5,
    "hoverDropSpeed": 2.0
  },
  "dedicatedSlots": { "engine": 1, "power": 1 },
  "tags": ["military", "heavy", "durable"],
  "cost": 150000
}
```

## Future Enhancements

The JSON structure is extensible. Consider adding:

```json
{
  "armor": 50,
  "handling": 0.8,
  "maxSpeed": 150,
  "powerDrain": 2.5,
  "maxPassengers": 2,
  "craftingRequirements": {
    "metals": 100,
    "electronics": 50
  },
  "moduleSlots": [
    { "name": "weapon", "count": 2 },
    { "name": "sensor", "count": 1 },
    { "name": "armor", "count": 4 }
  ]
}
```

Then extend `VehicleAssetData` and `Vehicle.cs` to support these new fields.

## Tips

- **Keep IDs lowercase with underscores** for consistency (e.g., `hover_bike`, not `Hover Bike`).
- **Use descriptive tags** to group vehicles by role (combat, transport, scout, etc.).
- **Comment your JSON** where non-obvious (use a separate `.jsonc` file or comments in your design docs).
- **Test new vehicles** with the included archetypes (hover_bike, hover_car, big_droid) first.
- **Version your assets** in source control so changes can be reviewed and reverted.

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "VehicleAssets.json not found" | Ensure the file is in `Assets/Resources/` and named exactly `VehicleAssets.json`. |
| JSON parse errors | Validate JSON syntax at [jsonlint.com](https://www.jsonlint.com). Check for missing commas. |
| Settings not applying | Apply the asset before calling `NetworkObject.Spawn()`, and check the Unity Console for validation errors. |
| Cache not updating | Call `VehicleAssetLoader.LoadAssets()` again after editing JSON. |

---

**Last Updated:** 2026  
**Creator:** Copilot  
**License:** Included in Carl Wars Galaxies project
