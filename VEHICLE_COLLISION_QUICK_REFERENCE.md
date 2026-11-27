# Vehicle Collision System - Quick Reference

## What Changed?

### ❌ **OLD BEHAVIOR** (INCORRECT)
```
Vehicle: Off-World         Vehicle: Mounted/Occupied
└─ BoxCollider             └─ BoxCollider (TRIGGER MODE)
   isTrigger = false          isTrigger = true  ← ALLOWS PASS-THROUGH!
   BLOCKS everything          Players can walk through it
```

### ✅ **NEW BEHAVIOR** (CORRECT)
```
Vehicle: Off-World         Vehicle: Mounted/Occupied
└─ BoxCollider             └─ BoxCollider
   isTrigger = false          isTrigger = false  ← ALWAYS SOLID
   BLOCKS everything          Players cannot walk through it
└─ Rigidbody               └─ Rigidbody
   isKinematic = true         isKinematic = true
   useGravity = false         useGravity = false
```

## Key Points

| Aspect | Before | After |
|--------|--------|-------|
| **Collider Mode** | Toggled (trigger when occupied) | Always solid |
| **Rigidbody** | None | Kinematic (non-physical) |
| **Can Walk Through** | Yes, if occupied | Never |
| **Mount/Dismount** | Used trigger to allow pass-through | Works naturally with solid collider |
| **Obstacle Behavior** | Inconsistent | Reliable |

## Code Diff Summary

```csharp
// 1. Added Rigidbody field
private Rigidbody vehicleRigidbody;

// 2. Initialize in Awake()
vehicleRigidbody = geometryObject.AddComponent<Rigidbody>();
vehicleRigidbody.isKinematic = true;
vehicleRigidbody.useGravity = false;

// 3. Remove from TryMount()
- bodyCollider.isTrigger = true;  // ← DELETED

// 4. Remove from LateUpdate()
- bodyCollider.isTrigger = IsOccupied;  // ← DELETED
```

## Why This Matters

**Vehicles are now real obstacles** that you must navigate around, not pass through. This:
- Makes the game world feel more solid and realistic
- Requires careful positioning when parking
- Creates actual strategic value to vehicle placement
- Enables future collision-damage and push mechanics
- Maintains consistency with other world objects

## Testing It

1. **Spawn a vehicle** in a narrow space
2. **Try to walk through it** → Should be blocked ✅
3. **Mount the vehicle** → Should work normally ✅
4. **Drive it into terrain** → Should stop at collision ✅
5. **Multiple vehicles** → Can't occupy same space ✅

## Common Questions

**Q: Why not keep it as a trigger?**
A: Triggers are for detecting overlaps, not blocking movement. Vehicles should be physical obstacles, like buildings or terrain.

**Q: What about clipping through the floor?**
A: Kinematic Rigidbody + solid collider prevents this. Ground detection raycast places vehicles correctly.

**Q: Does mounting still work?**
A: Yes! Mounting teleports the player to the vehicle's position, so collision doesn't prevent it.

**Q: Can vehicles push through walls?**
A: No. Vehicles respect terrain and structure collisions. They're true kinematic obstacles.

## Files Changed
- ✏️ `Assets/Vehicle.cs` - Core collision implementation
- 📄 `VEHICLE_COLLISION_SYSTEM.md` - Detailed documentation
- 📄 `VEHICLE_COLLISION_CHANGES.md` - Change summary

## Related Systems

- **PlayerMovement.cs** - Uses CharacterController; respects vehicle collision
- **VehicleInteraction.cs** - Handles mounting; works with solid collider
- **WorldPickup.cs** - Skipped in vehicle collision raycasts
- **UnevenGround.cs** - Terrain collider system vehicles interact with
