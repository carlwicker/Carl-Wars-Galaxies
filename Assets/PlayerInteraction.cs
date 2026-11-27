using TMPro;
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
    internal const float ItemInteractionRange = 2f;
    private const int InteractionCircleSegments = 64;
    private const float InteractionIndicatorLineWidth = 0.05f;
    internal const float PistolRange = 10f;
    private const int PistolCircleSegments = 256;
    private const float PistolIndicatorGroundOffset = 0.15f;
    internal const float SlotSize = 86f;
    private const float SlotSpacing = 6f;
    private static Rect InventoryPanel => new Rect(12f, 90f,
        Mathf.Min(12f + GridColumns * (SlotSize + SlotSpacing) + 12f, Mathf.Max(0f, Screen.width - 24f)),
        Mathf.Min(38f + 4f * (SlotSize + SlotSpacing) + 12f, Mathf.Max(0f, Screen.height - 102f)));
    private static GUISkin inventorySkin;
    private static GUIStyle inventorySlotStyle;

    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerChat playerChat;
    [SerializeField] private Transform visualRoot;
    private readonly RaycastHit[] raycastHits = new RaycastHit[32];
    private Camera mainCamera;
    private VehicleInteraction vehicleInteraction;
    private SelectionOutline selectionOutline;
    private GameObject interactionRangeIndicator;
    private Material interactionIndicatorMaterial;
    private LineRenderer pistolRangeIndicator;
    private Collider pistolRangeGround;
    private Vector3 pistolRangeLastPosition;
    private bool pistolRangePositionSet;
    private bool inventoryOpen;
    private bool craftingOpen;
    private CraftingTerminal selectedTerminal;
    private CraftingSchematic selectedSchematic;
    private int selectedMetal = -1;
    private int selectedPetrochemicals = -1;
    private int selectedPrototype = -1;
    private string surveyMessage = "Ready - press V or select Survey.";
    private Vector2 craftingScroll;
    private bool radialMenuOpen;
    private bool isDraggingItem;
    private int draggedSlotIndex = -1;
    private MenuContext menuContext;
    private WorldPickup selectedPickup;
    private GlowCube selectedGlowCube;
    private int selectedSlot;
    private int selectedInventorySlot = -1;
    private Vector2 inventoryScrollPosition;
    private Vector2 radialCenter;
    private Rect radialBounds;

    public Transform SelectedTarget => selectionOutline != null ? selectionOutline.Target : null;
    public bool IsCombatInputBlocked => inventoryOpen || craftingOpen || radialMenuOpen
        || (vehicleInteraction != null && vehicleInteraction.IsVehicleMenuOpen);

    public void SetVehicleRiderHeight(float height)
    {
        if (visualRoot == null)
        {
            Debug.LogError("Cannot adjust rider height because the player visual root is not assigned.", this);
            return;
        }

        CharacterController controller = GetComponent<CharacterController>();
        float halfPlayerHeight = controller != null ? controller.height * 0.5f : 1f;
        visualRoot.localPosition = Vector3.up * (height == 0f ? 0f : height - halfPlayerHeight);
    }

    private void Awake()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (playerChat == null) playerChat = GetComponent<PlayerChat>();
        vehicleInteraction = GetComponent<VehicleInteraction>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        interactionRangeIndicator = new GameObject("Item Interaction Range");
        interactionRangeIndicator.transform.SetParent(transform, false);

        CharacterController controller = GetComponent<CharacterController>();
        Vector3 capsuleBottom = controller != null
            ? controller.center + Vector3.down * (controller.height * 0.5f)
            : Vector3.down;
        interactionRangeIndicator.transform.localPosition = capsuleBottom + Vector3.up * 0.015f;

        LineRenderer circle = interactionRangeIndicator.AddComponent<LineRenderer>();
        circle.useWorldSpace = false;
        circle.loop = true;
        circle.positionCount = InteractionCircleSegments;
        circle.startWidth = InteractionIndicatorLineWidth;
        circle.endWidth = InteractionIndicatorLineWidth;
        circle.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        circle.receiveShadows = false;

        for (int i = 0; i < InteractionCircleSegments; i++)
        {
            float angle = 2f * Mathf.PI * i / InteractionCircleSegments;
            circle.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * ItemInteractionRange,
                0f,
                Mathf.Sin(angle) * ItemInteractionRange));
        }

        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader == null)
        {
            Debug.LogError("Cannot display the item interaction range because the line shader is unavailable.", this);
            return;
        }

        interactionIndicatorMaterial = new Material(lineShader);
        Color indicatorColor = new Color(0.8f, 0.8f, 0.8f);
        interactionIndicatorMaterial.color = Color.white;
        circle.startColor = indicatorColor;
        circle.endColor = indicatorColor;
        circle.material = interactionIndicatorMaterial;

        GameObject labelObject = new GameObject("Interaction Label");
        labelObject.transform.SetParent(interactionRangeIndicator.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 0.05f, ItemInteractionRange - 0.2f);
        labelObject.transform.localScale = Vector3.one * 0.04f;

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 48f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.rectTransform.sizeDelta = new Vector2(12f, 3f);
        label.text = "action";
        label.color = Color.white;
        labelObject.AddComponent<LookAtCamera>();
        labelObject.AddComponent<NameplateBackground>();

        CreatePistolRangeIndicator();
    }

    private void CreatePistolRangeIndicator()
    {
        GameObject indicator = new GameObject("Pistol Range (10m)");
        indicator.transform.SetParent(transform, false);
        pistolRangeIndicator = indicator.AddComponent<LineRenderer>();
        pistolRangeIndicator.useWorldSpace = true;
        pistolRangeIndicator.loop = true;
        pistolRangeIndicator.positionCount = PistolCircleSegments;
        pistolRangeIndicator.startWidth = 0.2f;
        pistolRangeIndicator.endWidth = 0.2f;
        pistolRangeIndicator.startColor = Color.green;
        pistolRangeIndicator.endColor = Color.green;
        pistolRangeIndicator.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pistolRangeIndicator.receiveShadows = false;
        pistolRangeIndicator.sharedMaterial = interactionIndicatorMaterial;

        UnevenGround ground = FindAnyObjectByType<UnevenGround>();
        pistolRangeGround = ground != null ? ground.GetComponent<Collider>() : null;
        if (pistolRangeGround == null)
            Debug.LogWarning("Pistol range indicator could not find terrain; using the player's ground height.", this);

        UpdatePistolRangeIndicator();
    }

    private void LateUpdate()
    {
        if (pistolRangeIndicator != null && (!pistolRangePositionSet || transform.position != pistolRangeLastPosition))
            UpdatePistolRangeIndicator();
    }

    private void UpdatePistolRangeIndicator()
    {
        Vector3 center = transform.position;
        CharacterController controller = GetComponent<CharacterController>();
        float fallbackHeight = controller != null
            ? transform.TransformPoint(controller.center + Vector3.down * (controller.height * 0.5f)).y
            : center.y;

        for (int i = 0; i < PistolCircleSegments; i++)
        {
            float angle = 2f * Mathf.PI * i / PistolCircleSegments;
            Vector3 point = new Vector3(center.x + Mathf.Cos(angle) * PistolRange,
                fallbackHeight, center.z + Mathf.Sin(angle) * PistolRange);
            if (pistolRangeGround != null)
            {
                Bounds bounds = pistolRangeGround.bounds;
                Ray ray = new Ray(new Vector3(point.x, bounds.max.y + 1f, point.z), Vector3.down);
                if (pistolRangeGround.Raycast(ray, out RaycastHit hit, bounds.size.y + 2f))
                    point.y = hit.point.y;
            }
            point.y += PistolIndicatorGroundOffset;
            pistolRangeIndicator.SetPosition(i, point);
        }

        pistolRangeLastPosition = center;
        pistolRangePositionSet = true;
    }

    public override void OnNetworkDespawn()
    {
        if (selectionOutline != null) selectionOutline.SetTarget(null);
        CloseRadialMenu();

        if (interactionRangeIndicator != null)
        {
            Destroy(interactionRangeIndicator);
            interactionRangeIndicator = null;
        }

        if (pistolRangeIndicator != null)
        {
            Destroy(pistolRangeIndicator.gameObject);
            pistolRangeIndicator = null;
            pistolRangePositionSet = false;
            pistolRangeGround = null;
        }

        if (interactionIndicatorMaterial != null)
        {
            Destroy(interactionIndicatorMaterial);
            interactionIndicatorMaterial = null;
        }
    }

    internal static bool IsWithinItemInteractionRange(Vector3 playerPosition, Vector3 itemPosition)
    {
        Vector3 offset = itemPosition - playerPosition;
        offset.y = 0f;
        return offset.sqrMagnitude <= ItemInteractionRange * ItemInteractionRange;
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (playerChat != null && playerChat.IsChatActive)
        {
            CloseRadialMenu();
            return;
        }

        if (craftingOpen && (selectedTerminal == null || !selectedTerminal.isActiveAndEnabled
            || !IsWithinItemInteractionRange(transform.position, selectedTerminal.transform.position)
            || !selectedTerminal.IsWithinRange(transform.position)))
            craftingOpen = false;

        if (radialMenuOpen && menuContext != MenuContext.UseItem && !IsWorldMenuTargetValid())
            CloseRadialMenu();

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
                if (craftingOpen)
                {
                    craftingOpen = false;
                }
                else if (radialMenuOpen)
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
                if (vehicleInteraction != null && vehicleInteraction.IsRidingVehicle)
                {
                    inventoryOpen = false;
                }
                else
                {
                    inventoryOpen = !inventoryOpen;
                    Debug.Log($"Inventory toggled: {inventoryOpen}");
                }
            }

            if (keyboard.vKey.wasPressedThisFrame)
            {
                if (inventory != null && inventory.IsSurveyingToolActive)
                    SurveyResources();
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePosition = mouse.position.ReadValue();
            Vector2 guiPosition = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
            if ((vehicleInteraction == null || !vehicleInteraction.IsVehicleMenuOpen)
                && !PlayerCombat.IsPointerOverActionBar(guiPosition)
                && !(inventoryOpen && InventoryPanel.Contains(guiPosition))
                && !(craftingOpen && CraftingPanel.Contains(guiPosition))
                && !(inventory != null && inventory.IsSurveyingToolActive && SurveyPanel.Contains(guiPosition))
                && !(radialMenuOpen && radialBounds.Contains(guiPosition)))
            {
                SelectWorldTarget(mousePosition, guiPosition);
            }
        }

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
    }

    private void SelectWorldTarget(Vector2 mousePosition, Vector2 guiPosition)
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
            QueryTriggerInteraction.Ignore);

        Collider closestCollider = null;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = raycastHits[i].collider;
            if (hitCollider == null) continue;

            Transform hitTransform = hitCollider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform)) continue;
            if (raycastHits[i].distance < closestDistance)
            {
                closestCollider = hitCollider;
                closestDistance = raycastHits[i].distance;
            }
        }

        Transform target = null;
        if (closestCollider != null)
        {
            Vehicle vehicle = closestCollider.GetComponentInParent<Vehicle>();
            WorldPickup pickup = closestCollider.GetComponentInParent<WorldPickup>();
            GlowCube glowCube = closestCollider.GetComponentInParent<GlowCube>();
            CraftingTerminal terminal = closestCollider.GetComponentInParent<CraftingTerminal>();
            CombatDummy dummy = closestCollider.GetComponentInParent<CombatDummy>();
            if (vehicle != null)
            {
                target = vehicle.transform;
            }
            else if (pickup != null && pickup.CanBePickedUp)
            {
                target = pickup.transform;
            }
            else if (glowCube != null && glowCube.CanToggle)
            {
                target = glowCube.transform;
            }
            else if (terminal != null && terminal.isActiveAndEnabled)
            {
                target = terminal.transform;
            }
            else if (dummy != null && dummy.IsSpawned && !dummy.IsDefeated)
            {
                target = dummy.transform;
            }
        }

        HandleWorldTargetClick(target, guiPosition);
    }

    private void HandleWorldTargetClick(Transform target, Vector2 guiPosition)
    {
        if (target != null && selectionOutline != null && target == selectionOutline.Target
            && target.gameObject.activeInHierarchy)
        {
            TryOpenInteractionMenu(target, guiPosition);
            return;
        }

        CloseRadialMenu();
        if (selectionOutline == null)
        {
            selectionOutline = GetComponent<SelectionOutline>();
            if (selectionOutline == null) selectionOutline = gameObject.AddComponent<SelectionOutline>();
        }

        selectionOutline.SetTarget(target);
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

        if (craftingOpen) DrawCrafting();

        if (inventory != null && inventory.IsSurveyingToolActive)
            DrawSurveyPanel();
        if (inventory != null && !string.IsNullOrEmpty(inventory.StatusMessage))
        {
            GUI.Label(new Rect(12f, Screen.height - 42f, 360f, 28f), inventory.StatusMessage);
        }
    }

    private static Rect CraftingPanel => new Rect((Screen.width - 500f) * 0.5f, 90f, 500f, 500f);
    private static Rect SurveyPanel => new Rect(12f, Mathf.Max(12f, Screen.height - 132f),
        Mathf.Min(520f, Mathf.Max(0f, Screen.width - 24f)), 44f);

    private void DrawSurveyPanel()
    {
        Rect panel = SurveyPanel;
        GUI.Box(panel, string.Empty);
        GUI.Label(new Rect(panel.x + 10f, panel.y + 10f, panel.width - 112f, 24f), surveyMessage);
        if (GUI.Button(new Rect(panel.xMax - 92f, panel.y + 7f, 82f, 30f), "Survey"))
            SurveyResources();
    }

    private void DrawCrafting()
    {
        Rect panel = CraftingPanel;
        GUI.Box(panel, "Crafting Terminal");
        GUILayout.BeginArea(new Rect(panel.x + 12, panel.y + 30, panel.width - 24, panel.height - 42));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Engine Cube")) SelectSchematic("engine");
        if (GUILayout.Button("Power Cube")) SelectSchematic("power");
        if (GUILayout.Button("Surveying Tool")) SelectSchematic("surveying_tool");
        GUILayout.EndHorizontal();
        if (selectedSchematic == null || inventory == null || !inventory.IsReady)
        {
            GUILayout.Label("Crafting schematic or inventory is unavailable.");
            if (GUILayout.Button("Close")) craftingOpen = false;
            GUILayout.EndArea();
            return;
        }
        GUILayout.Label($"{selectedSchematic.displayName}: {selectedSchematic.firstResourceCount} {selectedSchematic.firstResourceId} and "
            + $"{selectedSchematic.secondResourceCount} {selectedSchematic.secondResourceId}.");
        GUILayout.Label("Assembly: 90% success; failure consumes resources. Resource OQ and CD affect engine stats.");
        GUILayout.Label("Select one stack for each ingredient:");
        craftingScroll = GUILayout.BeginScrollView(craftingScroll, GUILayout.Height(180));
        if (inventory != null && inventory.IsReady)
        {
            for (int i = 0; i < inventory.Slots.Count; i++)
            {
                InventorySlot item = inventory.GetSlot(i);
                string id = item.ItemId.ToString();
                bool isPrototype = selectedSchematic.usesExperimentation && id == selectedSchematic.prototypeId;
                if (id != selectedSchematic.firstResourceId && id != selectedSchematic.secondResourceId
                    && !isPrototype) continue;
                bool selected = i == selectedMetal || i == selectedPetrochemicals || i == selectedPrototype;
                string details = isPrototype
                    ? $"Speed {item.Speed}%, points {item.ExperimentPoints}"
                    : id == "radioactive"
                        ? $"x{item.Count}, OQ {item.Quality}, PE {item.PotentialEnergy}, CR {item.ColdResistance}, "
                            + $"DR {item.DecayResistance}, ER {item.EntangleResistance}"
                        : $"x{item.Count}, OQ {item.Quality}, CD {item.Conductivity}";
                if (GUILayout.Button($"{(selected ? "* " : "")}{item.ItemName}: {details}"))
                {
                    if (id == selectedSchematic.firstResourceId) selectedMetal = i;
                    else if (id == selectedSchematic.secondResourceId) selectedPetrochemicals = i;
                    else selectedPrototype = i;
                }
            }
        }
        GUILayout.EndScrollView();
        if (GUILayout.Button("Assemble"))
        {
            inventory.RequestAssembly(selectedSchematic.schematicId, selectedMetal, selectedPetrochemicals);
            selectedMetal = selectedPetrochemicals = selectedPrototype = -1;
        }
        if (selectedSchematic.usesExperimentation)
        {
            GUILayout.Label("Select a prototype. Each experiment has a 75% chance of +4% speed; failure is -5%.");
            if (GUILayout.Button("Experiment (1 point)"))
                inventory.RequestExperiment(selectedSchematic.schematicId, selectedPrototype);
            if (GUILayout.Button($"Finish {selectedSchematic.displayName}"))
            {
                inventory.RequestFinishCraft(selectedSchematic.schematicId, selectedPrototype);
                selectedPrototype = -1;
            }
        }
        if (GUILayout.Button("Close")) craftingOpen = false;
        GUILayout.EndArea();
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

        if (selectedInventorySlot >= inventory.Slots.Count) selectedInventorySlot = -1;
        Rect viewport = new Rect(InventoryPanel.x + 12f, InventoryPanel.y + 38f,
            InventoryPanel.width - 24f, InventoryPanel.height - 50f);
        Rect content = new Rect(0f, 0f, GridColumns * (SlotSize + SlotSpacing) - SlotSpacing,
            4f * (SlotSize + SlotSpacing) - SlotSpacing);
        Vector2 guiMousePosition = Event.current.mousePosition;
        inventoryScrollPosition = GUI.BeginScrollView(viewport, inventoryScrollPosition, content);
        for (int i = 0; i < PlayerInventory.MaximumSlots; i++)
        {
            int column = i % GridColumns;
            int row = i / GridColumns;
            Rect slotRect = new Rect(
                column * (SlotSize + SlotSpacing),
                row * (SlotSize + SlotSpacing),
                SlotSize,
                SlotSize);

            if (i < inventory.Slots.Count)
            {
                InventorySlot slot = inventory.GetSlot(i);
                string itemId = slot.ItemId.ToString();
                string label = $"{slot.ItemName}\nx{slot.Count}"
                    + (itemId == "radioactive"
                        ? $"\nOQ {slot.Quality}\nPE {slot.PotentialEnergy}\nCR {slot.ColdResistance}"
                            + $"\nDR {slot.DecayResistance}\nER {slot.EntangleResistance}"
                        : IsResourceItem(itemId) ? $"\nOQ {slot.Quality}\nCD {slot.Conductivity}"
                            : slot.Quality > 0 ? $"\nQ{slot.Quality}" : "")
                    + (itemId == "power_cube" ? $"\n{Mathf.Max(0f, Vehicle.MaximumEnergy - slot.EnergyUsed):0}% energy" : "")
                    + (itemId == "engine_cube" || itemId == "engine_prototype" ? $"\n{slot.Speed}%" : "");
                
                if (!radialMenuOpen && !isDraggingItem && viewport.Contains(guiMousePosition)
                    && slotRect.Contains(Event.current.mousePosition)
                    && Event.current.type == EventType.MouseDown)
                {
                    if (Event.current.button == 0)
                    {
                        if (selectedInventorySlot == i)
                            OpenUseMenu(i, guiMousePosition);
                        else
                        {
                            selectedInventorySlot = i;
                            isDraggingItem = true;
                            draggedSlotIndex = i;
                        }
                        Event.current.Use();
                    }
                }
                GUI.Box(slotRect, label, InventorySlotStyle);
                if (i == selectedInventorySlot) DrawSelectedSlotBorder(slotRect);
            }
            else
            {
                GUI.Box(slotRect, string.Empty, InventorySlotStyle);
            }
        }
        GUI.EndScrollView();
        
        // Draw dragged item at mouse position
        if (isDraggingItem && draggedSlotIndex >= 0 && draggedSlotIndex < inventory.Slots.Count)
        {
            InventorySlot draggedSlot = inventory.GetSlot(draggedSlotIndex);
            Vector2 mousePos = Event.current.mousePosition;
            GUI.Box(new Rect(mousePos.x - SlotSize * 0.5f, mousePos.y - SlotSize * 0.5f, SlotSize, SlotSize),
                $"{draggedSlot.ItemName}\nx{draggedSlot.Count}", InventorySlotStyle);
        }
    }

    internal static GUIStyle InventorySlotStyle
    {
        get
        {
            if (inventorySlotStyle == null || inventorySkin != GUI.skin)
            {
                inventorySkin = GUI.skin;
                inventorySlotStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 11,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip
                };
            }
            return inventorySlotStyle;
        }
    }

    internal static void DrawSelectedSlotBorder(Rect rect)
    {
        if (Event.current.type != EventType.Repaint) return;
        const float thickness = 3f;
        Color previousColor = GUI.color;
        GUI.color = Color.yellow;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.color = previousColor;
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

    private void TryOpenInteractionMenu(Transform target, Vector2 guiPosition)
    {
        CraftingTerminal terminal = target.GetComponent<CraftingTerminal>();
        if (terminal != null && terminal.isActiveAndEnabled
            && IsWithinItemInteractionRange(transform.position, target.position)
            && terminal.IsWithinRange(transform.position))
        {
            CloseRadialMenu();
            selectedTerminal = terminal;
            SelectSchematic("engine");
            craftingOpen = true;
            return;
        }
        Vehicle vehicle = target.GetComponent<Vehicle>();
        if (vehicle != null && vehicle.isActiveAndEnabled && vehicleInteraction != null
            && (!vehicle.IsOccupied || (vehicleInteraction.TryGetRiddenVehicle(out Vehicle ridden) && ridden == vehicle))
            && vehicle.IsWithinRange(transform.position, ItemInteractionRange))
        {
            CloseRadialMenu();
            vehicleInteraction.OpenMenu(vehicle, guiPosition);
            return;
        }

        WorldPickup pickup = target.GetComponent<WorldPickup>();
        GlowCube glowCube = target.GetComponent<GlowCube>();
        if (!IsWithinItemInteractionRange(transform.position, target.position)) return;
        if (pickup != null && pickup.CanBePickedUp)
        {
            selectedPickup = pickup;
            selectedGlowCube = null;
            menuContext = MenuContext.Pickup;
        }
        else if (glowCube != null && glowCube.CanToggle)
        {
            selectedPickup = null;
            selectedGlowCube = glowCube;
            menuContext = MenuContext.GlowCube;
        }
        else
        {
            return;
        }

        OpenRadialMenu(guiPosition);
    }

    private void SelectSchematic(string schematicId)
    {
        selectedSchematic = CraftingSchematic.LoadById(schematicId);
        if (selectedSchematic == null)
        {
            Debug.LogError($"Could not load the {schematicId} crafting schematic.", this);
            selectedSchematic = null;
        }

        selectedMetal = selectedPetrochemicals = selectedPrototype = -1;
    }

    private void SurveyResources()
    {
        if (inventory == null || !inventory.IsSurveyingToolActive) return;

        if (ResourceConcentrationGrid.Hotspots.Count == 0)
        {
            surveyMessage = "Survey: resource grid is not initialized.";
            return;
        }

        if (!ResourceConcentrationGrid.TryFindNearest(transform.position, 80f,
            out ResourceConcentrationGrid.Hotspot hotspot, out float distance))
        {
            surveyMessage = "Survey: no resource hotspots detected nearby.";
            return;
        }

        surveyMessage = $"Survey: {hotspot.DisplayName} {hotspot.Concentration}% at {distance:0}m"
            + $" | OQ {hotspot.OverallQuality}"
            + (hotspot.ResourceId == "radioactive"
                ? $" | PE {hotspot.PotentialEnergy} | CR {hotspot.ColdResistance}"
                    + $" | DR {hotspot.DecayResistance} | ER {hotspot.EntangleResistance}"
                : $" | CD {hotspot.Conductivity}");
    }

    private static bool IsResourceItem(string itemId)
    {
        return itemId == "metal" || itemId == "petrochemicals"
            || itemId == "energy" || itemId == "radioactive";
    }

    private bool IsWorldMenuTargetValid()
    {
        if (menuContext == MenuContext.Pickup)
            return selectedPickup != null && selectedPickup.CanBePickedUp
                && IsWithinItemInteractionRange(transform.position, selectedPickup.transform.position);
        if (menuContext == MenuContext.GlowCube)
            return selectedGlowCube != null && selectedGlowCube.CanToggle
                && IsWithinItemInteractionRange(transform.position, selectedGlowCube.transform.position);
        return false;
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

        if (index == 0)
        {
            if (menuContext == MenuContext.UseItem && inventory != null
                && selectedSlot >= 0 && selectedSlot < inventory.Slots.Count
                && inventory.GetSlot(selectedSlot).ItemId.ToString() == "surveying_tool")
            {
                return inventory.IsSurveyingToolActive ? "Deactivate" : "Activate";
            }
            return "Use";
        }
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
                if (!PlayerInventory.SameItem(slot, selectedItem)) continue;
                matchingStackCount++;
                totalCount += slot.Count;
            }

            if (matchingStackCount < 2) return false;

            int remainingCount = totalCount;
            for (int i = 0; i < inventory.Slots.Count; i++)
            {
                InventorySlot slot = inventory.GetSlot(i);
                if (!PlayerInventory.SameItem(slot, selectedItem)) continue;

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
        if (!IsWithinItemInteractionRange(transform.position, glowCube.transform.position)) return;

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
