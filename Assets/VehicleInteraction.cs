using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInteraction), typeof(PlayerInventory))]
public sealed class VehicleInteraction : NetworkBehaviour
{
    private const float InteractionRange = 5f;
    private readonly NetworkVariable<ulong> mountedVehicleId = new NetworkVariable<ulong>(Vehicle.NoRider);
    private PlayerInventory inventory;
    private PlayerInteraction playerInteraction;
    private PlayerChat playerChat;
    private Vehicle selectedVehicle;
    private bool menuOpen;
    private bool inventoryOpen;
    private int selectedPlayerSlot = -1;
    private string status = string.Empty;
    private float statusUntil;
    private Vector2 menuPosition;
    private Vector2 playerScrollPosition;
    private Vector2 cargoScrollPosition;
    private bool visualRaised;

    public bool IsRidingVehicle => mountedVehicleId.Value != Vehicle.NoRider;
    public bool IsVehicleMenuOpen => menuOpen || inventoryOpen;

    public bool TryGetRiddenVehicle(out Vehicle vehicle)
    {
        return TryGetVehicle(mountedVehicleId.Value, out vehicle);
    }

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        playerInteraction = GetComponent<PlayerInteraction>();
        playerChat = GetComponent<PlayerChat>();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && TryGetVehicle(mountedVehicleId.Value, out Vehicle vehicle))
            vehicle.Dismount(playerInteraction);
        if (visualRaised) playerInteraction.SetVehicleRiderHeight(0f);
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (playerChat != null && playerChat.IsChatActive)
        {
            menuOpen = false;
            inventoryOpen = false;
            return;
        }

        if ((menuOpen || inventoryOpen) && (selectedVehicle == null || !selectedVehicle.isActiveAndEnabled
            || !selectedVehicle.IsWithinRange(transform.position, PlayerInteraction.ItemInteractionRange)))
        {
            menuOpen = false;
            inventoryOpen = false;
            selectedPlayerSlot = -1;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.eKey.wasPressedThisFrame && IsRidingVehicle)
            DismountServerRpc();
        else if (keyboard.eKey.wasPressedThisFrame && !playerInteraction.IsCombatInputBlocked
            && TryGetNearbyVehicle(out Vehicle nearbyVehicle))
            MountServerRpc(nearbyVehicle.NetworkObjectId);
        else if (keyboard.tKey.wasPressedThisFrame && TryGetRiddenVehicle(out Vehicle riddenVehicle))
            SetPowerServerRpc(riddenVehicle.NetworkObjectId, !riddenVehicle.IsOn);
        else if (keyboard.iKey.wasPressedThisFrame && TryGetRiddenVehicle(out Vehicle inventoryVehicle))
            ToggleRiddenInventory(inventoryVehicle);
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            menuOpen = false;
            inventoryOpen = false;
            selectedPlayerSlot = -1;
        }
    }

    private void LateUpdate()
    {
        if (!IsSpawned) return;
        if (!IsRidingVehicle && visualRaised)
        {
            playerInteraction.SetVehicleRiderHeight(0f);
            visualRaised = false;
        }
        else if (TryGetVehicle(mountedVehicleId.Value, out Vehicle vehicle))
        {
            playerInteraction.SetVehicleRiderHeight(vehicle.RiderHeight);
            visualRaised = true;
        }
    }

    public void OpenMenu(Vehicle vehicle, Vector2 position)
    {
        if (!IsOwner || vehicle == null || !vehicle.isActiveAndEnabled
            || !vehicle.IsWithinRange(transform.position, PlayerInteraction.ItemInteractionRange)) return;
        selectedVehicle = vehicle;
        menuPosition = position;
        inventoryOpen = false;
        menuOpen = true;
        selectedPlayerSlot = -1;
    }

    private void OnGUI()
    {
        if (!IsOwner || (playerChat != null && playerChat.IsChatActive)) return;
        if (menuOpen && selectedVehicle != null) DrawMenu();
        if (inventoryOpen && selectedVehicle != null) DrawInventory();
        if (IsRidingVehicle && TryGetRiddenVehicle(out Vehicle riddenVehicle))
            GUI.Label(new Rect(12f, Screen.height - 72f, 450f, 28f),
                riddenVehicle.IsOn ? "Riding vehicle: WASD to drive, I for inventory, T for power, E to dismount"
                    : "Vehicle is off: I for inventory, T for power, E to dismount");
        if (Time.unscaledTime < statusUntil)
            GUI.Label(new Rect(12f, Screen.height - 105f, 600f, 28f), status);
    }

    private void DrawMenu()
    {
        Rect panel = new Rect(
            Mathf.Clamp(menuPosition.x, 0f, Mathf.Max(0f, Screen.width - 200f)),
            Mathf.Clamp(menuPosition.y, 0f, Mathf.Max(0f, Screen.height - 160f)),
            200f, 160f);
        GUILayout.BeginArea(panel, GUI.skin.box);
        GUILayout.Label(selectedVehicle.VehicleName);
        if ((!IsRidingVehicle || selectedVehicle.NetworkObjectId == mountedVehicleId.Value)
            && GUILayout.Button(selectedVehicle.IsOn ? "Turn off" : "Turn on"))
        {
            SetPowerServerRpc(selectedVehicle.NetworkObjectId, !selectedVehicle.IsOn);
            menuOpen = false;
        }
        if (IsRidingVehicle)
        {
            if (GUILayout.Button("Exit Transport")) { DismountServerRpc(); menuOpen = false; }
        }
        else
        {
            if (GUILayout.Button("Enter Transport"))
            {
                MountServerRpc(selectedVehicle.NetworkObjectId);
                menuOpen = false;
            }
            if (GUILayout.Button("Inventory"))
            {
                inventoryOpen = true;
                menuOpen = false;
            }
        }
        if (GUILayout.Button("Close")) menuOpen = false;
        GUILayout.EndArea();
    }

    private void ToggleRiddenInventory(Vehicle vehicle)
    {
        bool closeInventory = inventoryOpen && selectedVehicle == vehicle;
        selectedVehicle = vehicle;
        menuOpen = false;
        inventoryOpen = !closeInventory;
        selectedPlayerSlot = -1;
    }

    private void DrawInventory()
    {
        if (!inventory.IsReady || !selectedVehicle.IsReady)
        {
            GUI.Label(new Rect(20f, 120f, 500f, 25f), "Inventory or vehicle is not ready.");
            return;
        }

        const float width = 800f;
        float height = Mathf.Max(400f, Mathf.Min(620f, Screen.height - 40f));
        Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(panel, $"{selectedVehicle.VehicleName} inventory");
        GUI.Label(new Rect(panel.x + 15f, panel.y + 25f, 400f, 25f),
            "Select a player item, then a vehicle slot. Click a vehicle item to take one.");

        const float playerSlotStep = PlayerInteraction.SlotSize + 6f;
        Rect playerViewport = new Rect(panel.x + 15f, panel.y + 60f, 390f, height - 110f);
        Rect playerContent = new Rect(0f, 0f, 4f * playerSlotStep - 6f, 6f * playerSlotStep - 6f);
        playerScrollPosition = GUI.BeginScrollView(playerViewport, playerScrollPosition, playerContent);
        if (selectedPlayerSlot >= inventory.Slots.Count) selectedPlayerSlot = -1;
        for (int i = 0; i < PlayerInventory.MaximumSlots; i++)
        {
            Rect rect = new Rect((i % 4) * playerSlotStep, (i / 4) * playerSlotStep,
                PlayerInteraction.SlotSize, PlayerInteraction.SlotSize);
            if (i >= inventory.Slots.Count) { GUI.Box(rect, "", PlayerInteraction.InventorySlotStyle); continue; }
            InventorySlot item = inventory.GetSlot(i);
            string label = $"{item.ItemName}\nx{item.Count}"
                + (item.ItemId.ToString() == "power_cube"
                    ? $"\n{Mathf.Max(0f, Vehicle.MaximumEnergy - item.EnergyUsed):0}% energy" : "");
            if (GUI.Button(rect, label, PlayerInteraction.InventorySlotStyle)) selectedPlayerSlot = i == selectedPlayerSlot ? -1 : i;
            if (i == selectedPlayerSlot) PlayerInteraction.DrawSelectedSlotBorder(rect);
        }
        GUI.EndScrollView();

        GUI.Label(new Rect(panel.x + 420f, panel.y + 58f, 350f, 24f), "Dedicated slots");
        DrawVehicleSlot(new Rect(panel.x + 420f, panel.y + 84f, 150f, 86f), 0,
            selectedVehicle.HasEngine ? $"Engine Cube\n{selectedVehicle.EngineSpeedMultiplier * 100f:0}% speed" : "Engine (empty)",
            selectedVehicle.HasEngine ? "engine_cube" : null);
        DrawVehicleSlot(new Rect(panel.x + 580f, panel.y + 84f, 150f, 86f), 1,
            selectedVehicle.HasPower ? $"Power Cube\nQ{selectedVehicle.PowerQuality}\n{selectedVehicle.EnergyRemaining:0}% energy" : "Power (empty)",
            selectedVehicle.HasPower ? "power_cube" : null);
        GUI.Label(new Rect(panel.x + 420f, panel.y + 178f, 350f, 24f), "Cargo");
        Rect cargoViewport = new Rect(panel.x + 420f, panel.y + 203f, 350f, Mathf.Max(100f, height - 260f));
        int cargoRows = (selectedVehicle.CargoSlotCount + 2) / 3;
        Rect cargoContent = new Rect(0f, 0f, 318f, Mathf.Max(0f, cargoRows * 92f - 6f));
        cargoScrollPosition = GUI.BeginScrollView(cargoViewport, cargoScrollPosition, cargoContent);
        for (int i = 0; i < selectedVehicle.CargoSlotCount; i++)
        {
            InventorySlot item = i < selectedVehicle.Cargo.Count ? selectedVehicle.Cargo[i] : default;
            bool canInteract = i <= selectedVehicle.Cargo.Count;
            string cargoLabel = i < selectedVehicle.Cargo.Count ? $"{item.ItemName} x{item.Count}" : "Empty";
            if (i < selectedVehicle.Cargo.Count && item.ItemId.ToString() == "power_cube")
                cargoLabel += $"\n{Mathf.Max(0f, Vehicle.MaximumEnergy - item.EnergyUsed):0}% energy";
            DrawVehicleSlot(new Rect((i % 3) * 106f, (i / 3) * 92f, 100f, 86f),
                i + 2, cargoLabel,
                i < selectedVehicle.Cargo.Count ? item.ItemId.ToString() : null, canInteract);
        }
        GUI.EndScrollView();
        if (GUI.Button(new Rect(panel.x + width - 100f, panel.y + height - 35f, 85f, 25f), "Close"))
        {
            inventoryOpen = false;
            selectedPlayerSlot = -1;
        }
    }

    private void DrawVehicleSlot(Rect rect, int slot, string label, string itemId, bool canInteract = true)
    {
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && canInteract;
        bool clicked = GUI.Button(rect, label, PlayerInteraction.InventorySlotStyle);
        GUI.enabled = wasEnabled;
        if (!clicked) return;
        if (selectedPlayerSlot >= 0 && selectedPlayerSlot < inventory.Slots.Count)
        {
            string selectedId = inventory.GetSlot(selectedPlayerSlot).ItemId.ToString();
            TransferServerRpc(selectedVehicle.NetworkObjectId, selectedPlayerSlot, slot, true, new FixedString32Bytes(selectedId));
        }
        else if (itemId != null)
        {
            TransferServerRpc(selectedVehicle.NetworkObjectId, -1, slot, false, new FixedString32Bytes(itemId));
        }
        selectedPlayerSlot = -1;
    }

    [ServerRpc]
    private void TransferServerRpc(ulong id, int playerSlot, int vehicleSlot, bool toVehicle,
        FixedString32Bytes itemId, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (!TryGetVehicle(id, out Vehicle vehicle) || !vehicle.IsWithinRange(transform.position, InteractionRange)
            || !inventory.TransferVehicleItem(vehicle, playerSlot, vehicleSlot, toVehicle, itemId.ToString()))
            ShowStatusClientRpc("Transfer failed. Check the slot, distance and inventory space.", OwnerRpcParams());
    }

    [ServerRpc]
    private void SetPowerServerRpc(ulong id, bool value, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (!TryGetVehicle(id, out Vehicle vehicle)
            || (mountedVehicleId.Value != id && !vehicle.IsWithinRange(transform.position, InteractionRange))
            || !vehicle.TrySetPower(value, NetworkObjectId))
            ShowStatusClientRpc("Cannot change power. Check the cubes, remaining energy, distance, occupancy, and host log.", OwnerRpcParams());
    }

    [ServerRpc]
    private void MountServerRpc(ulong id, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId || IsRidingVehicle) return;
        if (!TryGetVehicle(id, out Vehicle vehicle) || !vehicle.IsWithinRange(transform.position, InteractionRange)
            || !vehicle.TryMount(playerInteraction))
        {
            ShowStatusClientRpc("Cannot enter: vehicle is unavailable, occupied, or out of range.", OwnerRpcParams());
            return;
        }
        Vector3 position = vehicle.GetMountPosition();
        Teleport(position);
        vehicle.RefreshRiderCollision();
        mountedVehicleId.Value = id;
        TeleportClientRpc(position, OwnerRpcParams());
    }

    [ServerRpc]
    private void DismountServerRpc(ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId || !IsRidingVehicle) return;
        if (!TryGetVehicle(mountedVehicleId.Value, out Vehicle vehicle))
        {
            Debug.LogError("Cannot dismount: the mounted vehicle is missing.", this);
            return;
        }
        bool saved = vehicle.Dismount(playerInteraction);
        Vector3 position = vehicle.GetExitPosition();
        Teleport(position);
        mountedVehicleId.Value = Vehicle.NoRider;
        TeleportClientRpc(position, OwnerRpcParams());
        if (!saved) ShowStatusClientRpc("Vehicle position could not be saved; check the host log.", OwnerRpcParams());
    }

    [ClientRpc]
    private void TeleportClientRpc(Vector3 position, ClientRpcParams clientRpcParams = default)
    {
        Teleport(position);
    }

    private void Teleport(Vector3 position)
    {
        CharacterController controller = GetComponent<CharacterController>();
        bool wasEnabled = controller.enabled;
        controller.enabled = false;
        transform.position = position;
        controller.enabled = wasEnabled;
    }

    [ClientRpc]
    private void ShowStatusClientRpc(FixedString128Bytes message, ClientRpcParams clientRpcParams = default)
    {
        status = message.ToString();
        statusUntil = Time.unscaledTime + 4f;
    }

    private ClientRpcParams OwnerRpcParams()
    {
        return new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } } };
    }

    private bool TryGetVehicle(ulong id, out Vehicle vehicle)
    {
        vehicle = null;
        if (id == Vehicle.NoRider || NetworkManager == null
            || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject obj)) return false;
        return obj.TryGetComponent(out vehicle);
    }

    private bool TryGetNearbyVehicle(out Vehicle closestVehicle)
    {
        closestVehicle = null;
        if (NetworkManager == null) return false;

        float closestDistance = float.PositiveInfinity;
        Vector3 playerPosition = transform.position;
        foreach (NetworkObject networkObject in NetworkManager.SpawnManager.SpawnedObjects.Values)
        {
            if (networkObject == null || !networkObject.TryGetComponent(out Vehicle candidate)
                || !candidate.isActiveAndEnabled || !candidate.IsWithinRange(playerPosition, InteractionRange))
                continue;

            float distance = (candidate.transform.position - playerPosition).sqrMagnitude;
            if (distance >= closestDistance) continue;
            closestVehicle = candidate;
            closestDistance = distance;
        }

        return closestVehicle != null;
    }
}
