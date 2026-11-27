using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class CombatTests
{
    private GameObject player;
    private GameObject obstacle;
    private Component manager;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (manager != null)
            manager.GetType().GetMethod("Shutdown", new[] { typeof(bool) }).Invoke(manager, new object[] { false });
        if (player != null) UnityEngine.Object.Destroy(player);
        if (obstacle != null) UnityEngine.Object.Destroy(obstacle);
        Scene scene = SceneManager.GetSceneByName("SampleScene");
        if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        yield return null;
    }

    [UnityTest]
    public IEnumerator HostValidatesCombatCostsRangeCooldownAndDummyRespawn()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Additive);
        yield return null;
        Assert.That(SceneManager.SetActiveScene(SceneManager.GetSceneByName("SampleScene")), Is.True);

        Type managerType = Type.GetType("Unity.Netcode.NetworkManager, Unity.Netcode.Runtime");
        Type networkObjectType = Type.GetType("Unity.Netcode.NetworkObject, Unity.Netcode.Runtime");
        Type referenceType = Type.GetType("Unity.Netcode.NetworkObjectReference, Unity.Netcode.Runtime");
        Type dummyType = Type.GetType("CombatDummy, Assembly-CSharp");
        Type combatType = Type.GetType("PlayerCombat, Assembly-CSharp");
        Type hamType = Type.GetType("PlayerHAM, Assembly-CSharp");
        Assert.That(managerType, Is.Not.Null);
        manager = UnityEngine.Object.FindAnyObjectByType(managerType) as Component;
        Assert.That(manager, Is.Not.Null);
        object config = managerType.GetField("NetworkConfig").GetValue(manager);
        GameObject playerPrefab = (GameObject)config.GetType().GetField("PlayerPrefab").GetValue(config);
        Assert.That(playerPrefab, Is.Not.Null);
        config.GetType().GetField("PlayerPrefab").SetValue(config, null);
        Type transportType = Type.GetType(
            "Unity.Netcode.Transports.SinglePlayer.SinglePlayerTransport, Unity.Netcode.Runtime");
        config.GetType().GetField("NetworkTransport").SetValue(config, manager.gameObject.AddComponent(transportType));
        Assert.That((bool)managerType.GetMethod("StartHost").Invoke(manager, null), Is.True);
        yield return null;

        Component dummy = UnityEngine.Object.FindAnyObjectByType(dummyType) as Component;
        Assert.That(dummy, Is.Not.Null);
        Assert.That((bool)dummyType.GetProperty("IsSpawned").GetValue(dummy), Is.True);
        dummy.transform.position = new Vector3(10000f, 50f, 10005f);
        player = UnityEngine.Object.Instantiate(playerPrefab);
        UnityEngine.Object.DestroyImmediate(player.GetComponent(Type.GetType("VehicleInteraction, Assembly-CSharp")));
        UnityEngine.Object.DestroyImmediate(player.GetComponent(Type.GetType("PlayerInventory, Assembly-CSharp")));
        ((Behaviour)player.GetComponent(Type.GetType("PlayerMovement, Assembly-CSharp"))).enabled = false;
        player.transform.position = new Vector3(10000f, 50f, 10000f);
        Component netObject = player.GetComponent(networkObjectType);
        Component ham = player.GetComponent(hamType);
        Component combat = player.GetComponent(combatType);
        networkObjectType.GetMethod("SpawnAsPlayerObject").Invoke(netObject, new object[] { 0UL, true });
        Physics.SyncTransforms();
        yield return null;

        Component dummyNetwork = dummy.GetComponent(networkObjectType);
        object target = Activator.CreateInstance(referenceType, new object[] { dummyNetwork });
        object[] referenceArgs = { null, manager };
        Assert.That((bool)referenceType.GetMethod("TryGet").Invoke(target, referenceArgs), Is.True);
        Assert.That(referenceArgs[0], Is.SameAs(dummyNetwork));
        MethodInfo useAction = combatType.GetMethod("UseActionServerRpc", BindingFlags.Instance | BindingFlags.NonPublic);
        useAction.Invoke(combat, new[] { (object)1, target });
        yield return null;
        string status = (string)combatType.GetField("status", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(combat);
        Assert.That((float)dummyType.GetProperty("Health").GetValue(dummy), Is.EqualTo(75f), status);
        Assert.That((float)hamType.GetProperty("Action").GetValue(ham), Is.EqualTo(86f).Within(1f));
        Assert.That(GameObject.Find("Combat Hit Sparks"), Is.Not.Null,
            "A successful hit should display sparks at the target.");
        Assert.That(GameObject.Find("Pistol Muzzle Flash"), Is.Not.Null,
            "A successful pistol shot should display a muzzle flash.");
        useAction.Invoke(combat, new[] { (object)1, target });
        yield return null;
        Assert.That((float)dummyType.GetProperty("Health").GetValue(dummy), Is.EqualTo(75f));

        player.transform.position = dummy.transform.position + Vector3.right * 11f;
        useAction.Invoke(combat, new[] { (object)0, target });
        yield return null;
        Assert.That((float)dummyType.GetProperty("Health").GetValue(dummy), Is.EqualTo(75f));

        player.transform.position = new Vector3(10000f, 50f, 10000f);
        object cooldown = combatType.GetField("nextActionTime", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(combat);
        PropertyInfo cooldownValue = cooldown.GetType().GetProperty("Value");
        cooldownValue.SetValue(cooldown, 0d);
        obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.transform.position = new Vector3(10000f, 51f, 10002.5f);
        Physics.SyncTransforms();
        useAction.Invoke(combat, new[] { (object)0, target });
        yield return null;
        Assert.That((float)dummyType.GetProperty("Health").GetValue(dummy), Is.EqualTo(75f));
        UnityEngine.Object.Destroy(obstacle);
        yield return null;
        Physics.SyncTransforms();

        foreach (int actionIndex in new[] { 0, 2, 3, 4 })
        {
            cooldownValue.SetValue(cooldown, 0d);
            useAction.Invoke(combat, new[] { (object)actionIndex, target });
            yield return null;
        }
        Assert.That((float)dummyType.GetProperty("Health").GetValue(dummy), Is.EqualTo(48f));
        Assert.That((float)dummyType.GetProperty("Action").GetValue(dummy), Is.EqualTo(66f));
        Assert.That((float)dummyType.GetProperty("Mind").GetValue(dummy), Is.EqualTo(66f));

        float remainingAction = (float)hamType.GetProperty("Action").GetValue(ham);
        Assert.That((bool)hamType.GetMethod("TrySpendCombatResources")
            .Invoke(ham, new object[] { remainingAction - 1f, 0f }), Is.True);
        cooldownValue.SetValue(cooldown, 0d);
        useAction.Invoke(combat, new[] { (object)1, target });
        yield return null;
        Assert.That((float)dummyType.GetProperty("Health").GetValue(dummy), Is.EqualTo(48f));

        dummyType.GetMethod("ApplyDamage").Invoke(dummy, new object[] { 0f, 100f, 0f });
        Assert.That((bool)dummyType.GetProperty("IsDefeated").GetValue(dummy), Is.True);
        FieldInfo respawnAt = dummyType.GetField("respawnAt", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That((float)respawnAt.GetValue(dummy) - Time.time, Is.EqualTo(8f).Within(0.1f));
        respawnAt.SetValue(dummy, 0f);
        yield return null;
        Assert.That((bool)dummyType.GetProperty("IsDefeated").GetValue(dummy), Is.False);
        Assert.That((float)dummyType.GetProperty("Health").GetValue(dummy), Is.EqualTo(100f));
        Assert.That((float)dummyType.GetProperty("Action").GetValue(dummy), Is.EqualTo(100f));
    }
}
