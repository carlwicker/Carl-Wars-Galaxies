using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class SelectionOutlineTests
{
    [UnityTest]
    public IEnumerator PistolRangeFollowsPlayerAndGroundAtTenMeters()
    {
        Type interactionType = Type.GetType("PlayerInteraction, Assembly-CSharp");
        Type groundType = Type.GetType("UnevenGround, Assembly-CSharp");
        Assert.That(interactionType, Is.Not.Null);
        Assert.That(groundType, Is.Not.Null);

        GameObject player = new GameObject("Range test player");
        GameObject terrain = new GameObject("Range test terrain");
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Material material = null;
        try
        {
            terrain.AddComponent<MeshFilter>();
            terrain.AddComponent<MeshCollider>();
            terrain.AddComponent(groundType);
            player.transform.position = new Vector3(10000f, 10f, 10000f);
            ground.transform.position = new Vector3(10000f, 2f, 10000f);
            ground.transform.localScale = new Vector3(200f, 2f, 200f);
            Physics.SyncTransforms();

            Component interaction = player.AddComponent(interactionType);
            Shader shader = Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null);
            material = new Material(shader);
            interactionType.GetField("interactionIndicatorMaterial", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(interaction, material);
            interactionType.GetMethod("CreatePistolRangeIndicator", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(interaction, null);
            interactionType.GetField("pistolRangeGround", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(interaction, ground.GetComponent<Collider>());
            MethodInfo update = interactionType.GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            player.transform.position += new Vector3(3f, 0f, 4f);
            player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            update.Invoke(interaction, null);

            LineRenderer ring = player.GetComponentInChildren<LineRenderer>();
            Assert.That(ring, Is.Not.Null);
            Assert.That(ring.loop, Is.True);
            Assert.That(ring.startColor, Is.EqualTo(Color.green));
            Assert.That(ring.endColor, Is.EqualTo(Color.green));
            for (int i = 0; i < ring.positionCount; i++)
            {
                Vector3 point = ring.GetPosition(i);
                Vector2 offset = new Vector2(point.x - player.transform.position.x, point.z - player.transform.position.z);
                Assert.That(offset.magnitude, Is.EqualTo(10f).Within(0.002f));
                Assert.That(point.y, Is.EqualTo(3.15f).Within(0.001f));
            }

            interactionType.GetMethod("OnNetworkDespawn").Invoke(interaction, null);
            yield return null;
            Assert.That(player.GetComponentInChildren<LineRenderer>(), Is.Null);
        }
        finally
        {
            Object.Destroy(player);
            Object.Destroy(terrain);
            Object.Destroy(ground);
            if (material != null) Object.Destroy(material);
        }
    }

    [UnityTest]
    public IEnumerator WorldMenuRequiresSelectedTargetAndActionRange()
    {
        Type interactionType = Type.GetType("PlayerInteraction, Assembly-CSharp");
        Type pickupType = Type.GetType("WorldPickup, Assembly-CSharp");
        Type outlineType = Type.GetType("SelectionOutline, Assembly-CSharp");
        Assert.That(interactionType, Is.Not.Null);
        Assert.That(pickupType, Is.Not.Null);
        Assert.That(outlineType, Is.Not.Null);

        GameObject player = new GameObject("Interaction test player");
        GameObject first = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject second = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            Component interaction = player.AddComponent(interactionType);
            first.AddComponent(pickupType);
            second.AddComponent(pickupType);
            first.transform.position = new Vector3(2.01f, 0f, 0f);
            second.transform.position = new Vector3(1f, 0f, 0f);
            MethodInfo click = interactionType.GetMethod("HandleWorldTargetClick", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo valid = interactionType.GetMethod("IsWorldMenuTargetValid", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo close = interactionType.GetMethod("CloseRadialMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo menuOpen = interactionType.GetField("radialMenuOpen", BindingFlags.Instance | BindingFlags.NonPublic);
            PropertyInfo target = outlineType.GetProperty("Target");

            click.Invoke(interaction, new object[] { first.transform, Vector2.zero });
            Component outline = player.GetComponent(outlineType);
            Assert.That(target.GetValue(outline), Is.EqualTo(first.transform));
            Assert.That(menuOpen.GetValue(interaction), Is.EqualTo(false));

            click.Invoke(interaction, new object[] { first.transform, Vector2.zero });
            Assert.That(menuOpen.GetValue(interaction), Is.EqualTo(false), "Outside the action circle");
            first.transform.position = new Vector3(2f, 10f, 0f);
            click.Invoke(interaction, new object[] { first.transform, Vector2.zero });
            Assert.That(menuOpen.GetValue(interaction), Is.EqualTo(true), "At the action-circle boundary");
            Assert.That(valid.Invoke(interaction, null), Is.EqualTo(true));

            first.transform.position = new Vector3(2.01f, 0f, 0f);
            Assert.That(valid.Invoke(interaction, null), Is.EqualTo(false), "Moving out of range invalidates the menu");
            close.Invoke(interaction, null);
            click.Invoke(interaction, new object[] { second.transform, Vector2.zero });
            Assert.That(target.GetValue(outline), Is.EqualTo(second.transform));
            Assert.That(menuOpen.GetValue(interaction), Is.EqualTo(false), "Changing target only selects it");
            click.Invoke(interaction, new object[] { second.transform, Vector2.zero });
            Assert.That(menuOpen.GetValue(interaction), Is.EqualTo(true));
            second.SetActive(false);
            Assert.That(valid.Invoke(interaction, null), Is.EqualTo(false), "Collected targets invalidate the menu");
        }
        finally
        {
            Object.Destroy(player);
            Object.Destroy(first);
            Object.Destroy(second);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator CraftingTerminalRequiresSelectionAndActionRange()
    {
        Type interactionType = Type.GetType("PlayerInteraction, Assembly-CSharp");
        Type terminalType = Type.GetType("CraftingTerminal, Assembly-CSharp");
        Assert.That(interactionType, Is.Not.Null);
        Assert.That(terminalType, Is.Not.Null);

        GameObject player = new GameObject("Interaction test player");
        GameObject terminal = new GameObject("Interaction test terminal");
        try
        {
            Component interaction = player.AddComponent(interactionType);
            terminal.AddComponent(terminalType);
            terminal.transform.position = new Vector3(3f, 0f, 0f);
            MethodInfo click = interactionType.GetMethod("HandleWorldTargetClick", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo craftingOpen = interactionType.GetField("craftingOpen", BindingFlags.Instance | BindingFlags.NonPublic);

            click.Invoke(interaction, new object[] { terminal.transform, Vector2.zero });
            click.Invoke(interaction, new object[] { terminal.transform, Vector2.zero });
            Assert.That(craftingOpen.GetValue(interaction), Is.EqualTo(false));
            terminal.transform.position = new Vector3(2f, 0f, 0f);
            click.Invoke(interaction, new object[] { terminal.transform, Vector2.zero });
            Assert.That(craftingOpen.GetValue(interaction), Is.EqualTo(true));
        }
        finally
        {
            Object.Destroy(player);
            Object.Destroy(terminal);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator CubeAndScaledVehicleShowAnOutlineThatClears()
    {
        Type outlineType = Type.GetType("SelectionOutline, Assembly-CSharp");
        Assert.That(outlineType, Is.Not.Null);
        Material template = Resources.Load<Material>("SelectionOutline");
        Assert.That(template, Is.Not.Null);
        Assert.That(template.shader.isSupported, Is.True);

        GameObject cameraObject = new GameObject("Outline test camera");
        GameObject owner = new GameObject("Outline test owner");
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        RenderTexture target = new RenderTexture(128, 128, 24);
        Texture2D pixels = new Texture2D(128, 128, TextureFormat.RGB24, false);
        Material cubeMaterial = null;
        try
        {
            const int testLayer = 30;
            cube.layer = testLayer;
            cube.transform.position = new Vector3(100f, 500f, 100f);
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            Assert.That(unlitShader, Is.Not.Null);
            cubeMaterial = new Material(unlitShader);
            cubeMaterial.color = Color.blue;
            cube.GetComponent<Renderer>().sharedMaterial = cubeMaterial;

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = cube.transform.position + new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << testLayer;
            camera.targetTexture = target;

            Component outline = owner.AddComponent(outlineType);
            var setTarget = outlineType.GetMethod("SetTarget");
            Assert.That(setTarget, Is.Not.Null);
            setTarget.Invoke(outline, new object[] { cube.transform });
            yield return new WaitForEndOfFrame();
            Assert.That(CountYellowPixels(target, pixels), Is.GreaterThan(0), "Front-facing cube");

            cube.transform.localScale = new Vector3(1f, 1f, 4f);
            cube.transform.rotation = Quaternion.Euler(15f, 30f, 0f);
            yield return new WaitForEndOfFrame();
            Assert.That(CountYellowPixels(target, pixels), Is.GreaterThan(0), "Scaled, rotated vehicle");

            setTarget.Invoke(outline, new object[] { null });
            yield return new WaitForEndOfFrame();
            Assert.That(CountYellowPixels(target, pixels), Is.Zero, "Deselected object");
        }
        finally
        {
            cameraObject.GetComponent<Camera>().targetTexture = null;
            Object.Destroy(cameraObject);
            Object.Destroy(owner);
            Object.Destroy(cube);
            Object.Destroy(cubeMaterial);
            Object.Destroy(pixels);
            target.Release();
            Object.Destroy(target);
        }
    }

    private static int CountYellowPixels(RenderTexture target, Texture2D pixels)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        pixels.Apply();
        RenderTexture.active = previous;

        int count = 0;
        foreach (Color pixel in pixels.GetPixels())
        {
            if (pixel.r > 0.7f && pixel.g > 0.6f && pixel.b < 0.3f) count++;
        }
        return count;
    }
}
