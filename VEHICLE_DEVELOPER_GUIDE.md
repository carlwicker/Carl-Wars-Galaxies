# Vehicle Collision System - Developer Guide

## Overview

This guide explains how to work with the vehicle collision system in Carl Wars Galaxies. Vehicles are now fully collidable kinematic rigid bodies that act as solid obstacles in the world.

## Architecture Quick Reference

### Vehicle Component Stack

```csharp
// Vehicle.cs - Main script
public sealed class Vehicle : NetworkBehaviour
{
    // Collision components
    private BoxCollider bodyCollider;        // Collision shape
    private Rigidbody vehicleRigidbody;     // Physics body (kinematic)
    
    // Movement/state
    private NetworkVariable<Vector3> parkedPosition;
    private NetworkVariable<float> parkedYaw;
    private NetworkVariable<ulong> riderId;
    // ... other state variables
}
```

### Why Kinematic Rigidbody?

```
WRONG (Old): Static collider (no physics)
  └─ No collision callbacks
  └─ Raycasts don't see it properly
  └─ Had to toggle isTrigger (caused bugs)

RIGHT (New): Kinematic Rigidbody
  ├─ Position controlled by script: transform.position = newPos
  ├─ Rotation controlled by script: transform.rotation = newRot
  ├─ Physics engine doesn't affect it (no gravity, no forces)
  ├─ Collisions detected reliably
  ├─ Raycasts work perfectly
  └─ Network synchronization straightforward
```

## Implementation Details

### Collision Setup (Awake)

```csharp
private void Awake()
{
    // ... shader/material setup ...
    
    // Create collision body
    GameObject geometryObject = new GameObject("Vehicle Geometry");
    bodyCollider = geometryObject.AddComponent<BoxCollider>();
    
    // Add kinematic rigidbody
    vehicleRigidbody = geometryObject.AddComponent<Rigidbody>();
    vehicleRigidbody.isKinematic = true;      // Position controlled by script
    vehicleRigidbody.useGravity = false;      // Hover system handles height
    
    // Calculate collision bounds later in RebuildGeometry()
    // bodyCollider.size = dimensions;
    // bodyCollider.center = new Vector3(0f, dimensions.y * 0.5f, 0f);
}
```

### Position Updates (LateUpdate)

```csharp
private void LateUpdate()
{
    if (!IsSpawned || !ready.Value || geometry == null) return;

    // If occupied, follow the rider
    if (IsOccupied && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(riderId.Value, out NetworkObject rider))
    {
        CharacterController controller = rider.GetComponent<CharacterController>();
        float footOffset = controller != null ? controller.height * 0.5f - controller.center.y : 1f;
        
        // This directly moves the vehicle's transform
        // The BoxCollider + Rigidbody move with it automatically
        transform.position = rider.transform.position - Vector3.up * footOffset;
        transform.rotation = Quaternion.Euler(0f, rider.transform.eulerAngles.y, 0f);
        
        // ... persistence logic ...
    }
    else
    {
        // Vehicle is parked - update from network state
        transform.SetPositionAndRotation(
            parkedPosition.Value,
            Quaternion.Euler(0f, parkedYaw.Value, 0f)
        );
    }

    // Handle hover rise/fall animation
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

    // Move visual geometry up for hover effect
    // (BoxCollider stays on Vehicle Geometry game object, moves with it)
    geometry.localPosition = Vector3.up * currentHoverHeight;
    
    // ... sway and light effects ...
    
    // KEY: Never set isTrigger anymore!
    // bodyCollider.isTrigger remains false forever
}
```

### Mounting (No Trigger Needed)

```csharp
public bool TryMount(PlayerInteraction player)
{
    if (!IsServer || !IsReady || !IsOn || IsOccupied) 
        return false;
    
    // Set the rider
    riderId.Value = player.NetworkObjectId;
    
    // That's it! No need to disable collision.
    // The player teleports to the vehicle's position in VehicleInteraction.MountServerRpc()
    // So collision doesn't matter - they're at the same location.
    
    return true;
}
```

## Common Patterns

### Checking if Player Can Interact

```csharp
// From VehicleInteraction.cs
public void OpenMenu(Vehicle vehicle, Vector2 position)
{
    if (!IsOwner || vehicle == null) return;
    
    // Check range (uses BoxCollider.ClosestPoint)
    if (!vehicle.IsWithinRange(transform.position, InteractionRange + 5f))
        return;
    
    selectedVehicle = vehicle;
    menuOpen = true;
}

// In Vehicle.cs
public bool IsWithinRange(Vector3 position, float range)
{
    return bodyCollider != null
        && (bodyCollider.ClosestPoint(position) - position).sqrMagnitude <= range * range;
}
```

### Ground Detection with Raycasting

```csharp
private bool TryGetGroundHeight(Vector3 position, out float height)
{
    // Cast down from high above
    RaycastHit[] hits = Physics.RaycastAll(
        position + Vector3.up * 100f,     // Start high
        Vector3.down,                      // Down direction
        200f,                              // 200 units range
        Physics.DefaultRaycastLayers,      // All layers
        QueryTriggerInteraction.Ignore     // Skip triggers
    );
    
    bool found = false;
    height = float.NegativeInfinity;
    
    foreach (RaycastHit hit in hits)
    {
        // Skip vehicles, players, pickups
        // Only accept terrain/structures
        if (hit.collider.GetComponentInParent<Vehicle>() == null
            && hit.collider.GetComponentInParent<PlayerInteraction>() == null
            && hit.collider.GetComponentInParent<WorldPickup>() == null
            && hit.point.y > height)
        {
            height = hit.point.y;
            found = true;
        }
    }
    
    return found;
}
```

## Network Synchronization

### What Gets Synced

```csharp
private readonly NetworkVariable<Vector3> parkedPosition = new NetworkVariable<Vector3>();
private readonly NetworkVariable<float> parkedYaw = new NetworkVariable<float>();
private readonly NetworkVariable<bool> poweredOn = new NetworkVariable<bool>();
private readonly NetworkVariable<ulong> riderId = new NetworkVariable<ulong>(NoRider);

// These are automatically replicated to all clients
// BoxCollider position updates on all clients because transform updates sync
```

### Save/Load System

```csharp
public HostInventoryStore.SavedVehicle CreateSave()
{
    return new HostInventoryStore.SavedVehicle
    {
        vehicleId = vehicleId,
        x = parkedPosition.Value.x,
        y = parkedPosition.Value.y,
        z = parkedPosition.Value.z,
        yaw = parkedYaw.Value,
        engineInstalled = engineInstalled.Value,
        powerInstalled = powerInstalled.Value,
        isOn = poweredOn.Value,
        cargo = /* ... */
    };
}

// On spawn, vehicle loads its last saved position
// Collision works from the first frame
```

## Troubleshooting

### Vehicle Clips Through Terrain

**Symptoms:** Vehicle spawns partially below ground

**Cause:** `TryGetGroundHeight()` raycast failed
- Terrain might not have a collider
- Raycast layer mask misconfigured
- Ground detection skips happened before finding ground

**Fix:**
```csharp
// Add debug visualization
private bool TryGetGroundHeight(Vector3 position, out float height)
{
    RaycastHit[] hits = Physics.RaycastAll(/* ... */);
    
    // DEBUG
    Debug.DrawLine(position + Vector3.up * 100f, position - Vector3.up * 100f, Color.red, 2f);
    
    // ... rest of method ...
}
```

### Player Can Walk Through Vehicle

**Symptoms:** Players pass right through vehicles as if they don't exist

**Cause:** `bodyCollider.isTrigger` was set to `true`
- Old code set `isTrigger = IsOccupied` (WRONG!)
- Triggers detect overlaps but don't block movement

**Fix:**
```csharp
// WRONG - DELETE THIS:
bodyCollider.isTrigger = IsOccupied;

// CORRECT - NEVER SET isTrigger:
// Leave it as false (default)
```

### Vehicle Doesn't Move When Mounted

**Symptoms:** Player mounts vehicle but vehicle stays in place

**Cause:** Position not being updated in LateUpdate

**Check:**
```csharp
// Ensure in LateUpdate():
if (IsOccupied && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(riderId.Value, out NetworkObject rider))
{
    transform.position = rider.transform.position - Vector3.up * footOffset;
    // ... this should run every frame while mounted
}
```

### Raycasts Don't Hit Vehicle

**Symptoms:** `Physics.Raycast()` doesn't detect the vehicle

**Cause:** Raycast layer mask misconfigured OR vehicle on wrong layer

**Fix:**
```csharp
// Check vehicle's GameObject layer
// If it's on "Ignore Raycast" layer, raycasts won't hit it
// Set to "Default" or appropriate layer

// Or use specific layer mask:
RaycastHit hit;
if (Physics.Raycast(origin, direction, out hit, distance, LayerMask.GetMask("Default")))
{
    // Will hit vehicles on Default layer
}
```

## Performance Optimization Tips

### Raycasting Efficiency
```csharp
// Reuse array to avoid allocation
private static RaycastHit[] reuseableHits = new RaycastHit[64];

private bool TryGetGroundHeight(Vector3 position, out float height)
{
    // Use array parameter (no allocation)
    int count = Physics.RaycastNonAlloc(
        position + Vector3.up * 100f,
        Vector3.down,
        reuseableHits,
        200f,
        Physics.DefaultRaycastLayers,
        QueryTriggerInteraction.Ignore
    );
    
    // Process only valid hits
    for (int i = 0; i < count; i++)
    {
        // ... process hits ...
    }
}
```

### Collision Detection Layers
```csharp
// In Project Settings > Layers
// Add custom layer "Vehicle" for optimization

// Use layer masks for specific queries
int vehicleLayer = LayerMask.NameToLayer("Vehicle");
int vehicleMask = 1 << vehicleLayer;

// Only detect vehicle collisions when needed
if (Physics.CheckSphere(position, radius, vehicleMask))
{
    // Vehicle nearby
}
```

### LOD System (Future)
```csharp
// For large vehicle counts, implement LOD:
// - Full physics at close range
// - Simplified collision at distance
// - Or use spatial partitioning for queries
```

## Extending the System

### Adding Component-Based Damage

```csharp
// Future: Track individual cube damage
// Each cube could have health separate from vehicle

[SerializeField] private int[,,] cubeHealthGrid;

private void InitializeCubeGrid()
{
    cubeHealthGrid = new int[dimensions.x, dimensions.y, dimensions.z];
    for (int x = 0; x < dimensions.x; x++)
        for (int y = 0; y < dimensions.y; y++)
            for (int z = 0; z < dimensions.z; z++)
                cubeHealthGrid[x, y, z] = 100;
}

public void DamageCube(Vector3 worldPosition, int damage)
{
    // Find which cube was hit
    // Reduce health
    // Visual effect if destroyed
}
```

### Adding Vehicle Push/Knockback

```csharp
// When a heavy object hits vehicle at speed
public void OnCollisionEnter(Collision collision)
{
    Vector3 force = collision.relativeVelocity;
    // Push vehicle in opposite direction
    // parkedPosition.Value += force * knockbackMultiplier;
}
```

### Custom Collision Shapes

```csharp
// Replace single BoxCollider with multiple colliders
// for detailed hit detection:

// In Awake():
// - Cargo area collider (soft collision?)
// - Engine area collider (critical hit zone)
// - Hull collider (normal collision)

// Use different layers for different areas
```

## References

- **Script:** [Vehicle.cs](Assets/Vehicle.cs)
- **System Guide:** [VEHICLE_SYSTEM_GUIDE.md](VEHICLE_SYSTEM_GUIDE.md)
- **Collision Reference:** [VEHICLE_COLLISION_SYSTEM.md](VEHICLE_COLLISION_SYSTEM.md)
- **Visual Guide:** [VEHICLE_COLLISION_VISUAL_GUIDE.md](VEHICLE_COLLISION_VISUAL_GUIDE.md)
- **Quick Ref:** [VEHICLE_COLLISION_QUICK_REFERENCE.md](VEHICLE_COLLISION_QUICK_REFERENCE.md)

## Related Scripts

- **VehicleInteraction.cs** - User interaction and mounting
- **PlayerMovement.cs** - Character controller (respects vehicle collision)
- **PlayerInteraction.cs** - Interaction menu system
- **WorldPickup.cs** - Items (skipped in vehicle raycasts)
- **UnevenGround.cs** - Terrain system

