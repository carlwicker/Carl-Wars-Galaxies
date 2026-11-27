using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class EngineCrafting
{
    public static bool TryAssemble(List<HostInventoryStore.SavedItem> items, CraftingSchematic recipe,
        int firstIndex, int secondIndex, bool success, out string message)
    {
        if (recipe == null || recipe.firstResourceCount <= 0 || recipe.secondResourceCount <= 0
            || string.IsNullOrWhiteSpace(recipe.firstResourceId) || string.IsNullOrWhiteSpace(recipe.secondResourceId)
            || recipe.firstResourceId == recipe.secondResourceId
            || recipe.schematicId == "engine" && (!recipe.usesExperimentation
                || recipe.prototypeId != "engine_prototype" || recipe.outputId != "engine_cube")
            || recipe.schematicId == "power" && (recipe.usesExperimentation
                || !string.IsNullOrEmpty(recipe.prototypeId) || recipe.outputId != "power_cube")
            || recipe.schematicId == "surveying_tool" && (recipe.usesExperimentation
                || !string.IsNullOrEmpty(recipe.prototypeId) || recipe.outputId != "surveying_tool")
            || recipe.schematicId != "engine" && recipe.schematicId != "power"
                && recipe.schematicId != "surveying_tool"
            || string.IsNullOrWhiteSpace(recipe.displayName)
            || Encoding.UTF8.GetByteCount(recipe.displayName) > 29
            || Encoding.UTF8.GetByteCount(recipe.firstResourceId) > 29
            || Encoding.UTF8.GetByteCount(recipe.secondResourceId) > 29
            || Encoding.UTF8.GetByteCount(recipe.outputId) > 29
            || recipe.usesExperimentation && (string.IsNullOrWhiteSpace(recipe.prototypeId)
                || Encoding.UTF8.GetByteCount(recipe.prototypeId) > 29))
        {
            message = "Crafting schematic is missing or invalid.";
            return false;
        }
        if (firstIndex < 0 || firstIndex >= items.Count || secondIndex < 0 || secondIndex >= items.Count
            || firstIndex == secondIndex)
        {
            message = "Select both resource stacks.";
            return false;
        }
        HostInventoryStore.SavedItem first = items[firstIndex];
        HostInventoryStore.SavedItem second = items[secondIndex];
        if (first.itemId != recipe.firstResourceId || second.itemId != recipe.secondResourceId
            || first.count < recipe.firstResourceCount || second.count < recipe.secondResourceCount
            || first.quality < 1 || first.quality > 1000 || second.quality < 1 || second.quality > 1000
            || first.conductivity < 0 || first.conductivity > 1000
            || second.conductivity < 0 || second.conductivity > 1000
            || first.speed < 0 || first.speed > 1000 || second.speed < 0 || second.speed > 1000)
        {
            message = "Select valid resource stacks with enough materials.";
            return false;
        }
        int remainingSlots = items.Count - (first.count == recipe.firstResourceCount ? 1 : 0)
            - (second.count == recipe.secondResourceCount ? 1 : 0);
        if (remainingSlots >= PlayerInventory.MaximumSlots)
        {
            message = "Your inventory needs a free slot for the prototype.";
            return false;
        }

        int quality = (first.quality * recipe.firstResourceCount + second.quality * recipe.secondResourceCount)
            / (recipe.firstResourceCount + recipe.secondResourceCount);
        int firstConductivity = first.conductivity > 0 ? first.conductivity : first.quality;
        int secondConductivity = second.conductivity > 0 ? second.conductivity : second.quality;
        int conductivity = (firstConductivity * recipe.firstResourceCount
            + secondConductivity * recipe.secondResourceCount)
            / (recipe.firstResourceCount + recipe.secondResourceCount);
        first.count -= recipe.firstResourceCount;
        second.count -= recipe.secondResourceCount;
        items.RemoveAll(item => item.count == 0);
        if (success)
        {
            items.Add(new HostInventoryStore.SavedItem
            {
                itemId = recipe.usesExperimentation ? recipe.prototypeId : recipe.outputId,
                itemName = recipe.usesExperimentation ? $"{recipe.displayName} Prototype" : recipe.displayName,
                count = 1,
                quality = quality,
                speed = recipe.usesExperimentation ? 60 + (quality + conductivity) * 30 / 1000 : 0,
                experimentPoints = recipe.usesExperimentation ? 5 : 0
            });
        }
        message = success ? recipe.usesExperimentation
            ? "Assembly succeeded. Experiment or finish your prototype."
            : $"{recipe.displayName} crafted."
            : "Assembly failed; the resources were consumed.";
        return true;
    }

    public static bool TryExperiment(HostInventoryStore.SavedItem prototype, string prototypeId, bool success)
    {
        if (prototype == null || string.IsNullOrWhiteSpace(prototypeId)
            || prototype.itemId != prototypeId || prototype.count != 1
            || prototype.experimentPoints <= 0) return false;
        prototype.speed = Mathf.Clamp(prototype.speed + (success ? 4 : -5), 50, 150);
        prototype.experimentPoints--;
        return true;
    }
}
