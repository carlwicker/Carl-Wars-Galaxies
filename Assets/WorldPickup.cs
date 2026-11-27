using System.Text;
using TMPro;
using UnityEngine;

public sealed class WorldPickup : MonoBehaviour
{
    [SerializeField] private string pickupId = "scrap-crate";
    [SerializeField] private string itemId = "scrap";
    [SerializeField] private string itemName = "Scrap";
    [SerializeField] private int itemCount = 1;
    [SerializeField] private int quality;
    [SerializeField] private int conductivity;
    [SerializeField] private int speed;
    [SerializeField] private int experimentPoints;
    [SerializeField] private float energyUsed;
    [SerializeField] private int potentialEnergy;
    [SerializeField] private int coldResistance;
    [SerializeField] private int decayResistance;
    [SerializeField] private int entangleResistance;

    private TMP_Text nameplate;
    private UnifiedNameplate nameplateComponent;
    private bool hasValidItemData;

    public string PickupId => pickupId;
    public string ItemId => itemId;
    public string ItemName => itemName;
    public int ItemCount => itemCount;
    public int Quality => quality;
    public int Conductivity => conductivity;
    public int Speed => speed;
    public int ExperimentPoints => experimentPoints;
    public float EnergyUsed => energyUsed;
    public int PotentialEnergy => potentialEnergy;
    public int ColdResistance => coldResistance;
    public int DecayResistance => decayResistance;
    public int EntangleResistance => entangleResistance;
    public bool CanBePickedUp => hasValidItemData && isActiveAndEnabled && gameObject.activeInHierarchy;

    private void Awake()
    {
        ValidateItemData();
        EnsureNameplate();
        RefreshNameplate();
    }

    public void Initialize(string newPickupId, string newItemId, string newItemName, int newItemCount,
        int newQuality = 0, int newSpeed = 0, int newExperimentPoints = 0, int newConductivity = 0,
        float newEnergyUsed = 0f, int newPotentialEnergy = 0, int newColdResistance = 0,
        int newDecayResistance = 0, int newEntangleResistance = 0)
    {
        pickupId = newPickupId;
        itemId = newItemId;
        itemName = newItemName;
        itemCount = newItemCount;
        quality = newQuality;
        conductivity = newConductivity;
        speed = newSpeed;
        experimentPoints = newExperimentPoints;
        energyUsed = newEnergyUsed;
        potentialEnergy = newPotentialEnergy;
        coldResistance = newColdResistance;
        decayResistance = newDecayResistance;
        entangleResistance = newEntangleResistance;
        ValidateItemData();
        EnsureNameplate();
        RefreshNameplate();
    }

    public static void SpawnDroppedReplica(
        string pickupId,
        string itemId,
        string itemName,
        int itemCount,
        Vector3 position, int quality, int speed, int experimentPoints, int conductivity = 0, float energyUsed = 0f,
        int potentialEnergy = 0, int coldResistance = 0, int decayResistance = 0, int entangleResistance = 0)
    {
        WorldPickup[] pickups = FindObjectsByType<WorldPickup>(FindObjectsInactive.Include);

        for (int i = 0; i < pickups.Length; i++)
        {
            if (pickups[i].PickupId == pickupId)
            {
                pickups[i].Initialize(pickupId, itemId, itemName, itemCount, quality, speed, experimentPoints,
                    conductivity, energyUsed, potentialEnergy, coldResistance, decayResistance, entangleResistance);
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
        droppedPickup.Initialize(pickupId, itemId, itemName, itemCount, quality, speed, experimentPoints,
            conductivity, energyUsed, potentialEnergy, coldResistance, decayResistance, entangleResistance);
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
            && itemCount > 0 && !float.IsNaN(energyUsed) && !float.IsInfinity(energyUsed)
            && energyUsed >= 0f && energyUsed <= 100f
            && (itemId != "radioactive"
                || IsValidRadioactiveStat(quality) && IsValidRadioactiveStat(potentialEnergy)
                    && IsValidRadioactiveStat(coldResistance) && IsValidRadioactiveStat(decayResistance)
                    && IsValidRadioactiveStat(entangleResistance));

        if (!hasValidItemData)
        {
            Debug.LogError("Pickup IDs and item details must be non-empty and fit in 29 UTF-8 bytes.", this);
        }
    }

    private static bool IsValidRadioactiveStat(int value)
    {
        return value >= 1 && value <= 1000;
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
            string qualityLabel = itemId == "radioactive"
                ? $" OQ{quality}"
                : quality > 0 ? $" Q{quality}" : "";
            nameplate.text = $"{itemName} ({itemCount})" + qualityLabel
                + (itemId == "radioactive"
                    ? $" PE{potentialEnergy} CR{coldResistance} DR{decayResistance} ER{entangleResistance}"
                    : conductivity > 0 ? $" CD{conductivity}" : "");
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
