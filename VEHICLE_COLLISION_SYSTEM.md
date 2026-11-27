# Vehicle Collision System Implementation

## Overview
Vehicles in Carl Wars Galaxies are now fully collidable physics objects in the world. This enables:
- **Physical collision detection** with terrain, other vehicles, and world objects
- **Proper blocking behavior** so players and other entities cannot pass through vehicles
- **Realistic obstacle interaction** as vehicles are treated as kinematic rigid bodies

## Architecture

### Key Components

**Vehicle.cs**
- **BoxCollider**: Scaled to vehicle dimensions, always solid (never a trigger)
- **Rigidbody**: Kinematic mode for consistent collision without gravity
  - `isKinematic = true`: Prevents physics simulation; position/rotation controlled via scripts
  - `useGravity = false`: No automatic gravity application (hover system manages vertical position)
  - `isTrigger = false`: **Always false** - vehicles are solid obstacles

### Why This Design

1. **Kinematic Rigidbody**: 
   - Provides reliable collision callbacks and raycasting
   - Allows network synchronization without competing with physics engine
   - Prevents unintended behavior when players mount/dismount

2. **Solid Collider (Never a Trigger)**:
   - Previous implementation toggled `isTrigger` based on `IsOccupied`
   - This was **incorrect** - it allowed pass-through when occupied
   - Now vehicles are **always solid**, maintaining consistent collision behavior

3. **Player Mounting**:
   - Players mount via standard vehicle interaction (radial menu → "Ride")
   - The vehicle remains solid to everyone except its own rider
   - Each client ignores the rider/vehicle collider pair while mounted and restores it on dismount

4. **Driving**:
   - The rider's movement sweeps the full vehicle box against solid obstacles
   - Turning checks the vehicle footprint, and movement checks the resulting controller position
   - Trigger colliders and layers excluded by the collision matrix do not block the vehicle

## Collision Behavior

### Vehicle-to-World Interactions
- **Terrain**: Vehicles collide with static geometry and respect hover height
- **Other Vehicles**: Driven vehicles are blocked by other solid vehicle bodies
- **World Objects**: Solid world colliders block driven vehicles
- **Players**: Other players are blocked by the vehicle hull; its own rider is exempt

### Mount/Dismount Mechanics
- **Mount**: Player teleports to vehicle center, begins following it
  - Collision persists for other objects; only the rider/vehicle pair is ignored
  - Rider height is adjusted via `PlayerInteraction.SetVehicleRiderHeight()`
  
- **Dismount**: Player teleports to exit position (offset from vehicle)
  - Exit position calculated with ground-height raycast
  - Vehicle stays in world, fully collidable
  - Position saved to persistent storage

## Usage Examples

### Spawning a Vehicle with Collision
```csharp
Vehicle vehicle = Instantiate(vehiclePrefab);
vehicle.ConfigureFromAsset(
    id: "speeder-001",
    displayName: "Hover Speeder",
    assetDimensions: new Vector3Int(1, 3, 5),
    assetCargoSlotCount: 12,
    assetColor: Color.cyan,
    assetHoverRiseSpeed: 3f,
    assetHoverDropSpeed: 2f
);
NetworkObject networkObject = vehicle.GetComponent<NetworkObject>();
networkObject.Spawn();
```

The vehicle is now:
- ✅ Collidable with the world
- ✅ Blocked by terrain and structures
- ✅ Visible as an obstacle to players
- ✅ Fully persistent across network

### Interacting with Vehicles
Players can interact via the radial menu:
1. **Range Check**: Within 5 units of vehicle
2. **Line of Sight**: Not required (through walls is OK)
3. **Menu Options**:
   - **Turn on/off**: Change power state
   - **Ride**: Mount vehicle (if powered and unoccupied)
   - **Inventory**: Transfer items between player and cargo

## Physics Raycasts & Ground Detection

Vehicle systems use raycasts to:
- **Find ground height**: Place vehicles at correct Y position
- **Calculate exit positions**: Ensure dismount points are walkable
- **Detect obstacles**: Driving uses box casts and overlap checks for the full vehicle body

```csharp
// From Vehicle.TryGetGroundHeight()
RaycastHit[] hits = Physics.RaycastAll(
    position + Vector3.up * 100f,     // Start high above
    Vector3.down,                      // Raycast downward
    200f,                              // 200-unit range
    Physics.DefaultRaycastLayers,      // All layers
    QueryTriggerInteraction.Ignore     // Skip triggers
);
// Skips Vehicle, Player, WorldPickup objects
```

## Network Synchronization

Vehicle positions are synchronized via:
- **NetworkVariable<Vector3> parkedPosition**: Current world position
- **NetworkVariable<float> parkedYaw**: Rotation around Y axis
- **NetworkVariable<bool> poweredOn**: Active hover state
- **NetworkVariable<ulong> riderId**: Currently mounted player

Collision bounds move with the vehicle's networked transform, ensuring all clients see the same obstacle.

## Configuration & Tuning

### In the Inspector (Vehicle Prefab)
```
Hover Rise Speed: 3 (units/sec when powering on)
Hover Drop Speed: 2 (units/sec when powering off)
Hover Height: 1.5 (units above ground when active)
```

### Dimensions by Vehicle Type
| Type | Dimensions (W×H×L) | Cargo | Use Case |
|------|------------|-------|----------|
| Hover Bike | 1×1×4 | 4 | Fast solo transport |
| Hover Car | 1×3×5 | 12-16 | Multi-passenger courier |
| Big Droid | 5×15×5 | 24+ | Heavy platform |

## Troubleshooting

### "Vehicle Clips Through Terrain"
- **Cause**: Ground detection raycast failed
- **Fix**: Check `TryGetGroundHeight()` raycast layer mask; ensure terrain has colliders
- **Debug**: Add `Debug.DrawRay()` in ground-check method

### "Player Can't Mount Vehicle"
- **Cause**: Not within range (5 units), vehicle not powered, or already occupied
- **Status Message**: "Cannot ride: turn the unoccupied vehicle on first."
- **Fix**: Ensure Engine Cube and Power Cube installed; power vehicle on; dismount current rider

### "Vehicle Jitters or Bounces"
- **Cause**: CharacterController pushing vehicle during movement
- **Fix**: Verify Rigidbody is kinematic; CharacterController has proper layer setup
- **Prevention**: Adjust Character Controller radius to avoid tight spaces

## Future Enhancements

1. **Collision Damage**: Vehicles take damage when hitting objects at high speed
2. **Multi-Component Collision**: Individual cube components can be damaged
3. **Vehicle-to-Vehicle Interactions**: Pushing, towing, docking
4. **Environmental Forces**: Wind, gravity wells affecting vehicle trajectory
5. **Damage Visualization**: Dented/charred cube visuals based on impact

## See Also
- [Vehicle System Guide](VEHICLE_SYSTEM_GUIDE.md)
- [Custom Instructions - Vehicles Section](AssistantCustomInstructions.txt)
