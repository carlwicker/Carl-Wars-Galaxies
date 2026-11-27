using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerHAM), typeof(PlayerInteraction))]
public sealed class PlayerCombat : NetworkBehaviour
{
    private const float SlotWidth = 100f;
    private const float BarHeight = 78f;

    private struct CombatAction
    {
        public string Name;
        public float HealthDamage, ActionDamage, MindDamage, ActionCost, MindCost, Cooldown;

        public CombatAction(string name, float healthDamage, float actionDamage, float mindDamage,
            float actionCost, float mindCost, float cooldown)
        {
            Name = name;
            HealthDamage = healthDamage;
            ActionDamage = actionDamage;
            MindDamage = mindDamage;
            ActionCost = actionCost;
            MindCost = mindCost;
            Cooldown = cooldown;
        }
    }

    private static readonly CombatAction[] Actions =
    {
        new CombatAction("Quick Shot", 12f, 0f, 0f, 0f, 0f, 1f),
        new CombatAction("Power Shot", 25f, 0f, 0f, 14f, 4f, 2f),
        new CombatAction("Crippling Shot", 0f, 24f, 0f, 12f, 5f, 2f),
        new CombatAction("Disrupting Shot", 0f, 0f, 24f, 8f, 10f, 2f),
        new CombatAction("Burst Fire", 15f, 10f, 10f, 18f, 8f, 3f)
    };

    private readonly NetworkVariable<double> nextActionTime = new NetworkVariable<double>(
        0, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
    private PlayerHAM ham;
    private PlayerInteraction interaction;
    private PlayerChat chat;
    private VehicleInteraction vehicle;
    private string status = "Click the training dummy to target it.";
    private float statusUntil;

    private static Rect ActionBar => new Rect((Screen.width - SlotWidth * 5f) * 0.5f,
        Screen.height - BarHeight - 12f, SlotWidth * 5f, BarHeight);

    public static bool IsPointerOverActionBar(Vector2 position) => ActionBar.Contains(position);

    private void Awake()
    {
        ham = GetComponent<PlayerHAM>();
        interaction = GetComponent<PlayerInteraction>();
        chat = GetComponent<PlayerChat>();
        vehicle = GetComponent<VehicleInteraction>();
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner || interaction.IsCombatInputBlocked
            || (chat != null && chat.IsChatActive)) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.digit1Key.wasPressedThisFrame) RequestAction(0);
        else if (keyboard.digit2Key.wasPressedThisFrame) RequestAction(1);
        else if (keyboard.digit3Key.wasPressedThisFrame) RequestAction(2);
        else if (keyboard.digit4Key.wasPressedThisFrame) RequestAction(3);
        else if (keyboard.digit5Key.wasPressedThisFrame) RequestAction(4);
    }

    private void RequestAction(int index)
    {
        Transform target = interaction.SelectedTarget;
        CombatDummy dummy = target != null ? target.GetComponent<CombatDummy>() : null;
        if (dummy == null || !dummy.IsSpawned || dummy.IsDefeated)
        {
            ShowStatus("Click a living training dummy to target it.");
            return;
        }
        UseActionServerRpc(index, dummy.NetworkObject);
    }

    [ServerRpc]
    private void UseActionServerRpc(int index, NetworkObjectReference targetReference)
    {
        if (index < 0 || index >= Actions.Length
            || !targetReference.TryGet(out NetworkObject targetObject)
            || !targetObject.TryGetComponent(out CombatDummy dummy) || dummy.IsDefeated)
        {
            ReportFailure("Invalid or defeated target.");
            return;
        }
        if (vehicle != null && vehicle.IsRidingVehicle)
        {
            ReportFailure("Dismount before attacking.");
            return;
        }
        Vector3 offset = dummy.transform.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude > PlayerInteraction.PistolRange * PlayerInteraction.PistolRange)
        {
            ReportFailure("Target is outside pistol range (10m).");
            return;
        }
        Vector3 origin = transform.position + Vector3.up;
        Vector3 destination = dummy.transform.position + Vector3.up;
        RaycastHit[] hits = Physics.RaycastAll(origin, destination - origin,
            Vector3.Distance(origin, destination), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform) || hit.transform == transform
                || hit.transform.IsChildOf(dummy.transform) || hit.transform == dummy.transform) continue;
            ReportFailure("Line of sight is blocked.");
            return;
        }
        if (NetworkManager.ServerTime.Time < nextActionTime.Value)
        {
            ReportFailure("Action is still on cooldown.");
            return;
        }
        CombatAction action = Actions[index];
        if (!ham.TrySpendCombatResources(action.ActionCost, action.MindCost))
        {
            ReportFailure("Not enough Action or Mind, or incapacitated.");
            return;
        }
        nextActionTime.Value = NetworkManager.ServerTime.Time + action.Cooldown;
        PlayMuzzleFlashClientRpc(dummy.transform.position);
        dummy.ApplyDamageFrom(action.HealthDamage, action.ActionDamage, action.MindDamage, transform.position);
    }

    [ClientRpc]
    private void PlayMuzzleFlashClientRpc(Vector3 targetPosition)
    {
        Vector3 direction = Vector3.ProjectOnPlane(targetPosition - transform.position, Vector3.up).normalized;
        if (direction.sqrMagnitude < 0.001f)
            direction = transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
        Vector3 muzzlePosition = transform.position + Vector3.up * 1.1f + direction * 0.45f + right * 0.2f;

        GameObject flashObject = new GameObject("Pistol Muzzle Flash");
        flashObject.transform.SetPositionAndRotation(muzzlePosition, Quaternion.LookRotation(direction));
        ParticleSystem flash = flashObject.AddComponent<ParticleSystem>();
        flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = flash.main;
        main.duration = 0.2f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
        main.startColor = new Color(5f, 4f, 1.5f);
        main.maxParticles = 8;

        ParticleSystem.EmissionModule emission = flash.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });

        ParticleSystem.ShapeModule shape = flash.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 24f;
        shape.radius = 0.015f;

        ParticleSystemRenderer flashRenderer = flash.GetComponent<ParticleSystemRenderer>();
        flashRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        flashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        flashRenderer.receiveShadows = false;

        GameObject lightObject = new GameObject("Pistol Muzzle Flash Light");
        lightObject.transform.position = muzzlePosition;
        Light muzzleLight = lightObject.AddComponent<Light>();
        muzzleLight.color = new Color(1f, 0.9f, 0.62f);
        muzzleLight.intensity = 15f;
        muzzleLight.range = 2.5f;
        muzzleLight.shadows = LightShadows.None;

        flash.Play();
        Destroy(flashObject, 0.25f);
        Destroy(lightObject, 0.1f);
    }

    private void ReportFailure(string message)
    {
        ReportFailureClientRpc(message, new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        });
    }

    [ClientRpc]
    private void ReportFailureClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        if (IsOwner) ShowStatus(message);
    }

    private void ShowStatus(string message)
    {
        status = message;
        statusUntil = Time.unscaledTime + 3f;
    }

    private void OnGUI()
    {
        if (!IsSpawned || !IsOwner) return;
        Rect panel = ActionBar;
        GUI.Box(panel, GUIContent.none);
        bool inputAllowed = !interaction.IsCombatInputBlocked && (chat == null || !chat.IsChatActive);
        for (int i = 0; i < Actions.Length; i++)
        {
            CombatAction action = Actions[i];
            string label = $"{i + 1}: {action.Name}\nA {action.ActionCost:0}  M {action.MindCost:0}\n{action.Cooldown:0.#}s";
            GUI.enabled = inputAllowed;
            if (GUI.Button(new Rect(panel.x + i * SlotWidth + 2f, panel.y + 3f, SlotWidth - 4f, 70f), label))
                RequestAction(i);
        }
        GUI.enabled = true;

        Transform selected = interaction.SelectedTarget;
        CombatDummy dummy = selected != null ? selected.GetComponent<CombatDummy>() : null;
        string targetText = dummy == null ? "Target: none" : dummy.IsDefeated ? "Training dummy: respawning"
            : $"Training dummy   H {dummy.Health:0}   A {dummy.Action:0}   M {dummy.Mind:0}";
        double cooldown = System.Math.Max(0, nextActionTime.Value - NetworkManager.ServerTime.Time);
        GUI.Label(new Rect(panel.x, panel.y - 48f, panel.width, 22f), targetText);
        GUI.Label(new Rect(panel.x, panel.y - 25f, panel.width, 22f),
            Time.unscaledTime < statusUntil ? status : cooldown > 0 ? $"Cooldown: {cooldown:0.0}s" : "Ready");
    }
}
