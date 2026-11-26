using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerInteraction : NetworkBehaviour
{
    private enum MenuContext
    {
        Pickup,
        UseItem,
        GlowCube
    }

    private const int GridColumns = 6;
    private const float SlotSize = 62f;
    private const float SlotSpacing = 6f;
    private static readonly Rect InventoryPanel = new Rect(12f, 90f, 430f, 330f);

    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerChat playerChat;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float interactionRange = 5f;

    private readonly RaycastHit[] raycastHits = new RaycastHit[32];
    private Camera mainCamera;
    private VehicleInteraction vehicleInteraction;
    private bool inventoryOpen;
    private bool radialMenuOpen;
    private bool isDraggingItem;
    private int draggedSlotIndex = -1;
    private MenuContext menuContext;
    private WorldPickup selectedPickup;
    private GlowCube selectedGlowCube;
    private int selectedSlot;
    private Vector2 radialCenter;
    private Rect radialBounds;

    public void SetVehicleRiderHeight(float height)
    {
        if (visualRoot == null)
        {
            Debug.LogError("Cannot adjust rider height because the player visual root is not assigned.", this);
            return;
        }

        visualRoot.localPosition = Vector3.up * height;
    }

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (playerChat == null) playerChat = GetComponent<PlayerChat>();
        vehicleInteraction = GetComponent<VehicleInteraction>();
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (playerChat != null && playerChat.IsChatActive)
        {
            CloseRadialMenu();
            return;
        }

        // Check if radial menu should close due to mouse movement
        if (radialMenuOpen && radialBounds.width > 0)
        {
            Mouse inputMouse = Mouse.current;
            if (inputMouse != null)
            {
                Vector2 mousePosition = inputMouse.position.ReadValue();
                Vector2 guiPosition = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
                
                if (!radialBounds.Contains(guiPosition))
                {
                    CloseRadialMenu();
                }
            }
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (radialMenuOpen)
                {
                    CloseRadialMenu();
                }
                else
                {
                    inventoryOpen = false;
                }
                return;
            }

            if (keyboard.iKey.wasPressedThisFrame && !radialMenuOpen)
            {
                inventoryOpen = !inventoryOpen;
                Debug.Log($"Inventory toggled: {inventoryOpen}");
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (isDraggingItem && !mouse.leftButton.isPressed)
        {
            isDraggingItem = false;
            draggedSlotIndex = -1;
            return;
        }

        // Cancel drag on right-click
        if (isDraggingItem && mouse.rightButton.wasPressedThisFrame)
        {
            isDraggingItem = false;
            draggedSlotIndex = -1;
            return;
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            Vector2 mousePosition = mouse.position.ReadValue();
            Vector2 guiPosition = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
            if (vehicleInteraction != null && vehicleInteraction.IsVehicleMenuOpen) return;
            if (vehicleInteraction != null && vehicleInteraction.IsRidingVehicle)
            {
                vehicleInteraction.OpenRidingMenu(guiPosition);
                return;
            }
            if (radialMenuOpen)
            {
                if (!radialBounds.Contains(guiPosition)) CloseRadialMenu();
                return;
            }

            if (inventoryOpen && InventoryPanel.Contains(guiPosition)) return;
            TryOpenInteractionMenu(mousePosition, guiPosition);
        }
    }

    private void OnGUI()
    {
        if (!IsOwner || (playerChat != null && playerChat.IsChatActive)) return;

        if (inventoryOpen && (vehicleInteraction == null || !vehicleInteraction.IsVehicleMenuOpen))
        {
            DrawInventory();
        }

        if (radialMenuOpen)
        {
            DrawRadialMenu();
        }

        if (inventory != null && !string.IsNullOrEmpty(inventory.StatusMessage))
        {
            GUI.Label(new Rect(12f, Screen.height - 42f, 360f, 28f), inventory.StatusMessage);
        }
    }

    private void DrawInventory()
    {
        Debug.Log($"DrawInventory called. Inventory ready: {(inventory != null ? inventory.IsReady : false)}");
        GUI.Box(InventoryPanel, "Inventory / Transfer (I)");
        if (inventory == null || !inventory.IsReady)
        {
            GUI.Label(new Rect(InventoryPanel.x + 12f, InventoryPanel.y + 32f, 300f, 24f), "Loading inventory...");
            return;
        }

        for (int i = 0; i < PlayerInventory.MaximumSlots; i++)
        {
            int column = i % GridColumns;
            int row = i / GridColumns;
            Rect slotRect = new Rect(
                InventoryPanel.x + 12f + column * (SlotSize + SlotSpacing),
                InventoryPanel.y + 38f + row * (SlotSize + SlotSpacing),
                SlotSize,
                SlotSize);

            if (i < inventory.Slots.Count)
            {
                InventorySlot slot = inventory.GetSlot(i);
                string label = $"{slot.ItemName}\nx{slot.Count}";
                
                if (isDraggingItem && draggedSlotIndex == i)
                {
                    GUI.Box(slotRect, label);
                }
                else if (radialMenuOpen)
                {
                    GUI.Box(slotRect, label);
                }
                else if (GUI.Button(slotRect, label))
                {
                    if (Event.current.button == 1)
                    {
                        OpenUseMenu(i, Event.current.mousePosition);
                    }
                }
                
                // Handle drag start - LEFT CLICK to drag
                if (!isDraggingItem && slotRect.Contains(Event.current.mousePosition))
                {
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        isDraggingItem = true;
                        draggedSlotIndex = i;
                        Debug.Log($"Started dragging from player inventory slot {i}");
                        Event.current.Use();
                    }
                }
            }
            else
            {
                GUI.Box(slotRect, string.Empty);
            }
        }
        
        // Draw dragged item at mouse position
        if (isDraggingItem && draggedSlotIndex >= 0 && draggedSlotIndex < inventory.Slots.Count)
        {
            InventorySlot draggedSlot = inventory.GetSlot(draggedSlotIndex);
            Vector2 mousePos = Event.current.mousePosition;
            GUI.Box(new Rect(mousePos.x - 30f, mousePos.y - 30f, SlotSize, SlotSize), 
                $"{draggedSlot.ItemName}\nx{draggedSlot.Count}");
        }
    }

    private void DrawRadialMenu()
    {
        const float buttonWidth = 112f;
        const float buttonHeight = 34f;
        const float radius = 72f;
        radialBounds = new Rect(radialCenter.x - 130f, radialCenter.y - 112f, 260f, 224f);
        GUI.Box(radialBounds, string.Empty);

        bool canSplit = CanSplitSelectedStack;
        bool canCombine = CanCombineSelectedStacks;
        int splitActionIndex = canSplit ? 2 : -1;
        int combineActionIndex = canCombine ? 2 + (canSplit ? 1 : 0) : -1;
        int actionCount = menuContext == MenuContext.UseItem
            ? 3 + (canSplit ? 1 : 0) + (canCombine ? 1 : 0)
            : 2;
        for (int i = 0; i < actionCount; i++)
        {
            float angle = (-90f + i * 360f / actionCount) * Mathf.Deg2Rad;
            Vector2 buttonCenter = radialCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Rect buttonRect = new Rect(
                buttonCenter.x - buttonWidth * 0.5f,
                buttonCenter.y - buttonHeight * 0.5f,
                buttonWidth,
                buttonHeight);
            string label = GetActionLabel(i, splitActionIndex, combineActionIndex);

            if (GUI.Button(buttonRect, label))
            {
                if (i == 0)
                {
                    ExecutePrimaryAction();
                }
                else if (menuContext == MenuContext.UseItem && i == 1)
                {
                    ExecuteDropAction();
                }
                else if (menuContext == MenuContext.UseItem && i == splitActionIndex)
                {
                    ExecuteSplitAction();
                }
                else if (menuContext == MenuContext.UseItem && i == combineActionIndex)
                {
                    ExecuteCombineAction();
                }
                CloseRadialMenu();
            }
        }
    }

    private void TryOpenInteractionMenu(Vector2 mousePosition, Vector2 guiPosition)
    {
        if (mainCamera == null || !mainCamera.isActiveAndEnabled)
        {
            mainCamera = Camera.main;
        }
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(mousePosition);
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            raycastHits,
            200f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);

        WorldPickup closestPickup = null;
        GlowCube closestGlowCube = null;
        Vehicle closestVehicle = null;
        float closestHitDistance = float.MaxValue;
        float maxRangeSquared = interactionRange * interactionRange;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = raycastHits[i].collider;
            if (hitCollider == null) continue;

            Transform hitTransform = hitCollider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;

            WorldPickup pickup = hitCollider.GetComponentInParent<WorldPickup>();
            if (pickup != null && pickup.CanBePickedUp
                && (pickup.transform.position - transform.position).sqrMagnitude <= maxRangeSquared
                && raycastHits[i].distance < closestHitDistance)
            {
                closestPickup = pickup;
                closestGlowCube = null;
                closestVehicle = null;
                closestHitDistance = raycastHits[i].distance;
            }

            GlowCube glowCube = hitCollider.GetComponentInParent<GlowCube>();
            if (glowCube != null && glowCube.CanToggle
                && (glowCube.transform.position - transform.position).sqrMagnitude <= maxRangeSquared
                && raycastHits[i].distance < closestHitDistance)
            {
                closestPickup = null;
                closestGlowCube = glowCube;
                closestVehicle = null;
                closestHitDistance = raycastHits[i].distance;
            }

            Vehicle vehicle = hitCollider.GetComponentInParent<Vehicle>();
            if (vehicle != null && !vehicle.IsOccupied && vehicle.IsWithinRange(transform.position, interactionRange)
                && raycastHits[i].distance < closestHitDistance)
            {
                closestPickup = null;
                closestGlowCube = null;
                closestVehicle = vehicle;
                closestHitDistance = raycastHits[i].distance;
            }
        }

        if (closestVehicle != null && vehicleInteraction != null)
        {
            CloseRadialMenu();
            vehicleInteraction.OpenMenu(closestVehicle, guiPosition);
            return;
        }
        if (closestPickup != null)
        {
            selectedPickup = closestPickup;
            selectedGlowCube = null;
            menuContext = MenuContext.Pickup;
        }
        else if (closestGlowCube != null)
        {
            selectedPickup = null;
            selectedGlowCube = closestGlowCube;
            menuContext = MenuContext.GlowCube;
        }
        else
        {
            return;
        }

        OpenRadialMenu(guiPosition);
    }

    private void OpenUseMenu(int slotIndex, Vector2 guiPosition)
    {
        selectedSlot = slotIndex;
        menuContext = MenuContext.UseItem;
        selectedPickup = null;
        selectedGlowCube = null;
        OpenRadialMenu(guiPosition);
    }

    private void OpenRadialMenu(Vector2 guiPosition)
    {
        radialCenter = new Vector2(
            Mathf.Clamp(guiPosition.x, 130f, Screen.width - 130f),
            Mathf.Clamp(guiPosition.y, 112f, Screen.height - 112f));
        radialMenuOpen = true;
    }

    private string GetActionLabel(int index, int splitActionIndex, int combineActionIndex)
    {
        if (menuContext == MenuContext.Pickup)
        {
            return index == 0 ? "Pick up" : "Cancel";
        }

        if (menuContext == MenuContext.GlowCube)
        {
            if (index != 0) return "Cancel";
            return selectedGlowCube != null && selectedGlowCube.IsOn ? "Turn off" : "Turn on";
        }

        if (index == 0) return "Use";
        if (index == 1) return "Drop stack";
        if (index == splitActionIndex) return "Split stack";
        if (index == combineActionIndex) return "Combine matching";
        return "Cancel";
    }

    private bool CanSplitSelectedStack
    {
        get
        {
            return menuContext == MenuContext.UseItem
                && inventory != null
                && inventory.IsReady
                && selectedSlot >= 0
                && selectedSlot < inventory.Slots.Count
                && inventory.GetSlot(selectedSlot).Count > 1;
        }
    }

    private bool CanCombineSelectedStacks
    {
        get
        {
            if (menuContext != MenuContext.UseItem || inventory == null
                || !inventory.IsReady || selectedSlot < 0 || selectedSlot >= inventory.Slots.Count)
            {
                return false;
            }

            InventorySlot selectedItem = inventory.GetSlot(selectedSlot);
            int matchingStackCount = 0;
            int totalCount = 0;
            for (int i = 0; i < inventory.Slots.Count; i++)
            {
                InventorySlot slot = inventory.GetSlot(i);
                if (!slot.ItemId.Equals(selectedItem.ItemId)) continue;
                matchingStackCount++;
                totalCount += slot.Count;
            }

            if (matchingStackCount < 2) return false;

            int remainingCount = totalCount;
            for (int i = 0; i < inventory.Slots.Count; i++)
            {
                InventorySlot slot = inventory.GetSlot(i);
                if (!slot.ItemId.Equals(selectedItem.ItemId)) continue;

                int expectedCount = Mathf.Min(remainingCount, PlayerInventory.MaximumStackSize);
                if (slot.Count != expectedCount) return true;
                remainingCount -= expectedCount;
            }

            return false;
        }
    }

    private void ExecutePrimaryAction()
    {
        if (menuContext == MenuContext.Pickup && selectedPickup != null)
        {
            if (inventory != null)
            {
                inventory.RequestPickup(selectedPickup.PickupId);
            }
        }
        else if (menuContext == MenuContext.UseItem)
        {
            if (inventory != null)
            {
                inventory.RequestUse(selectedSlot);
            }
        }
        else if (menuContext == MenuContext.GlowCube && selectedGlowCube != null)
        {
            SetGlowServerRpc(selectedGlowCube.CubeId, !selectedGlowCube.IsOn);
        }
    }

    private void ExecuteDropAction()
    {
        if (inventory != null)
        {
            inventory.RequestDrop(selectedSlot);
        }
    }

    private void ExecuteSplitAction()
    {
        if (inventory != null)
        {
            inventory.RequestSplit(selectedSlot);
        }
    }

    private void ExecuteCombineAction()
    {
        if (inventory != null)
        {
            inventory.RequestCombine(selectedSlot);
        }
    }

    [ServerRpc]
    private void SetGlowServerRpc(string cubeId, bool isOn, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

        GlowCube glowCube = FindGlowCube(cubeId);
        if (glowCube == null || !glowCube.CanToggle) return;
        if ((glowCube.transform.position - transform.position).sqrMagnitude > interactionRange * interactionRange) return;

        glowCube.SetGlow(isOn);
        SetGlowClientRpc(cubeId, isOn);
    }

    [ClientRpc]
    private void SetGlowClientRpc(string cubeId, bool isOn)
    {
        GlowCube[] glowCubes = FindObjectsByType<GlowCube>(FindObjectsInactive.Include);
        for (int i = 0; i < glowCubes.Length; i++)
        {
            if (glowCubes[i].CubeId == cubeId)
            {
                glowCubes[i].SetGlow(isOn);
            }
        }
    }

    private static GlowCube FindGlowCube(string cubeId)
    {
        GlowCube[] glowCubes = FindObjectsByType<GlowCube>(FindObjectsInactive.Include);
        for (int i = 0; i < glowCubes.Length; i++)
        {
            if (glowCubes[i].CubeId == cubeId)
            {
                return glowCubes[i];
            }
        }

        return null;
    }

    private void CloseRadialMenu()
    {
        radialMenuOpen = false;
        selectedPickup = null;
        selectedGlowCube = null;
    }
}
