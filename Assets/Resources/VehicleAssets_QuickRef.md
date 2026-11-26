# Vehicle Asset Quick Reference

## Included Vehicle Archetypes

### 🏍️ Hover Bike
- **ID:** `hover_bike`
- **Size:** 1×1×4 cubes
- **Cargo Slots:** 4
- **Role:** Personal reconnaissance, fast courier
- **Cost:** 50,000 cr
- **Speed Rating:** ⭐⭐⭐⭐⭐
- **Capacity Rating:** ⭐

```
[◾]  ← 1 cube wide
█████ ← 4 cubes long
```

---

### 🚙 Hover Car
- **ID:** `hover_car`
- **Size:** 1×3×5 cubes
- **Cargo Slots:** 14
- **Role:** Multi-passenger, utility transport
- **Cost:** 120,000 cr
- **Speed Rating:** ⭐⭐⭐
- **Capacity Rating:** ⭐⭐⭐

```
[◾◾◾]  ← 3 cubes tall
██████ ← 5 cubes long
```

---

### 🤖 Heavy Droid
- **ID:** `big_droid`
- **Size:** 5×15×5 cubes
- **Cargo Slots:** 32
- **Role:** Heavy platform, mobile crafting station, combat support
- **Cost:** 450,000 cr
- **Speed Rating:** ⭐
- **Capacity Rating:** ⭐⭐⭐⭐⭐

```
█████  ← 5 wide, 15 tall, 5 long
█████
█████
(tower)
```

---

### 🌟 Scout Speeder
- **ID:** `scout_speeder`
- **Size:** 1×2×3 cubes
- **Cargo Slots:** 6
- **Role:** Exploration, reconnaissance
- **Cost:** 85,000 cr
- **Speed Rating:** ⭐⭐⭐⭐
- **Capacity Rating:** ⭐⭐

```
[◾◾]  ← 2 cubes tall
███   ← 3 cubes long
```

---

### 📦 Cargo Hauler
- **ID:** `cargo_hauler`
- **Size:** 2×2×6 cubes
- **Cargo Slots:** 24
- **Role:** Heavy transport, resource hauling
- **Cost:** 200,000 cr
- **Speed Rating:** ⭐⭐
- **Capacity Rating:** ⭐⭐⭐⭐

```
[◾◾]  ← 2 cubes wide, 2 cubes tall
██████ ← 6 cubes long
```

---

## Component Requirements

All vehicles require:
- **1× Engine Cube** – Provides acceleration/speed stats
- **1× Power Cube** – Provides energy and drain efficiency

**A vehicle cannot power ON without both cubes installed.**

## Color Codes (RGB 0.0–1.0)

| Vehicle | Color | Hex |
|---------|-------|-----|
| Hover Bike | (0.08, 0.28, 0.38) | #142C61 |
| Hover Car | (0.12, 0.35, 0.42) | #1F596B |
| Heavy Droid | (0.25, 0.25, 0.28) | #404047 |
| Scout Speeder | (0.1, 0.5, 0.3) | #198047 |
| Cargo Hauler | (0.4, 0.3, 0.1) | #664C1A |

## Extending the System

### Quick Template

Copy and modify this template to add a new vehicle:

```json
{
  "id": "new_vehicle_id",
  "name": "New Vehicle Name",
  "description": "Brief description of role",
  "dimensions": {
    "width": 1,
    "height": 1,
    "length": 4
  },
  "cargoSlots": 4,
  "color": {
    "r": 0.5,
    "g": 0.5,
    "b": 0.5,
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
  "tags": ["category"],
  "cost": 100000
}
```

### Design Tips

- **Small & Light** (1×1×3 or smaller): Higher hover speeds, low cargo
- **Medium** (1×3×5 or similar): Balance of speed and capacity
- **Large & Heavy** (5×15×5 or bigger): Low speed, massive cargo
- **Hover Speed**: 2.0–5.0 is typical; below 1.5 feels sluggish
- **Cargo Slots**: Generally ~4 per 10 cubes of volume

---

**Use `VehicleAssetLoader.PrintAllAssets()` in the console to see all loaded vehicles.**
