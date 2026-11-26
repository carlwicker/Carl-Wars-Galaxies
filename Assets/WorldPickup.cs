using System.Text;
using TMPro;
using UnityEngine;

public sealed class WorldPickup : MonoBehaviour
{
    [SerializeField] private string pickupId = "scrap-crate";
    [SerializeField] private string itemId = "scrap";
    [SerializeField] private string itemName = "Scrap";
    [SerializeField] private int itemCount = 1;

    private TMP_Text nameplate;
    private UnifiedNameplate nameplateComponent;
    private bool hasValidItemData;

    public string PickupId => pickupId;
    public string ItemId => itemId;
    public string ItemName => itemName;
    public int ItemCount => itemCount;
    public bool CanBePickedUp => hasValidItemData && isActiveAndEnabled && gameObject.activeInHierarchy;

    private void Awake()
    {
        ValidateItemData();
        EnsureNameplate();
        RefreshNameplate();
    }

    public void Initialize(string newPickupId, string newItemId, string newItemName, int newItemCount)
    {
        pickupId = newPickupId;
        itemId = newItemId;
        itemName = newItemName;
        itemCount = newItemCount;
        ValidateItemData();
        EnsureNameplate();
        RefreshNameplate();
    }

    public static void SpawnDroppedReplica(
        string pickupId,
        string itemId,
        string itemName,
        int itemCount,
        Vector3 position)
    {
        WorldPickup[] pickups = FindObjectsByType<WorldPickup>(FindObjectsInactive.Include);

        for (int i = 0; i < pickups.Length; i++)
        {
            if (pickups[i].PickupId == pickupId)
            {
                pickups[i].Initialize(pickupId, itemId, itemName, itemCount);
                pickups[i].transform.position = position;
                pickups[i].gameObject.SetActive(true);
                return;
            }
        }

        if (pickups.Length == 0)
        {
            Debug.LogError($"Cannot spawn dropped pickup {pickupId}: no WorldPickup scene object is available as a template.");
            return;
        }

        WorldPickup droppedPickup = Instantiate(pickups[0].gameObject, position, Quaternion.identity)
            .GetComponent<WorldPickup>();
        droppedPickup.Initialize(pickupId, itemId, itemName, itemCount);
        droppedPickup.transform.localScale = Vector3.one;
        droppedPickup.gameObject.name = $"Dropped {itemName}";
        droppedPickup.gameObject.SetActive(true);
    }

    private void ValidateItemData()
    {
        hasValidItemData = !string.IsNullOrWhiteSpace(pickupId)
            && Encoding.UTF8.GetByteCount(pickupId) <= 29
            && !string.IsNullOrWhiteSpace(itemId)
            && Encoding.UTF8.GetByteCount(itemId) <= 29
            && !string.IsNullOrWhiteSpace(itemName)
            && Encoding.UTF8.GetByteCount(itemName) <= 29
            && itemCount > 0;

        if (!hasValidItemData)
        {
            Debug.LogError("Pickup IDs and item details must be non-empty and fit in 29 UTF-8 bytes.", this);
        }
    }

    private void EnsureNameplate()
    {
        if (nameplate != null) return;

        Transform existingNameplate = transform.Find("Pickup Nameplate");
        if (existingNameplate != null)
        {
            nameplate = existingNameplate.GetComponent<TMP_Text>();
        }

        if (nameplate == null)
        {
            GameObject nameplateObject = new GameObject("Pickup Nameplate", typeof(TextMeshPro));
            nameplateObject.transform.SetParent(transform, false);
            nameplateObject.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            nameplateObject.transform.localScale = Vector3.one * 0.0625f;
            nameplate = nameplateObject.GetComponent<TMP_Text>();
            nameplate.font = TMP_Settings.defaultFontAsset;
            nameplate.fontSize = 32f;
            nameplate.alignment = TextAlignmentOptions.Center;
            nameplate.color = Color.white;
            nameplate.rectTransform.sizeDelta = new Vector2(12f, 2.5f);
            nameplateObject.AddComponent<LookAtCamera>();
        }
    }

    private void RefreshNameplate()
    {
        if (nameplate != null)
        {
            nameplate.text = $"{itemName} ({itemCount})";
            if (nameplateComponent == null)
            {
                nameplateComponent = nameplate.GetComponent<UnifiedNameplate>();
                if (nameplateComponent == null)
                {
                    nameplateComponent = nameplate.gameObject.AddComponent<UnifiedNameplate>();
                }
            }

            nameplateComponent.Refresh();
        }
    }

    public void SetCollected()
    {
        gameObject.SetActive(false);
    }
}
