using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class EngineCraftingTests
{
    private Type itemType;
    private Type rulesType;
    private Type gridType;
    private ScriptableObject recipe;

    [SetUp]
    public void SetUp()
    {
        itemType = Type.GetType("HostInventoryStore+SavedItem, Assembly-CSharp");
        rulesType = Type.GetType("EngineCrafting, Assembly-CSharp");
        gridType = Type.GetType("ResourceConcentrationGrid, Assembly-CSharp");
        Type recipeType = Type.GetType("CraftingSchematic, Assembly-CSharp");
        Assert.That(itemType, Is.Not.Null);
        Assert.That(rulesType, Is.Not.Null);
        Assert.That(gridType, Is.Not.Null);
        Assert.That(recipeType, Is.Not.Null);
        recipe = ScriptableObject.CreateInstance(recipeType);
    }

    public class CraftingSceneTests
    {
        [UnityTest]
        public IEnumerator SampleSceneProvidesTerminalAndTestResources()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Additive);
            yield return null;

            Type terminalType = Type.GetType("CraftingTerminal, Assembly-CSharp");
            Type pickupType = Type.GetType("WorldPickup, Assembly-CSharp");
            Type schematicType = Type.GetType("CraftingSchematic, Assembly-CSharp");
            Assert.That(terminalType, Is.Not.Null);
            Assert.That(pickupType, Is.Not.Null);
            Assert.That(schematicType, Is.Not.Null);
            Assert.That(UnityEngine.Object.FindObjectsByType(terminalType, FindObjectsInactive.Exclude).Length, Is.EqualTo(1));
            Assert.That(Resources.Load("EngineSchematic", schematicType), Is.Not.Null);
            Assert.That(Resources.Load("PowerSchematic", schematicType), Is.Not.Null);
            Assert.That(Resources.Load("SurveyingToolSchematic", schematicType), Is.Not.Null);

            int metal = 0;
            int petrochemicals = 0;
            int energy = 0;
            int radioactive = 0;
            int engineCubes = 0;
            foreach (UnityEngine.Object pickup in UnityEngine.Object.FindObjectsByType(pickupType, FindObjectsInactive.Exclude))
            {
                string itemId = (string)pickupType.GetProperty("ItemId").GetValue(pickup);
                int quality = (int)pickupType.GetProperty("Quality").GetValue(pickup);
                int conductivity = (int)pickupType.GetProperty("Conductivity").GetValue(pickup);
                if (itemId == "engine_cube") engineCubes++;
                if (itemId == "metal") { metal++; Assert.That(quality, Is.InRange(100, 1000)); }
                if (itemId == "petrochemicals") { petrochemicals++; Assert.That(quality, Is.InRange(100, 1000)); }
                if (itemId == "energy") { energy++; Assert.That(quality, Is.InRange(100, 1000)); }
                if (itemId == "radioactive")
                {
                    radioactive++;
                    Assert.That(quality, Is.InRange(100, 1000));
                    foreach (string stat in new[] { "PotentialEnergy", "ColdResistance", "DecayResistance", "EntangleResistance" })
                        Assert.That((int)pickupType.GetProperty(stat).GetValue(pickup), Is.InRange(100, 1000));
                }
                if (itemId == "metal" || itemId == "petrochemicals" || itemId == "energy")
                    Assert.That(conductivity, Is.InRange(100, 1000));
            }
            Assert.That(metal, Is.EqualTo(6));
            Assert.That(petrochemicals, Is.EqualTo(6));
            Assert.That(energy, Is.EqualTo(6));
            Assert.That(radioactive, Is.EqualTo(6));
            Assert.That(engineCubes, Is.Zero);
            yield return SceneManager.UnloadSceneAsync("SampleScene");
        }
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(recipe);
    }

    [Test]
    public void AssemblyConsumesExactIngredientsAndCreatesQualityBasedPrototype()
    {
        IList items = Items(Item("metal", 6, 800), Item("petrochemicals", 3, 400));
        Assert.That(Assemble(items, true), Is.True);
        Assert.That(items.Count, Is.EqualTo(2));
        Assert.That(Get<int>(items[0], "count"), Is.EqualTo(1));
        Assert.That(Get<string>(items[1], "itemId"), Is.EqualTo("engine_prototype"));
        Assert.That(Get<int>(items[1], "quality"), Is.EqualTo(650));
        Assert.That(Get<int>(items[1], "speed"), Is.EqualTo(99));
        Assert.That(Get<int>(items[1], "experimentPoints"), Is.EqualTo(5));
    }

    [Test]
    public void EngineSpeedUsesResourceConductivityAlongsideOverallQuality()
    {
        IList items = Items(Item("metal", 5, 800, 300), Item("petrochemicals", 3, 400, 800));
        Assert.That(Assemble(items, true), Is.True);
        Assert.That(Get<int>(items[1], "quality"), Is.EqualTo(650));
        Assert.That(Get<int>(items[1], "speed"), Is.EqualTo(94));
    }

    [Test]
    public void PowerSchematicProducesPowerCubeFromFiveMetalAndFiveRadioactive()
    {
        object schematic = recipe.GetType().GetMethod("LoadById").Invoke(null, new object[] { "power" });
        Assert.That(schematic, Is.Not.Null);
        IList items = Items(Item("metal", 6, 800, 700), Item("radioactive", 6, 500));
        object[] args = { items, schematic, 0, 1, true, null };
        Assert.That(rulesType.GetMethod("TryAssemble").Invoke(null, args), Is.True);
        Assert.That(items.Count, Is.EqualTo(3));
        Assert.That(Get<int>(items[0], "count"), Is.EqualTo(1));
        Assert.That(Get<int>(items[1], "count"), Is.EqualTo(1));
        Assert.That(Get<string>(items[2], "itemId"), Is.EqualTo("power_cube"));
        Assert.That(Get<string>(items[2], "itemName"), Is.EqualTo("Power Cube"));
        Assert.That(Get<int>(items[2], "quality"), Is.EqualTo(650));
        Assert.That(Get<int>(items[2], "experimentPoints"), Is.Zero);
    }

    [Test]
    public void SurveyingToolSchematicCraftsAToolFromFiveMetalAndOnePowerCube()
    {
        object schematic = recipe.GetType().GetMethod("LoadById").Invoke(null, new object[] { "surveying_tool" });
        Assert.That(schematic, Is.Not.Null);
        IList items = Items(Item("metal", 7, 800), Item("power_cube", 2, 500));
        object[] args = { items, schematic, 0, 1, true, null };
        Assert.That(rulesType.GetMethod("TryAssemble").Invoke(null, args), Is.True);
        Assert.That(items.Count, Is.EqualTo(3));
        Assert.That(Get<int>(items[0], "count"), Is.EqualTo(2));
        Assert.That(Get<int>(items[1], "count"), Is.EqualTo(1));
        Assert.That(Get<string>(items[2], "itemId"), Is.EqualTo("surveying_tool"));
        Assert.That(Get<string>(items[2], "itemName"), Is.EqualTo("Surveying Tool"));
    }

    [TestCase("engine")]
    [TestCase("power")]
    [TestCase("surveying_tool")]
    public void SchematicIdLoadsItsMatchingResource(string id)
    {
        object schematic = recipe.GetType().GetMethod("LoadById").Invoke(null, new object[] { id });
        Assert.That(schematic, Is.Not.Null);
        Assert.That(recipe.GetType().GetField("schematicId").GetValue(schematic), Is.EqualTo(id));
    }

    [Test]
    public void ResourceGridIsSeededAndSurveyReturnsTheNearestHotspot()
    {
        var generate = gridType.GetMethod("Generate");
        object[] generationArgs = { 1234, new Vector2(-20f, -15f), new Vector2(20f, 30f), 3 };
        generate.Invoke(null, generationArgs);
        IList hotspots = (IList)gridType.GetProperty("Hotspots").GetValue(null);
        Assert.That(hotspots.Count, Is.EqualTo(12));
        object nearest = hotspots[0];
        Type hotspotType = nearest.GetType();
        Vector3 position = (Vector3)hotspotType.GetProperty("Position").GetValue(nearest);
        string expectedResourceId = (string)hotspotType.GetProperty("ResourceId").GetValue(nearest);

        object[] surveyArgs = { position, 1f, null, null };
        Assert.That(gridType.GetMethod("TryFindNearest").Invoke(null, surveyArgs), Is.True);
        Assert.That((float)surveyArgs[3], Is.Zero);
        Assert.That(hotspotType.GetProperty("ResourceId").GetValue(surveyArgs[2]), Is.EqualTo(expectedResourceId));

        bool foundRadioactive = false;
        foreach (object hotspot in hotspots)
        {
            if ((string)hotspotType.GetProperty("ResourceId").GetValue(hotspot) != "radioactive") continue;
            foundRadioactive = true;
            foreach (string stat in new[] { "PotentialEnergy", "ColdResistance", "DecayResistance", "EntangleResistance" })
                Assert.That((int)hotspotType.GetProperty(stat).GetValue(hotspot), Is.InRange(100, 1000));
        }
        Assert.That(foundRadioactive, Is.True);
        object[] outOfRangeArgs = { new Vector3(1000f, 0f, 1000f), 1f, null, null };
        Assert.That(gridType.GetMethod("TryFindNearest").Invoke(null, outOfRangeArgs), Is.False);

        object[] sameSeedArgs = { 1234, new Vector2(-20f, -15f), new Vector2(20f, 30f), 3 };
        generate.Invoke(null, sameSeedArgs);
        IList sameSeedHotspots = (IList)gridType.GetProperty("Hotspots").GetValue(null);
        Assert.That(hotspotType.GetProperty("Position").GetValue(sameSeedHotspots[0]), Is.EqualTo(position));

        object[] shiftedSeedArgs = { 4321, new Vector2(-20f, -15f), new Vector2(20f, 30f), 3 };
        generate.Invoke(null, shiftedSeedArgs);
        IList shiftedHotspots = (IList)gridType.GetProperty("Hotspots").GetValue(null);
        Assert.That(hotspotType.GetProperty("Position").GetValue(shiftedHotspots[0]), Is.Not.EqualTo(position));
    }

    [Test]
    public void FailedAssemblyConsumesResourcesButInvalidInputDoesNot()
    {
        IList failed = Items(Item("metal", 5, 800), Item("petrochemicals", 3, 400));
        Assert.That(Assemble(failed, false), Is.True);
        Assert.That(failed.Count, Is.Zero);

        IList invalid = Items(Item("metal", 4, 800), Item("petrochemicals", 3, 400));
        Assert.That(Assemble(invalid, true), Is.False);
        Assert.That(Get<int>(invalid[0], "count"), Is.EqualTo(4));
        Assert.That(Get<int>(invalid[1], "count"), Is.EqualTo(3));

        IList invalidConductivity = Items(Item("metal", 5, 800, 1001), Item("petrochemicals", 3, 400));
        Assert.That(Assemble(invalidConductivity, true), Is.False);
        Assert.That(Get<int>(invalidConductivity[0], "count"), Is.EqualTo(5));
    }

    [Test]
    public void FullInventoryDoesNotConsumeResources()
    {
        IList items = Items(Item("metal", 6, 800), Item("petrochemicals", 4, 400));
        while (items.Count < 24) items.Add(Item("other", 1, 0));
        Assert.That(Assemble(items, true), Is.False);
        Assert.That(Get<int>(items[0], "count"), Is.EqualTo(6));
        Assert.That(Get<int>(items[1], "count"), Is.EqualTo(4));
    }

    [Test]
    public void SavedItemClonePreservesCraftingStats()
    {
        IList items = Items(Item("metal", 5, 800), Item("petrochemicals", 3, 400));
        Assert.That(Assemble(items, true), Is.True);
        Type storeType = Type.GetType("HostInventoryStore, Assembly-CSharp");
        var cloneMethod = storeType.GetMethod("CloneItems",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        IList cloned = (IList)cloneMethod.Invoke(null, new object[] { items });
        Assert.That(Get<int>(cloned[0], "quality"), Is.EqualTo(650));
        Assert.That(Get<int>(cloned[0], "speed"), Is.EqualTo(99));
        Assert.That(Get<int>(cloned[0], "experimentPoints"), Is.EqualTo(5));
    }

    [Test]
    public void SavedItemClonePreservesResourceConductivity()
    {
        IList items = Items(Item("metal", 5, 800, 875));
        Type storeType = Type.GetType("HostInventoryStore, Assembly-CSharp");
        var cloneMethod = storeType.GetMethod("CloneItems",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        IList cloned = (IList)cloneMethod.Invoke(null, new object[] { items });
        Assert.That(Get<int>(cloned[0], "conductivity"), Is.EqualTo(875));
    }

    [Test]
    public void ExperimentsConsumePointsAndChangeSpeedInBothDirections()
    {
        IList items = Items(Item("metal", 5, 800), Item("petrochemicals", 3, 400));
        Assert.That(Assemble(items, true), Is.True);
        object prototype = items[0];
        var experiment = rulesType.GetMethod("TryExperiment");
        Assert.That(experiment.Invoke(null, new[] { prototype, "engine_prototype", (object)true }), Is.True);
        Assert.That(experiment.Invoke(null, new[] { prototype, "engine_prototype", (object)false }), Is.True);
        Assert.That(Get<int>(prototype, "speed"), Is.EqualTo(98));
        Assert.That(Get<int>(prototype, "experimentPoints"), Is.EqualTo(3));
        experiment.Invoke(null, new[] { prototype, "engine_prototype", (object)true });
        experiment.Invoke(null, new[] { prototype, "engine_prototype", (object)true });
        experiment.Invoke(null, new[] { prototype, "engine_prototype", (object)true });
        Assert.That(experiment.Invoke(null, new[] { prototype, "engine_prototype", (object)true }), Is.False);
    }

    private bool Assemble(IList items, bool success)
    {
        object[] args = { items, recipe, 0, 1, success, null };
        return (bool)rulesType.GetMethod("TryAssemble").Invoke(null, args);
    }

    private object Item(string id, int count, int quality, int conductivity = 0)
    {
        object item = Activator.CreateInstance(itemType);
        itemType.GetField("itemId").SetValue(item, id);
        itemType.GetField("itemName").SetValue(item, id);
        itemType.GetField("count").SetValue(item, count);
        itemType.GetField("quality").SetValue(item, quality);
        itemType.GetField("conductivity").SetValue(item, conductivity);
        return item;
    }

    private void Set<T>(string field, T value)
    {
        recipe.GetType().GetField(field).SetValue(recipe, value);
    }

    private IList Items(params object[] entries)
    {
        IList items = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
        foreach (object entry in entries) items.Add(entry);
        return items;
    }

    private T Get<T>(object item, string field)
    {
        return (T)itemType.GetField(field).GetValue(item);
    }
}
