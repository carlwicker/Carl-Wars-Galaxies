using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

[Serializable]
public struct InventorySlot : INetworkSerializable, IEquatable<InventorySlot>
{
    public FixedString32Bytes ItemId;
    public FixedString32Bytes ItemName;
    public int Count;

    public InventorySlot(string itemId, string itemName, int count)
    {
        ItemId = new FixedString32Bytes(itemId);
        ItemName = new FixedString32Bytes(itemName);
        Count = count;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ItemId);
        serializer.SerializeValue(ref ItemName);
        serializer.SerializeValue(ref Count);
    }

    public bool Equals(InventorySlot other)
    {
        return ItemId.Equals(other.ItemId)
            && ItemName.Equals(other.ItemName)
            && Count == other.Count;
    }

    public override bool Equals(object obj)
    {
        return obj is InventorySlot other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((ItemId.GetHashCode() * 397) ^ ItemName.GetHashCode()) * 397 ^ Count;
        }
    }
}

public sealed class PlayerInventory : NetworkBehaviour
{
    public const int MaximumSlots = 24;
    public const int MaximumStackSize = 99;
    private const float PickupRange = 5f;

    private readonly NetworkList<InventorySlot> slots = new NetworkList<InventorySlot>(
        null,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<bool> inventoryReady = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly HashSet<string> collectedPickupIds = new HashSet<string>();

    private string playerId;
    private string statusMessage = string.Empty;
    private float statusExpiresAt;

    public NetworkList<InventorySlot> Slots => slots;
    public bool IsReady => inventoryReady.Value;
    public string StatusMessage => Time.unscaledTime < statusExpiresAt ? statusMessage : string.Empty;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            SetLocalStatus("Sign in before loading your inventory.");
            Debug.LogError("Cannot load inventory because the player is not signed in.", this);
            return;
        }

        string authenticatedPlayerId = AuthenticationService.Instance.PlayerId;
        if (string.IsNullOrWhiteSpace(authenticatedPlayerId)
            || System.Text.Encoding.UTF8.GetByteCount(authenticatedPlayerId) > 61)
        {
            SetLocalStatus("Could not identify this player for saving.");
            Debug.LogError("Authentication returned an invalid player ID for inventory saving.", this);
            return;
        }

        RegisterPlayerServerRpc(new FixedString64Bytes(authenticatedPlayerId));
    }

    public override void OnDestroy()
    {
        slots.Dispose();
        base.OnDestroy();
    }

    public InventorySlot GetSlot(int index)
    {
        return slots[index];
    }

    public void RequestPickup(string pickupId)
    {
        if (IsOwner && IsSpawned && IsReady)
        {
            RequestPickupServerRpc(new FixedString32Bytes(pickupId));
        }
    }

    public void RequestUse(int slotIndex)
    {
        if (IsOwner && IsSpawned && IsReady)
        {
            RequestUseServerRpc(slotIndex);
        }
    }

    public void RequestDrop(int slotIndex)
    {
        if (IsOwner && IsSpawned && IsReady)
        {
            RequestDropServerRpc(slotIndex);
        }
    }

    public void RequestSplit(int slotIndex)
    {
        if (IsOwner && IsSpawned && IsReady)
        {
            RequestSplitServerRpc(slotIndex);
        }
    }

    public void RequestCombine(int slotIndex)
    {
        if (IsOwner && IsSpawned && IsReady)
        {
            RequestCombineServerRpc(slotIndex);
        }
    }

    public bool TransferVehicleItem(Vehicle vehicle, int playerSlot, int vehicleSlot, bool toVehicle, string expectedItemId)
    {
        if (!IsServer || !IsReady || vehicle == null || !vehicle.IsReady || vehicle.IsOccupied
            || vehicleSlot < 0 || vehicleSlot >= vehicle.CargoSlotCount + 2)
            return false;

        List<HostInventoryStore.SavedItem> items = CreateSavedItems();
        HostInventoryStore.SavedVehicle state = vehicle.CreateSave();
        if (toVehicle)
        {
            if (playerSlot < 0 || playerSlot >= items.Count || items[playerSlot].itemId != expectedItemId)
                return false;
            HostInventoryStore.SavedItem source = items[playerSlot];
            if (vehicleSlot == 0)
            {
                if (state.engineInstalled || source.itemId != "engine_cube") return false;
                state.engineInstalled = true;
            }
            else if (vehicleSlot == 1)
            {
                if (state.powerInstalled || source.itemId != "power_cube") return false;
                state.powerInstalled = true;
            }
            else
            {
                int index = vehicleSlot - 2;
                if (index < state.cargo.Count)
                {
                    HostInventoryStore.SavedItem target = state.cargo[index];
                    if (target.itemId != source.itemId || target.count >= MaximumStackSize) return false;
                    target.count++;
                }
                else if (index == state.cargo.Count)
                {
                    state.cargo.Add(new HostInventoryStore.SavedItem
                    {
                        itemId = source.itemId,
                        itemName = source.itemName,
                        count = 1
                    });
                }
                else return false;
            }
            if (--source.count == 0) items.RemoveAt(playerSlot);
        }
        else
        {
            string itemId;
            string itemName;
            if (vehicleSlot == 0)
            {
                if (!state.engineInstalled) return false;
                itemId = "engine_cube";
                itemName = "Engine Cube";
            }
            else if (vehicleSlot == 1)
            {
                if (!state.powerInstalled) return false;
                itemId = "power_cube";
                itemName = "Power Cube";
            }
            else
            {
                int index = vehicleSlot - 2;
                if (index >= state.cargo.Count) return false;
                itemId = state.cargo[index].itemId;
                itemName = state.cargo[index].itemName;
            }
            if (itemId != expectedItemId || !TryAddItem(items, itemId, itemName, 1)) return false;
            if (vehicleSlot == 0)
            {
                state.engineInstalled = false;
                state.isOn = false;
            }
            else if (vehicleSlot == 1)
            {
                state.powerInstalled = false;
                state.isOn = false;
            }
            else
            {
                int index = vehicleSlot - 2;
                if (--state.cargo[index].count == 0) state.cargo.RemoveAt(index);
            }
        }

        try
        {
            HostInventoryStore.SaveInventory(playerId, items, vehicle: state);
        }
        catch (IOException exception) { ReportSaveError(exception.Message); return false; }
        catch (UnauthorizedAccessException exception) { ReportSaveError(exception.Message); return false; }
        catch (InvalidDataException exception) { ReportSaveError(exception.Message); return false; }
        catch (ArgumentException exception) { ReportSaveError(exception.Message); return false; }
        ApplySavedItems(items);
        vehicle.ApplySave(state);
        return true;
    }

    [ServerRpc]
    private void RegisterPlayerServerRpc(FixedString64Bytes authenticatedPlayerId, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

        playerId = authenticatedPlayerId.ToString();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            ReportLoadError("The player ID was empty.");
            return;
        }

        try
        {
            ApplySavedItems(HostInventoryStore.LoadInventory(playerId));
            collectedPickupIds.Clear();

            List<string> savedPickupIds = HostInventoryStore.LoadCollectedPickups();
            for (int i = 0; i < savedPickupIds.Count; i++)
            {
                string pickupId = savedPickupIds[i];
                if (!string.IsNullOrWhiteSpace(pickupId)
                    && System.Text.Encoding.UTF8.GetByteCount(pickupId) <= 29)
                {
                    collectedPickupIds.Add(pickupId);
                }
            }

            List<HostInventoryStore.SavedPickup> savedDroppedPickups = HostInventoryStore.LoadDroppedPickups();
            inventoryReady.Value = true;
            foreach (string pickupId in collectedPickupIds)
            {
                SetPickupCollectedClientRpc(new FixedString32Bytes(pickupId));
            }

            for (int i = 0; i < savedDroppedPickups.Count; i++)
            {
                HostInventoryStore.SavedPickup pickup = savedDroppedPickups[i];
                SpawnDroppedPickupClientRpc(
                    pickup.pickupId,
                    pickup.itemId,
                    pickup.itemName,
                    pickup.count,
                    new Vector3(pickup.x, pickup.y, pickup.z));
            }
        }
        catch (IOException exception)
        {
            ReportLoadError(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            ReportLoadError(exception.Message);
        }
        catch (ArgumentException exception)
        {
            ReportLoadError(exception.Message);
        }
        catch (InvalidDataException exception)
        {
            ReportLoadError(exception.Message);
        }
    }

    [ServerRpc]
    private void RequestPickupServerRpc(FixedString32Bytes requestedPickupId, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId || !inventoryReady.Value) return;

        string pickupId = requestedPickupId.ToString();
        if (collectedPickupIds.Contains(pickupId))
        {
            ShowStatusClientRpc(new FixedString128Bytes("That item has already been collected."));
            return;
        }

        WorldPickup pickup = FindPickup(pickupId);
        if (pickup == null || !pickup.CanBePickedUp)
        {
            ShowStatusClientRpc(new FixedString128Bytes("That item is no longer available."));
            return;
        }

        if ((pickup.transform.position - transform.position).sqrMagnitude > PickupRange * PickupRange)
        {
            ShowStatusClientRpc(new FixedString128Bytes("Move closer to collect that item."));
            return;
        }

        List<HostInventoryStore.SavedItem> updatedItems = CreateSavedItems();
        if (!TryAddItem(updatedItems, pickup.ItemId, pickup.ItemName, pickup.ItemCount))
        {
            ShowStatusClientRpc(new FixedString128Bytes("Your inventory is full."));
            return;
        }

        if (!TrySave(updatedItems, pickupId, null, pickupId)) return;

        collectedPickupIds.Add(pickupId);
        ApplySavedItems(updatedItems);
        SetPickupCollectedClientRpc(requestedPickupId);
        ShowStatusClientRpc(new FixedString128Bytes($"Picked up {pickup.ItemCount} {pickup.ItemName}."));
    }

    [ServerRpc]
    private void RequestUseServerRpc(int slotIndex, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId || !inventoryReady.Value) return;
        if (slotIndex < 0 || slotIndex >= slots.Count) return;

        InventorySlot selectedItem = slots[slotIndex];
        List<HostInventoryStore.SavedItem> updatedItems = CreateSavedItems();
        if (selectedItem.Count <= 1)
        {
            updatedItems.RemoveAt(slotIndex);
        }
        else
        {
            updatedItems[slotIndex].count--;
        }

        if (!TrySave(updatedItems)) return;

        ApplySavedItems(updatedItems);
        ShowStatusClientRpc(new FixedString128Bytes($"Used {selectedItem.ItemName.ToString()}."));
    }

    [ServerRpc]
    private void RequestDropServerRpc(int slotIndex, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId || !inventoryReady.Value) return;
        if (slotIndex < 0 || slotIndex >= slots.Count) return;

        InventorySlot selectedItem = slots[slotIndex];
        if (selectedItem.Count <= 0) return;

        string pickupId = $"drop-{Guid.NewGuid():N}".Substring(0, 29);
        Vector3 position = GetDropPosition();
        List<HostInventoryStore.SavedItem> updatedItems = CreateSavedItems();
        updatedItems.RemoveAt(slotIndex);

        HostInventoryStore.SavedPickup droppedPickup = new HostInventoryStore.SavedPickup
        {
            pickupId = pickupId,
            itemId = selectedItem.ItemId.ToString(),
            itemName = selectedItem.ItemName.ToString(),
            count = selectedItem.Count,
            x = position.x,
            y = position.y,
            z = position.z
        };

        if (!TrySave(updatedItems, null, droppedPickup)) return;

        ApplySavedItems(updatedItems);
        SpawnDroppedPickupClientRpc(
            pickupId,
            droppedPickup.itemId,
            droppedPickup.itemName,
            droppedPickup.count,
            position);
        ShowStatusClientRpc(new FixedString128Bytes($"Dropped {droppedPickup.count} {droppedPickup.itemName}."));
    }

    [ServerRpc]
    private void RequestSplitServerRpc(int slotIndex, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId || !inventoryReady.Value) return;
        if (slotIndex < 0 || slotIndex >= slots.Count) return;

        InventorySlot selectedItem = slots[slotIndex];
        if (selectedItem.Count < 2)
        {
            ShowStatusClientRpc(new FixedString128Bytes("A stack needs at least two items to split."));
            return;
        }

        if (slots.Count >= MaximumSlots)
        {
            ShowStatusClientRpc(new FixedString128Bytes("Your inventory has no free slot for the split."));
            return;
        }

        int newStackCount = selectedItem.Count / 2;
        int remainingCount = selectedItem.Count - newStackCount;
        List<HostInventoryStore.SavedItem> updatedItems = CreateSavedItems();
        updatedItems[slotIndex].count = remainingCount;
        updatedItems.Insert(slotIndex + 1, new HostInventoryStore.SavedItem
        {
            itemId = selectedItem.ItemId.ToString(),
            itemName = selectedItem.ItemName.ToString(),
            count = newStackCount
        });

        if (!TrySave(updatedItems)) return;

        ApplySavedItems(updatedItems);
        ShowStatusClientRpc(new FixedString128Bytes(
            $"Split into stacks of {remainingCount} and {newStackCount}."));
    }

    [ServerRpc]
    private void RequestCombineServerRpc(int slotIndex, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId || !inventoryReady.Value) return;
        if (slotIndex < 0 || slotIndex >= slots.Count) return;

        InventorySlot selectedItem = slots[slotIndex];
        string itemId = selectedItem.ItemId.ToString();
        string itemName = selectedItem.ItemName.ToString();
        List<HostInventoryStore.SavedItem> currentItems = CreateSavedItems();
        int firstMatchingIndex = -1;
        int matchingStackCount = 0;
        int remainingCount = 0;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].ItemId.Equals(selectedItem.ItemId))
            {
                if (firstMatchingIndex < 0) firstMatchingIndex = i;
                matchingStackCount++;
                remainingCount += slots[i].Count;
            }
        }

        if (matchingStackCount < 2)
        {
            ShowStatusClientRpc(new FixedString128Bytes("There are no matching stacks to combine."));
            return;
        }

        List<HostInventoryStore.SavedItem> combinedItems = new List<HostInventoryStore.SavedItem>(currentItems.Count);
        for (int i = 0; i < currentItems.Count; i++)
        {
            if (currentItems[i].itemId == itemId)
            {
                if (i == firstMatchingIndex)
                {
                    while (remainingCount > 0)
                    {
                        int stackCount = Mathf.Min(remainingCount, MaximumStackSize);
                        combinedItems.Add(new HostInventoryStore.SavedItem
                        {
                            itemId = itemId,
                            itemName = itemName,
                            count = stackCount
                        });
                        remainingCount -= stackCount;
                    }
                }

                continue;
            }

            combinedItems.Add(currentItems[i]);
        }

        if (!TrySave(combinedItems)) return;

        ApplySavedItems(combinedItems);
        ShowStatusClientRpc(new FixedString128Bytes($"Combined {matchingStackCount} matching stacks."));
    }

    [ClientRpc]
    private void SpawnDroppedPickupClientRpc(
        string pickupId,
        string itemId,
        string itemName,
        int itemCount,
        Vector3 position)
    {
        WorldPickup.SpawnDroppedReplica(pickupId, itemId, itemName, itemCount, position);
    }

    [ClientRpc]
    private void SetPickupCollectedClientRpc(FixedString32Bytes pickupId)
    {
        WorldPickup[] pickups = FindObjectsByType<WorldPickup>(FindObjectsInactive.Include);

        for (int i = 0; i < pickups.Length; i++)
        {
            if (pickups[i].PickupId == pickupId.ToString())
            {
                pickups[i].SetCollected();
                return;
            }
        }
    }

    [ClientRpc]
    private void ShowStatusClientRpc(FixedString128Bytes message)
    {
        SetLocalStatus(message.ToString());
    }

    private bool TryAddItem(List<HostInventoryStore.SavedItem> items, string itemId, string itemName, int count)
    {
        if (count <= 0) return false;

        int remaining = count;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].itemId == itemId && items[i].count < MaximumStackSize)
            {
                int amountToAdd = Mathf.Min(remaining, MaximumStackSize - items[i].count);
                items[i].count += amountToAdd;
                remaining -= amountToAdd;
                if (remaining == 0) return true;
            }
        }

        while (remaining > 0 && items.Count < MaximumSlots)
        {
            int amountToAdd = Mathf.Min(remaining, MaximumStackSize);
            items.Add(new HostInventoryStore.SavedItem
            {
                itemId = itemId,
                itemName = itemName,
                count = amountToAdd
            });
            remaining -= amountToAdd;
        }

        return remaining == 0;
    }

    private List<HostInventoryStore.SavedItem> CreateSavedItems()
    {
        List<HostInventoryStore.SavedItem> savedItems = new List<HostInventoryStore.SavedItem>(slots.Count);
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            savedItems.Add(new HostInventoryStore.SavedItem
            {
                itemId = slot.ItemId.ToString(),
                itemName = slot.ItemName.ToString(),
                count = slot.Count
            });
        }

        return savedItems;
    }

    private void ApplySavedItems(List<HostInventoryStore.SavedItem> savedItems)
    {
        slots.Clear();
        for (int i = 0; i < savedItems.Count && i < MaximumSlots; i++)
        {
            HostInventoryStore.SavedItem item = savedItems[i];
            if (item == null || string.IsNullOrWhiteSpace(item.itemId)
                || string.IsNullOrWhiteSpace(item.itemName) || item.count <= 0)
            {
                continue;
            }

            slots.Add(new InventorySlot(item.itemId, item.itemName, Mathf.Min(item.count, MaximumStackSize)));
        }
    }

    private bool TrySave(
        List<HostInventoryStore.SavedItem> items,
        string collectedPickupId = null,
        HostInventoryStore.SavedPickup droppedPickup = null,
        string removedDroppedPickupId = null)
    {
        try
        {
            HostInventoryStore.SaveInventory(
                playerId,
                items,
                collectedPickupId,
                droppedPickup,
                removedDroppedPickupId);
            return true;
        }
        catch (IOException exception)
        {
            ReportSaveError(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            ReportSaveError(exception.Message);
        }
        catch (InvalidDataException exception)
        {
            ReportSaveError(exception.Message);
        }

        return false;
    }

    private Vector3 GetDropPosition()
    {
        Vector3 position = transform.position + transform.forward * 1.5f;
        Vector3 rayOrigin = position + Vector3.up * 2f;
        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                10f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * 0.5f;
        }

        position.y -= 0.5f;
        return position;
    }

    private WorldPickup FindPickup(string pickupId)
    {
        WorldPickup[] pickups = FindObjectsByType<WorldPickup>(FindObjectsInactive.Include);

        for (int i = 0; i < pickups.Length; i++)
        {
            if (pickups[i].PickupId == pickupId)
            {
                return pickups[i];
            }
        }

        return null;
    }

    private void ReportLoadError(string details)
    {
        Debug.LogError($"Could not load host inventory for player {playerId}: {details}", this);
        ShowStatusClientRpc(new FixedString128Bytes("Inventory could not be loaded. Check the host log."));
    }

    private void ReportSaveError(string details)
    {
        Debug.LogError($"Could not save host inventory for player {playerId}: {details}", this);
        ShowStatusClientRpc(new FixedString128Bytes("Inventory could not be saved. Check the host log."));
    }

    private void SetLocalStatus(string message)
    {
        statusMessage = message;
        statusExpiresAt = Time.unscaledTime + 4f;
    }
}
