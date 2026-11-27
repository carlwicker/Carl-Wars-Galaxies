using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class VehicleCollisionTests
{
    private GameObject vehicleObject;
    private GameObject riderObject;
    private GameObject obstacle;
    private Component vehicle;
    private Component manager;
    private GameObject mountedPlayer;
    private Scene testScene;
    private Scene emptyScene;
    private CharacterController rider;
    private Type vehicleType;
    private readonly Vector3 origin = new Vector3(10000f, 0f, 10000f);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        vehicleType = Type.GetType("Vehicle, Assembly-CSharp");
        Assert.That(vehicleType, Is.Not.Null);
        vehicleObject = new GameObject("Test Vehicle");
        vehicleObject.transform.position = origin;
        vehicle = vehicleObject.AddComponent(vehicleType);
        riderObject = new GameObject("Test Rider");
        riderObject.transform.position = origin + Vector3.up;
        rider = riderObject.AddComponent<CharacterController>();
        rider.height = 2f;
        rider.center = Vector3.zero;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (manager != null)
            manager.GetType().GetMethod("Shutdown", new[] { typeof(bool) }).Invoke(manager, new object[] { false });
        UnityEngine.Object.Destroy(mountedPlayer);
        UnityEngine.Object.Destroy(obstacle);
        UnityEngine.Object.Destroy(riderObject);
        UnityEngine.Object.Destroy(vehicleObject);
        if (testScene.IsValid() && testScene.isLoaded) yield return SceneManager.UnloadSceneAsync(testScene);
        if (emptyScene.IsValid() && emptyScene.isLoaded) yield return SceneManager.UnloadSceneAsync(emptyScene);
        yield return null;
    }

    [UnityTest]
    public IEnumerator EnteringDoesNotRequirePowerOrEngine()
    {
        UnityEngine.Object.Destroy(vehicleObject);
        vehicleObject = null;
        Type managerType = Type.GetType("Unity.Netcode.NetworkManager, Unity.Netcode.Runtime");
        foreach (UnityEngine.Object existing in UnityEngine.Object.FindObjectsByType(managerType))
        {
            Component existingManager = (Component)existing;
            managerType.GetMethod("Shutdown", new[] { typeof(bool) }).Invoke(existingManager, new object[] { false });
            UnityEngine.Object.Destroy(existingManager.gameObject);
        }
        yield return null;
        if (SceneManager.GetSceneByName("SampleScene").isLoaded)
        {
            emptyScene = SceneManager.CreateScene("VehicleEntryTestSetup");
            Assert.That(SceneManager.SetActiveScene(emptyScene), Is.True);
            while (SceneManager.GetSceneByName("SampleScene").isLoaded)
                yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("SampleScene"));
        }
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Additive);
        yield return null;
        testScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
        Assert.That(SceneManager.SetActiveScene(testScene), Is.True);

        manager = UnityEngine.Object.FindAnyObjectByType(managerType) as Component;
        Assert.That(manager, Is.Not.Null);
        object config = managerType.GetField("NetworkConfig").GetValue(manager);
        config.GetType().GetField("PlayerPrefab").SetValue(config, null);
        Type transportType = Type.GetType(
            "Unity.Netcode.Transports.SinglePlayer.SinglePlayerTransport, Unity.Netcode.Runtime");
        config.GetType().GetField("NetworkTransport").SetValue(config, manager.gameObject.AddComponent(transportType));
        Assert.That((bool)managerType.GetMethod("StartHost").Invoke(manager, null), Is.True);
        yield return null;

        Component sceneVehicle = null;
        foreach (UnityEngine.Object candidate in UnityEngine.Object.FindObjectsByType(vehicleType))
        {
            Component component = (Component)candidate;
            if ((bool)vehicleType.GetProperty("IsReady").GetValue(component))
            {
                sceneVehicle = component;
                break;
            }
        }
        Assert.That(sceneVehicle, Is.Not.Null);
        ((Behaviour)sceneVehicle).enabled = false;
        mountedPlayer = new GameObject("Test Player");
        mountedPlayer.AddComponent(Type.GetType("Unity.Netcode.NetworkObject, Unity.Netcode.Runtime"));
        Component player = mountedPlayer.AddComponent(Type.GetType("PlayerInteraction, Assembly-CSharp"));
        MethodInfo mount = vehicleType.GetMethod("TryMount");
        MethodInfo setPower = vehicleType.GetMethod("TrySetPower");

        foreach (bool engine in new[] { false, true })
        foreach (bool power in new[] { false, true })
        {
            SetNetworkValue(sceneVehicle, "engineInstalled", engine);
            SetNetworkValue(sceneVehicle, "powerInstalled", power);
            SetNetworkValue(sceneVehicle, "poweredOn", false);
            if (!engine || !power)
                Assert.That((bool)setPower.Invoke(sceneVehicle, new object[] { true, 0UL }), Is.False);
            Assert.That((bool)mount.Invoke(sceneVehicle, new object[] { player }), Is.True,
                $"Entry failed with engine={engine}, power={power}.");
            Assert.That((bool)mount.Invoke(sceneVehicle, new object[] { player }), Is.False,
                "An occupied vehicle cannot be entered again.");
            if (!engine || !power)
                Assert.That((bool)setPower.Invoke(sceneVehicle, new object[] { true, 0UL }), Is.False,
                    "The rider cannot turn on a vehicle without both cubes.");
            Assert.That((bool)setPower.Invoke(sceneVehicle, new object[] { true, 1UL }), Is.False,
                "Another player cannot turn on an occupied vehicle.");
            SetNetworkValue(sceneVehicle, "riderId", ulong.MaxValue);
        }

        SetNetworkValue(sceneVehicle, "poweredOn", true);
        Assert.That((bool)mount.Invoke(sceneVehicle, new object[] { player }), Is.True,
            "A powered vehicle should remain enterable.");
        Assert.That((bool)setPower.Invoke(sceneVehicle, new object[] { false, 1UL }), Is.False,
            "Another player cannot turn off an occupied vehicle.");
        SetNetworkValue(sceneVehicle, "riderId", ulong.MaxValue);
    }

    [Test]
    public void RiderHeightTracksActualHoverHeight()
    {
        PropertyInfo height = vehicleType.GetProperty("RiderHeight");
        Assert.That((float)height.GetValue(vehicle), Is.EqualTo(1f));
        vehicleType.GetField("currentHoverHeight", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(vehicle, 0.75f);
        Assert.That((float)height.GetValue(vehicle), Is.EqualTo(1.75f));
    }

    [Test]
    public void PowerCubeDrainsOnlyWhenOnAndMoreWhenDriving()
    {
        MethodInfo consume = vehicleType.GetMethod("ConsumeEnergy", BindingFlags.Instance | BindingFlags.NonPublic);
        SetNetworkValue(vehicle, "powerInstalled", true);
        consume.Invoke(vehicle, new object[] { 10f, 100f });
        Assert.That((float)vehicleType.GetProperty("EnergyRemaining").GetValue(vehicle), Is.EqualTo(100f));

        SetNetworkValue(vehicle, "poweredOn", true);
        consume.Invoke(vehicle, new object[] { 10f, 0f });
        Assert.That((float)vehicleType.GetProperty("EnergyRemaining").GetValue(vehicle), Is.EqualTo(99.5f).Within(0.001f));
        consume.Invoke(vehicle, new object[] { 10f, 100f });
        Assert.That((float)vehicleType.GetProperty("EnergyRemaining").GetValue(vehicle), Is.EqualTo(98.5f).Within(0.001f));
    }

    [Test]
    public void LargerVehicleHasMoreMassAndConsumesEnergyFaster()
    {
        bool configured = (bool)vehicleType.GetMethod("ConfigureFromAsset").Invoke(vehicle,
            new object[] { "test", "Test", new Vector3Int(1, 3, 5), 0, Color.white, 3f, 2f });
        Assert.That(configured, Is.True);
        Assert.That((float)vehicleType.GetProperty("Mass").GetValue(vehicle), Is.EqualTo(15f));
        Assert.That(vehicleObject.GetComponentInChildren<Rigidbody>().mass, Is.EqualTo(15f));

        SetNetworkValue(vehicle, "powerInstalled", true);
        SetNetworkValue(vehicle, "poweredOn", true);
        vehicleType.GetMethod("ConsumeEnergy", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(vehicle, new object[] { 10f, 100f });
        Assert.That((float)vehicleType.GetProperty("EnergyRemaining").GetValue(vehicle),
            Is.EqualTo(96.25f).Within(0.001f));
    }

    [Test]
    public void EmptyCubeShutsDownAndSurvivesVehicleSaveRoundTrip()
    {
        SetNetworkValue(vehicle, "powerInstalled", true);
        SetNetworkValue(vehicle, "powerEnergyUsed", 99.9f);
        SetNetworkValue(vehicle, "poweredOn", true);
        vehicleType.GetMethod("ConsumeEnergy", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(vehicle, new object[] { 10f, 0f });
        Assert.That((bool)vehicleType.GetProperty("IsOn").GetValue(vehicle), Is.False);
        Assert.That((bool)vehicleType.GetProperty("HasPower").GetValue(vehicle), Is.True);
        Assert.That((float)vehicleType.GetProperty("EnergyRemaining").GetValue(vehicle), Is.Zero);

        object save = vehicleType.GetMethod("CreateSave").Invoke(vehicle, null);
        string json = JsonUtility.ToJson(save);
        object restored = JsonUtility.FromJson(json, save.GetType());
        SetNetworkValue(vehicle, "powerEnergyUsed", 0f);
        vehicleType.GetMethod("ApplySave").Invoke(vehicle, new[] { restored });
        Assert.That((float)vehicleType.GetProperty("EnergyRemaining").GetValue(vehicle), Is.Zero);
        Assert.That((bool)vehicleType.GetProperty("HasPower").GetValue(vehicle), Is.True);

        object legacy = JsonUtility.FromJson("{\"powerInstalled\":true}", save.GetType());
        vehicleType.GetMethod("ApplySave").Invoke(vehicle, new[] { legacy });
        Assert.That((float)vehicleType.GetProperty("EnergyRemaining").GetValue(vehicle), Is.EqualTo(100f));
    }

    [Test]
    public void CubeChargeIsPreservedByItemCloningAndDistinguishesStacks()
    {
        Type itemType = Type.GetType("HostInventoryStore+SavedItem, Assembly-CSharp");
        Type storeType = Type.GetType("HostInventoryStore, Assembly-CSharp");
        IList items = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(itemType));
        object item = Activator.CreateInstance(itemType);
        itemType.GetField("itemId").SetValue(item, "power_cube");
        itemType.GetField("energyUsed").SetValue(item, 75f);
        items.Add(item);
        IList clones = (IList)storeType.GetMethod("CloneItems", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { items });
        Assert.That((float)itemType.GetField("energyUsed").GetValue(clones[0]), Is.EqualTo(75f));

        object full = Activator.CreateInstance(itemType);
        itemType.GetField("itemId").SetValue(full, "power_cube");
        Type inventoryType = Type.GetType("PlayerInventory, Assembly-CSharp");
        MethodInfo sameItem = inventoryType.GetMethod("SameItem", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { itemType, itemType }, null);
        Assert.That((bool)sameItem.Invoke(null, new[] { item, full }), Is.False);
    }

    [Test]
    public void CubeChargeSurvivesPlayerInventoryAndDroppedPickupData()
    {
        Type itemType = Type.GetType("HostInventoryStore+SavedItem, Assembly-CSharp");
        Type inventoryType = Type.GetType("PlayerInventory, Assembly-CSharp");
        Type pickupType = Type.GetType("WorldPickup, Assembly-CSharp");
        IList items = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(itemType));
        object item = Activator.CreateInstance(itemType);
        itemType.GetField("itemId").SetValue(item, "power_cube");
        itemType.GetField("itemName").SetValue(item, "Power Cube");
        itemType.GetField("count").SetValue(item, 1);
        itemType.GetField("energyUsed").SetValue(item, 42f);
        items.Add(item);

        GameObject player = new GameObject("Inventory Test");
        GameObject pickup = new GameObject("Pickup Test");
        try
        {
            Component inventory = player.AddComponent(inventoryType);
            inventoryType.GetMethod("ApplySavedItems", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(inventory, new object[] { items });
            IList saved = (IList)inventoryType.GetMethod("CreateSavedItems", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(inventory, null);
            Assert.That((float)itemType.GetField("energyUsed").GetValue(saved[0]), Is.EqualTo(42f));

            Component worldPickup = pickup.AddComponent(pickupType);
            pickupType.GetMethod("Initialize").Invoke(worldPickup,
                new object[] { "drop-test", "power_cube", "Power Cube", 1, 0, 0, 0, 0, 42f, 0, 0, 0, 0 });
            Assert.That((float)pickupType.GetProperty("EnergyUsed").GetValue(worldPickup), Is.EqualTo(42f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(pickup);
        }
    }

    [Test]
    public void RadioactiveStatsSurviveInventoryAndPickupRoundTrip()
    {
        Type itemType = Type.GetType("HostInventoryStore+SavedItem, Assembly-CSharp");
        Type inventoryType = Type.GetType("PlayerInventory, Assembly-CSharp");
        Type pickupType = Type.GetType("WorldPickup, Assembly-CSharp");
        Assert.That(itemType, Is.Not.Null);
        Assert.That(inventoryType, Is.Not.Null);
        Assert.That(pickupType, Is.Not.Null);

        object item = Activator.CreateInstance(itemType);
        itemType.GetField("itemId").SetValue(item, "radioactive");
        itemType.GetField("itemName").SetValue(item, "Radioactive");
        itemType.GetField("count").SetValue(item, 5);
        itemType.GetField("quality").SetValue(item, 800);
        itemType.GetField("potentialEnergy").SetValue(item, 901);
        itemType.GetField("coldResistance").SetValue(item, 902);
        itemType.GetField("decayResistance").SetValue(item, 903);
        itemType.GetField("entangleResistance").SetValue(item, 904);
        IList items = (IList)Activator.CreateInstance(
            typeof(System.Collections.Generic.List<>).MakeGenericType(itemType));
        items.Add(item);

        GameObject player = new GameObject("Radioactive Inventory Test");
        GameObject pickup = new GameObject("Radioactive Pickup Test");
        try
        {
            Component inventory = player.AddComponent(inventoryType);
            inventoryType.GetMethod("ApplySavedItems", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(inventory, new object[] { items });
            IList saved = (IList)inventoryType.GetMethod("CreateSavedItems", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(inventory, null);
            Assert.That((int)itemType.GetField("potentialEnergy").GetValue(saved[0]), Is.EqualTo(901));
            Assert.That((int)itemType.GetField("coldResistance").GetValue(saved[0]), Is.EqualTo(902));
            Assert.That((int)itemType.GetField("decayResistance").GetValue(saved[0]), Is.EqualTo(903));
            Assert.That((int)itemType.GetField("entangleResistance").GetValue(saved[0]), Is.EqualTo(904));

            Component worldPickup = pickup.AddComponent(pickupType);
            pickupType.GetMethod("Initialize").Invoke(worldPickup,
                new object[] { "radioactive-test", "radioactive", "Radioactive", 5, 800, 0, 0, 0, 0f, 901, 902, 903, 904 });
            Assert.That((int)pickupType.GetProperty("PotentialEnergy").GetValue(worldPickup), Is.EqualTo(901));
            Assert.That((int)pickupType.GetProperty("ColdResistance").GetValue(worldPickup), Is.EqualTo(902));
            Assert.That((int)pickupType.GetProperty("DecayResistance").GetValue(worldPickup), Is.EqualTo(903));
            Assert.That((int)pickupType.GetProperty("EntangleResistance").GetValue(worldPickup), Is.EqualTo(904));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(pickup);
        }
    }

    [UnityTest]
    public IEnumerator PlayerCannotWalkThroughParkedVehicle()
    {
        riderObject.transform.position = origin + new Vector3(0f, 1f, -4f);
        Physics.SyncTransforms();
        yield return null;

        rider.Move(Vector3.forward * 3f);
        Assert.That(riderObject.transform.position.z - origin.z, Is.LessThan(-2.3f));
    }

    [UnityTest]
    public IEnumerator FullVehicleBodyStopsAtObstacleButIgnoresTriggers()
    {
        obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.transform.position = origin + new Vector3(0f, 0.5f, 3f);
        obstacle.transform.localScale = new Vector3(3f, 3f, 0.1f);
        Collider obstacleCollider = obstacle.GetComponent<Collider>();
        obstacleCollider.isTrigger = true;
        yield return null;

        Vector3 requested = Vector3.forward * 3f;
        Vector3 withoutSolidObstacle = ConstrainMovement(requested);
        Assert.That(withoutSolidObstacle.z, Is.EqualTo(3f).Within(0.01f));

        obstacleCollider.isTrigger = false;
        Physics.SyncTransforms();
        Vector3 stopped = ConstrainMovement(requested);
        Assert.That(stopped.z, Is.InRange(0.8f, 0.96f));
    }

    [UnityTest]
    public IEnumerator RotationDoesNotSwingVehicleThroughObstacle()
    {
        obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.transform.position = origin + new Vector3(1.5f, 0.5f, 0f);
        obstacle.transform.localScale = new Vector3(0.1f, 3f, 2f);
        yield return null;

        Quaternion result = (Quaternion)vehicleType.GetMethod("ConstrainRiderRotation")
            .Invoke(vehicle, new object[] { rider, Quaternion.Euler(0f, 90f, 0f) });
        Assert.That(Quaternion.Angle(Quaternion.identity, result), Is.LessThan(89f));
    }

    [UnityTest]
    public IEnumerator WideVehicleUsesItsConfiguredDimensions()
    {
        bool configured = (bool)vehicleType.GetMethod("ConfigureFromAsset").Invoke(vehicle,
            new object[] { "test", "Test", new Vector3Int(5, 1, 4), 0, Color.white, 3f, 2f });
        Assert.That(configured, Is.True);
        obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.transform.position = origin + new Vector3(2f, 0.5f, 3f);
        obstacle.transform.localScale = new Vector3(0.5f, 3f, 0.1f);
        yield return null;

        Assert.That(ConstrainMovement(Vector3.forward * 3f).z, Is.InRange(0.8f, 0.96f));
    }

    [UnityTest]
    public IEnumerator VehicleUsesOneRectangularBodyAtConfiguredDimensions()
    {
        Vector3Int dimensions = new Vector3Int(5, 15, 5);
        bool configured = (bool)vehicleType.GetMethod("ConfigureFromAsset").Invoke(vehicle,
            new object[] { "test", "Test", dimensions, 0, Color.white, 3f, 2f });
        Assert.That(configured, Is.True);
        yield return null;

        Transform visuals = vehicleObject.transform.Find("Vehicle Geometry/Vehicle Visuals");
        Assert.That(visuals.childCount, Is.EqualTo(2));
        Transform body = visuals.Find("Vehicle Body");
        Assert.That(body, Is.Not.Null);
        Assert.That(body.localScale, Is.EqualTo((Vector3)dimensions));
        Assert.That(body.localPosition, Is.EqualTo(new Vector3(0f, dimensions.y * 0.5f, 0f)));
    }

    private Vector3 ConstrainMovement(Vector3 movement)
    {
        return (Vector3)vehicleType.GetMethod("ConstrainRiderMovement")
            .Invoke(vehicle, new object[] { rider, movement });
    }

    private void SetNetworkValue(Component target, string field, object value)
    {
        object variable = vehicleType.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        variable.GetType().GetProperty("Value").SetValue(variable, value);
    }
}
