# Unified Nameplate System

## Overview
A centralized nameplate system that provides black backgrounds with white text for players, vehicles, pickups, and chat bubbles. `UnifiedNameplate` uses the same `NameplateBackground` renderer as the original player nameplate.

## What Changed

### New Component: `UnifiedNameplate.cs`
- **Purpose**: Unified nameplate styling for all world objects (players, vehicles, pickups, NPCs, etc.)
- **Features**:
  - Automatic black background rendering
  - White text color (enforced at Awake)
  - Dynamic background sizing based on text content
  - Consistent padding around text (0.5 horizontal, 0.3 vertical)
  - Background resizing when `Refresh()` is called after text changes

### Updated Components
1. **PlayerIdentity.cs** - Player names use `UnifiedNameplate` and update when the networked name changes

2. **Vehicle.cs** - Vehicle nameplates use `UnifiedNameplate`
   - Text color set to white
   - Consistent black background styling

3. **PlayerChat.cs** - Chat bubbles use `UnifiedNameplate`
   - Chat text displays as white on black background
   - Consistent appearance with other world objects

4. **WorldPickup.cs** - Pickup item nameplates use `UnifiedNameplate`
   - Item names display with consistent white-on-black styling
   - Item counts included in display

## Usage

### For Existing Objects
No changes required. Simply replace `NameplateBackground` references with `UnifiedNameplate` in your code:

**Before:**
```csharp
private NameplateBackground nameplate;
nameplate.gameObject.AddComponent<NameplateBackground>();
```

**After:**
```csharp
private UnifiedNameplate nameplate;
nameplate.gameObject.AddComponent<UnifiedNameplate>();
```

### For New World Objects
When adding nameplates to new object types:

```csharp
// Create nameplate GameObject
GameObject nameplateObject = new GameObject("Object Nameplate");
nameplateObject.transform.SetParent(visuals, false);

// Add TextMeshPro component
TextMeshPro text = nameplateObject.AddComponent<TextMeshPro>();
text.text = "Display Name";
text.fontSize = 32;
text.alignment = TextAlignmentOptions.Center;

// Add these components in order
nameplateObject.AddComponent<LookAtCamera>();
nameplateObject.AddComponent<UnifiedNameplate>();
```

The `UnifiedNameplate` component will:
- Automatically set text to white color
- Create and manage the black background quad
- Handle background sizing and positioning

### Refreshing Nameplate After Text Changes
If you dynamically change the nameplate text, call `Refresh()` to update the background:

```csharp
nameplateText.text = "New Name";
unifiedNameplate.Refresh();
```

## Styling Details
- **Text Color**: White (Color.white)
- **Background Color**: Black (Color.black)
- **Shader**: "Sprites/Default"
- **Background Padding**: 0.5 horizontal, 0.3 vertical units
- **Render Queue**: 3000 (above most other objects)
- **Lighting**: No shadow casting, no shadow receiving

## Migration Notes
`NameplateBackground` is required by `UnifiedNameplate` and creates the background. Do not remove it.

## Benefits
✓ **Consistency**: All world objects have the same nameplate appearance  
✓ **Maintainability**: Single source of truth for nameplate styling  
✓ **Scalability**: Easy to add nameplates to new object types  
✓ **Performance**: Reusable component avoids code duplication  
✓ **Quality**: Black background ensures readability in all lighting conditions
