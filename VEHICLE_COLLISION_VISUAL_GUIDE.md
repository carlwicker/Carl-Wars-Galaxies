# Vehicle Collision System - Visual Guide

## Component Hierarchy

### Vehicle Structure
```
Vehicle (NetworkObject)
├─ Vehicle Geometry (GameObject with BoxCollider + Rigidbody)
│  ├─ Vehicle Visuals (Group of Cubes)
│  │  ├─ Vehicle Cube 0,0,0 (Renderer only)
│  │  ├─ Vehicle Cube 0,0,1 (Renderer only)
│  │  ├─ Vehicle Cube ... (Renderer only)
│  │  └─ Vehicle Nameplate (TextMeshPro)
│  │     └─ LookAtCamera (Billboard)
│  └─ Hover Glow (Light) [Added on NetworkSpawn]
└─ [Other NetworkObject components]
```

### Component Responsibilities

```
BoxCollider
  ├─ Shape: Scaled to vehicle dimensions (W×H×L)
  ├─ Center: (0, H/2, 0) - Offset up by half height
  ├─ Size: (dimensions.x, dimensions.y, dimensions.z)
  ├─ isTrigger: FALSE (always solid!)
  └─ Purpose: Collision boundary for physics

Rigidbody
  ├─ Body Type: Kinematic
  ├─ isKinematic: TRUE (not affected by physics engine)
  ├─ useGravity: FALSE (hover system handles height)
  ├─ Velocity: Never set (position set directly)
  └─ Purpose: Collision callbacks and physics queries
```

## Collision Interaction Flow

### Scenario 1: Player Walking Around Vehicle

```
Player Position Update
       ↓
PlayerMovement.HandleMovement()
       ↓
CharacterController.Move()
       ↓
Check collision against world
       ↓
Vehicle.BoxCollider detected! ← BLOCKED
       ↓
Player stops/slides along vehicle surface
       ↓
Player cannot pass through
```

### Scenario 2: Player Mounting Vehicle

```
Player clicks "Ride" on vehicle menu
       ↓
MountServerRpc() called
       ↓
Server: Vehicle.TryMount(player)
       ├─ Check: IsReady? IsOn? Not occupied?
       ├─ Set: riderId = player.NetworkObjectId
       └─ Return: true ✓
       ↓
Player teleported to mount position
       ↓
In LateUpdate():
  ├─ Vehicle follows rider position
  ├─ Collider moves with vehicle
  └─ Both are solid obstacles
```

### Scenario 3: Vehicle Collision with Terrain

```
Vehicle powered on (hover active)
       ↓
Hover height = 1.5 units above ground
       ↓
Terrain raycast finds ground at Y=100
       ↓
Vehicle placed at Y=101.5 (100 + 1.5)
       ↓
BoxCollider extends down to Y=101
       ↓
Terrain collider at Y=100
       ↓
Result: Vehicle hovers above ground, slight gap prevents clipping
```

### Scenario 4: Multiple Vehicles Nearby

```
Vehicle A Position       Vehicle B Position
    at (0,0,0)              at (3,0,0)
   
   W=1×1×4 (bike)        W=1×3×5 (car)
   
   BoxCollider A         BoxCollider B
   ├─ X: -0.5 to 0.5     ├─ X: 2.5 to 3.5
   ├─ Y: 0 to 1          ├─ Y: 0 to 3
   └─ Z: -2 to 2         └─ Z: -2.5 to 2.5
   
   NO OVERLAP ✓
   Both solid, side by side
   
   If B moves left to (1.5, 0, 0):
   
   BoxCollider A         BoxCollider B
   ├─ X: -0.5 to 0.5     ├─ X: 1 to 2
   ├─ Y: 0 to 1          ├─ Y: 0 to 3
   └─ Z: -2 to 2         └─ Z: -2.5 to 2.5
   
   OVERLAP in X-axis (collision!)
   Vehicle B cannot move here
```

## Movement Synchronization

### Local (Owner) Update Loop

```
Frame Update (Player is owner/riding)
     ↓
CharacterController.Move() [PlayerMovement]
     ↓
Player moves, rotation changes
     ↓
LateUpdate() [Vehicle]
     ↓
vehicle.transform.position = rider.position - footOffset
vehicle.transform.rotation = rider.rotation (yaw only)
     ↓
BoxCollider + Rigidbody move with transform
     ↓
All clients see vehicle at correct position
     ↓
Collision on other clients works from networked position
```

### Server Persistence

```
Every 1 second (or on significant change)
     ↓
Check if position/rotation changed > threshold
     ↓
Create SavedVehicle with current position/rotation/state
     ↓
Write to HostInventoryStore (JSON file)
     ↓
On next load, vehicle spawns at saved position
```

## Collision Matrix

| Object A | Object B | Collides? | Notes |
|----------|----------|-----------|-------|
| Vehicle | Terrain | ✅ Yes | Vehicle hovers above |
| Vehicle | Vehicle | ✅ Yes | Cannot overlap |
| Vehicle | Player (riding) | ✅ Yes | But player teleported to vehicle |
| Vehicle | Player (walking) | ✅ Yes | Player blocked/bounces |
| Vehicle | Pickup | ✅ Yes | Pickup not under vehicle |
| Vehicle | Trigger Area | ❌ No | Triggers ignored for vehicle collision |
| Rider (player) | Vehicle | ⚠️ Special | Rider height adjusted visually |

## Raycasting Behavior

### Ground Detection Raycast

```
Start: vehicle.position + (0, 100, 0)
       ↓
       Direction: (0, -1, 0) downward
       Range: 200 units
       ↓
Hits found in order:
  1. Sky = no hits
  2. 20 units: Vehicle above? SKIP ← Vehicle.cs line 390
  3. 40 units: Player controller? SKIP ← Ignore players
  4. 50 units: Pickup item? SKIP ← Ignore pickups
  5. 70 units: Terrain! ← ACCEPT
       ↓
Ground height = 70 units
       ↓
Vehicle final Y = 70 + 1.5 (hover height)
       ↓
Vehicle placed at (X, 71.5, Z)
```

### Interaction Raycast

```
Player clicks on 3D world
       ↓
Main camera raycasts forward
       ↓
Hits in order:
  1. Vehicle at 2 units? YES
       ↓
       Vehicle.IsWithinRange(player.position, range=5)
       ├─ bodyCollider.ClosestPoint(player.position)
       ├─ Distance check
       └─ Return true ✓
       ↓
Menu opens!
  Ride / Turn On / Inventory / Close
```

## Performance Implications

**Kinematic Rigidbody Benefits:**
- Faster than dynamic physics (no solver needed)
- Reliable collision detection
- Network-friendly (position set, not velocity)
- No unexpected accelerations/rotations

**BoxCollider Advantages:**
- Simple shape = fast collision tests
- Scales easily with vehicle dimensions
- Works well for cube-based vehicles
- Future: Can expand to compound collider for detailed hit zones

**Hover State Efficiency:**
- Collider always active
- BoxCollider intersection tests run continuously
- CharacterController already handles collision physics
- Minimal additional overhead vs. old trigger system

---

## State Diagram

```
                    ┌─────────────────┐
                    │  Vehicle Idle   │
                    │ (Off-World)     │
                    │ parkedPosition  │
                    │ isTrigger=false │
                    └────────┬────────┘
                             │ Player calls TrySetPower(true)
                             │ + Has Engine Cube
                             │ + Has Power Cube
                             ↓
                    ┌─────────────────┐
                    │  Vehicle On     │
                    │ (Unoccupied)    │
                    │ poweredOn=true  │
                    │ hover active    │
                    │ isTrigger=false │◄─────┐
                    └────────┬────────┘       │
                             │                │
         Player E (dismount) │                │ Player clicks
                             │                │ Ride button
                             ↓                │
                    ┌─────────────────┐       │
                    │  Vehicle On     │───────┘
                    │ (Occupied)      │ Player E
                    │ riderId = player│ dismount
                    │ isTrigger=false │
                    │ hover active    │
                    │ Player follows  │
                    │ vehicle position│
                    └─────────────────┘
```

