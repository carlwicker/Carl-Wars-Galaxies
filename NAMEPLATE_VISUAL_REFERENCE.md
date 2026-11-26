# Unified Nameplate Visual Reference

## Nameplate Components

### Before (Inconsistent)
```
Vehicle Nameplate: [Default colors - no background]
  "Hover Bike" (gray text, no background)

Chat Bubble: [Yellow text with black background]
  "[Player says something]" (yellow text)

Pickup: [White text with black background]
  "Scrap (5)" (white text)
```

### After (Unified)
```
Vehicle Nameplate: [WHITE TEXT ON BLACK BACKGROUND]
  ┌─────────────────┐
  │   Hover Bike    │  ← White text
  └─────────────────┘  ← Black background
  
Chat Bubble: [WHITE TEXT ON BLACK BACKGROUND]
  ┌──────────────────────────────────┐
  │ [Player says something here]     │  ← White text
  └──────────────────────────────────┘  ← Black background
  
Pickup: [WHITE TEXT ON BLACK BACKGROUND]
  ┌────────────────┐
  │  Scrap (5)     │  ← White text
  └────────────────┘  ← Black background
```

## Color Specifications

| Component | Color | RGB | Hex |
|-----------|-------|-----|-----|
| Text | White | (255, 255, 255) | #FFFFFF |
| Background | Black | (0, 0, 0) | #000000 |

## Component Hierarchy

### For Any World Object with Nameplate

```
World Object
├── [Visual Components]
├── [Physics/Network Components]
└── Nameplate Container
    ├── TextMeshPro (TMP_Text)
    │   └── text = "Display Name"
    │   └── color = Color.white
    │   └── fontSize = 32
    │   └── alignment = Center
    ├── LookAtCamera (makes it face camera)
    └── UnifiedNameplate (styling component)
        └── Automatically creates black background quad
        └── Maintains padding and sizing
        └── Provides Refresh() method
```

## Rendering Order

```
World Objects (depth ordering)
    ↓
Player/Vehicle/NPC Geometry
    ↓
Nameplate TextMeshPro (render queue ~2500)
    ↓
Nameplate Background Quad (render queue ~3000)  ← Always on top
    ↓
UI/Camera Effects
```

## Responsive Sizing Example

The UnifiedNameplate automatically adjusts background size to match text:

```
Text Length → Background Size:
"A"       → ┌───────┐
            │ A     │
            └───────┘

"Vehicle" → ┌──────────────┐
            │ Vehicle      │
            └──────────────┘

"Hover Bike" → ┌─────────────────────┐
               │ Hover Bike          │
               └─────────────────────┘
```

## Usage Pattern

### Setup
```csharp
// 1. Create TextMeshPro
TextMeshPro text = gameObject.AddComponent<TextMeshPro>();
text.text = "Name";

// 2. Add camera-facing component
gameObject.AddComponent<LookAtCamera>();

// 3. Add unified nameplate styling
gameObject.AddComponent<UnifiedNameplate>();
// → Automatically sets text to white
// → Creates black background quad
// → Sizes background to fit text
```

### Runtime
```csharp
// Update text
text.text = "New Name";
unifiedNameplate.Refresh();  // Resize background
```

## Lighting Independence

The black background ensures consistent readability:

```
Light Scenario 1: Bright Daylight
  ░░░░░░░░░░░░░░░░░░░░░
  ░ Scene with sun ░░░░░░
  ░░░░░░░░░░░░░░░░░░░░░
  
  ┌────────────┐
  │ White Text │  ✓ Readable
  └────────────┘  ✓ High contrast
  (Black background)

Light Scenario 2: Dark Cave
  ░░░░░░░░░░░░░░░░░░░░░
  ░ Dark area  ░░░░░░░░
  ░░░░░░░░░░░░░░░░░░░░░
  
  ┌────────────┐
  │ White Text │  ✓ Readable
  └────────────┘  ✓ Stands out
  (Black background)

Light Scenario 3: Neon/Low Light
  ░░░░░░░░░░░░░░░░░░░░░
  ░ Neon glow  ░░░░░░░░
  ░░░░░░░░░░░░░░░░░░░░░
  
  ┌────────────┐
  │ White Text │  ✓ Readable
  └────────────┘  ✓ Consistent
  (Black background)
```

## Performance Profile

- **Draw Calls**: 2 per nameplate (TextMeshPro + Background Quad)
- **Memory**: Minimal (~1KB per nameplate component)
- **Updates**: Only when text changes (call Refresh())
- **Render Queue**: 3000 (above most objects, below UI)

## Common Configurations

### Small Nameplate (NPCs, Small Items)
```csharp
nameplate.fontSize = 24;
nameplateObject.transform.localScale = Vector3.one * 0.08f;
nameplateObject.transform.localPosition = new Vector3(0f, 1.5f, 0f);
```

### Medium Nameplate (Players, Regular Items)
```csharp
nameplate.fontSize = 32;
nameplateObject.transform.localScale = Vector3.one * 0.1f;
nameplateObject.transform.localPosition = new Vector3(0f, 2.5f, 0f);
```

### Large Nameplate (Vehicles, Large Structures)
```csharp
nameplate.fontSize = 36;
nameplateObject.transform.localScale = Vector3.one * 0.12f;
nameplateObject.transform.localPosition = new Vector3(0f, 3.5f, 0f);
```

## Accessibility

✓ **High Contrast**: White on black (WCAG AA compliant)
✓ **Readable Font**: TextMeshPro default font is clear
✓ **Consistent Size**: Recommended 24-36pt for visibility
✓ **No Flashing**: Static background, no animation artifacts
✓ **Clear Hierarchy**: Background clearly separates text from world
