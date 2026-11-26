# Nameplate Implementation Examples

## Quick Reference

All nameplates in the Carl Wars Galaxies MMO use the `UnifiedNameplate` component for consistent black background and white text styling.

## Example 1: NPC with Nameplate

```csharp
using TMPro;
using UnityEngine;

public class NPC : MonoBehaviour
{
    [SerializeField] private string npcName = "Merchant";
    private TextMeshPro nameplate;
    private UnifiedNameplate nameplateComponent;

    private void Awake()
    {
        CreateNameplate();
    }

    private void CreateNameplate()
    {
        // Create parent object for nameplate
        GameObject nameplateObject = new GameObject("NPC Nameplate");
        nameplateObject.transform.SetParent(transform, false);
        nameplateObject.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        nameplateObject.transform.localScale = Vector3.one * 0.1f;

        // Add TextMeshPro
        nameplate = nameplateObject.AddComponent<TextMeshPro>();
        nameplate.text = npcName;
        nameplate.fontSize = 32;
        nameplate.alignment = TextAlignmentOptions.Center;
        nameplate.rectTransform.sizeDelta = new Vector2(100f, 60f);

        // Look at camera and apply unified nameplate styling
        nameplateObject.AddComponent<LookAtCamera>();
        nameplateComponent = nameplateObject.AddComponent<UnifiedNameplate>();
    }
}
```

## Example 2: Interactive Object with Status Nameplate

```csharp
using TMPro;
using UnityEngine;

public class InteractiveObject : MonoBehaviour
{
    [SerializeField] private string displayName = "Crafting Station";
    [SerializeField] private bool isOperational = true;
    private TextMeshPro statusNameplate;
    private UnifiedNameplate nameplateComponent;

    private void Start()
    {
        CreateStatusNameplate();
    }

    private void CreateStatusNameplate()
    {
        GameObject nameplateObject = new GameObject("Status Nameplate");
        nameplateObject.transform.SetParent(transform, false);
        nameplateObject.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        nameplateObject.transform.localScale = Vector3.one * 0.08f;

        statusNameplate = nameplateObject.AddComponent<TextMeshPro>();
        statusNameplate.alignment = TextAlignmentOptions.Center;
        statusNameplate.rectTransform.sizeDelta = new Vector2(100f, 60f);

        nameplateObject.AddComponent<LookAtCamera>();
        nameplateComponent = nameplateObject.AddComponent<UnifiedNameplate>();
        
        UpdateStatus();
    }

    public void SetOperational(bool operational)
    {
        isOperational = operational;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        string status = isOperational ? "Ready" : "Offline";
        statusNameplate.text = $"{displayName}\n({status})";
        nameplateComponent.Refresh();
    }
}
```

## Example 3: Droid/Pet Nameplate

```csharp
using TMPro;
using UnityEngine;

public class Droid : MonoBehaviour
{
    [SerializeField] private string droidName = "R2-D2";
    [SerializeField] private string droidType = "Astromech";
    private TextMeshPro nameplate;
    private UnifiedNameplate nameplateComponent;

    private void Awake()
    {
        CreateDroidNameplate();
    }

    private void CreateDroidNameplate()
    {
        GameObject nameplateObject = new GameObject("Droid Nameplate");
        nameplateObject.transform.SetParent(transform, false);
        
        // Position above droid head
        nameplateObject.transform.localPosition = new Vector3(0f, 3f, 0f);
        nameplateObject.transform.localScale = Vector3.one * 0.1f;

        nameplate = nameplateObject.AddComponent<TextMeshPro>();
        nameplate.text = $"{droidName}\n<size=70%>{droidType}</size>";
        nameplate.fontSize = 32;
        nameplate.alignment = TextAlignmentOptions.Center;
        nameplate.rectTransform.sizeDelta = new Vector2(100f, 60f);

        nameplateObject.AddComponent<LookAtCamera>();
        nameplateComponent = nameplateObject.AddComponent<UnifiedNameplate>();
    }
}
```

## Example 4: Dynamic Nameplate (Changes Over Time)

```csharp
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class NetworkCharacter : NetworkBehaviour
{
    private string characterName;
    private TextMeshPro nameplate;
    private UnifiedNameplate nameplateComponent;
    private NetworkVariable<int> healthPoints = new NetworkVariable<int>();

    private void Awake()
    {
        CreateNameplate();
    }

    private void CreateNameplate()
    {
        GameObject nameplateObject = new GameObject("Character Nameplate");
        nameplateObject.transform.SetParent(transform, false);
        nameplateObject.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        nameplateObject.transform.localScale = Vector3.one * 0.1f;

        nameplate = nameplateObject.AddComponent<TextMeshPro>();
        nameplate.fontSize = 32;
        nameplate.alignment = TextAlignmentOptions.Center;
        nameplate.rectTransform.sizeDelta = new Vector2(120f, 60f);

        nameplateObject.AddComponent<LookAtCamera>();
        nameplateComponent = nameplateObject.AddComponent<UnifiedNameplate>();
    }

    public override void OnNetworkSpawn()
    {
        characterName = gameObject.name;
        UpdateNameplate();
    }

    private void Update()
    {
        // Update nameplate if health changed
        if (healthPoints.Value <= 0)
        {
            UpdateNameplate();
        }
    }

    private void UpdateNameplate()
    {
        string status = healthPoints.Value <= 0 ? " [Incapacitated]" : "";
        nameplate.text = $"{characterName}{status}";
        nameplateComponent.Refresh();
    }
}
```

## Common Patterns

### Pattern: Set Nameplate Position Above Object
```csharp
nameplateObject.transform.localPosition = new Vector3(0f, dimensions.y + 1.5f, 0f);
```
Adjust `dimensions.y` based on the height of your object.

### Pattern: Set Nameplate Scale for Readability
```csharp
nameplateObject.transform.localScale = Vector3.one * 0.1f;
```
Adjust multiplier based on distance where nameplate is typically viewed.

### Pattern: Refresh After Text Change
```csharp
nameplate.text = newText;
nameplateComponent.Refresh();
```
Always call `Refresh()` after changing text to update background size.

### Pattern: Multi-line Nameplate
```csharp
nameplate.text = $"Character Name\n<size=70%>Subtitle Info</size>";
nameplate.textWrappingMode = TextWrappingModes.Normal;
```
Use TextMeshPro formatting tags for multi-line displays.

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Nameplate not visible | Ensure LookAtCamera component is added; check localScale |
| Text is yellow/wrong color | UnifiedNameplate overrides text color to white in Awake() |
| Background too small/large | Adjust padding values in UnifiedNameplate or call Refresh() after text changes |
| Nameplate rotates incorrectly | Verify LookAtCamera component is on the nameplate object, not parent |
| Multiple backgrounds appearing | Don't manually create background quad; UnifiedNameplate handles this |

## Performance Considerations

- **Nameplate Creation**: Create nameplates in `Awake()` or `OnNetworkSpawn()` for network objects
- **Text Updates**: Only update text when necessary; call `Refresh()` only when text changes
- **Multiple Objects**: The UnifiedNameplate component is lightweight; thousands can exist without issue
- **Camera Distance**: Adjust nameplate scale based on typical viewing distance in your gameplay
