using System;
using Unity.Netcode;
using UnityEngine;

public sealed class PlayerHAM : NetworkBehaviour
{
    private const float MaximumPool = 100f;
    private const float BarWidth = 144f;
    private const float BarHeight = 14f;
    private const float BarSpacing = 2f;
    private const float BarGroupHeight = BarHeight * 3f + BarSpacing * 2f;

    private static GUIStyle barValueStyle;

    private readonly NetworkVariable<float> health = new NetworkVariable<float>(
        MaximumPool, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> action = new NetworkVariable<float>(
        MaximumPool, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> mind = new NetworkVariable<float>(
        MaximumPool, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public float Health => health.Value;
    public float Action => action.Value;
    public float Mind => mind.Value;

    private void Update()
    {
        if (!IsSpawned || !IsServer) return;
        if (health.Value > 0f && action.Value > 0f && mind.Value > 0f
            && (action.Value < MaximumPool || mind.Value < MaximumPool))
            Restore(0f, 4f * Time.deltaTime, 4f * Time.deltaTime);
    }

    public bool TrySpendCombatResources(float actionCost, float mindCost)
    {
        ValidateAmount(actionCost, nameof(actionCost));
        ValidateAmount(mindCost, nameof(mindCost));
        if (!IsServer)
        {
            Debug.LogError("HAM costs can only be spent by the server.", this);
            return false;
        }

        if (health.Value <= 0f || action.Value <= actionCost || mind.Value <= mindCost)
            return false;

        action.Value -= actionCost;
        mind.Value -= mindCost;
        return true;
    }

    public bool ApplyDamage(float healthDamage, float actionDamage, float mindDamage)
    {
        ValidateAmount(healthDamage, nameof(healthDamage));
        ValidateAmount(actionDamage, nameof(actionDamage));
        ValidateAmount(mindDamage, nameof(mindDamage));

        if (!IsServer)
        {
            Debug.LogError("HAM damage can only be applied by the server.", this);
            return false;
        }

        health.Value = Mathf.Max(0f, health.Value - healthDamage);
        action.Value = Mathf.Max(0f, action.Value - actionDamage);
        mind.Value = Mathf.Max(0f, mind.Value - mindDamage);
        return true;
    }

    public bool Restore(float healthAmount, float actionAmount, float mindAmount)
    {
        ValidateAmount(healthAmount, nameof(healthAmount));
        ValidateAmount(actionAmount, nameof(actionAmount));
        ValidateAmount(mindAmount, nameof(mindAmount));

        if (!IsServer)
        {
            Debug.LogError("HAM pools can only be restored by the server.", this);
            return false;
        }

        health.Value = Mathf.Min(MaximumPool, health.Value + healthAmount);
        action.Value = Mathf.Min(MaximumPool, action.Value + actionAmount);
        mind.Value = Mathf.Min(MaximumPool, mind.Value + mindAmount);
        return true;
    }

    private void OnGUI()
    {
        if (!IsSpawned) return;

        Camera hudCamera = Camera.main;
        if (hudCamera == null) return;

        Vector3 screenPosition = hudCamera.WorldToScreenPoint(transform.position + Vector3.up * 2.7f);
        if (screenPosition.z <= 0f) return;

        DrawHamBars(screenPosition, health.Value, action.Value, mind.Value);
    }

    internal static void DrawHamBars(Vector3 screenPosition, float healthValue, float actionValue, float mindValue)
    {
        float left = screenPosition.x - BarWidth * 0.5f;
        float top = Screen.height - screenPosition.y - BarGroupHeight;
        DrawPool(left, top, healthValue, new Color(0.82f, 0.16f, 0.14f));
        DrawPool(left, top + BarHeight + BarSpacing, actionValue, new Color(0.24f, 0.72f, 0.27f));
        DrawPool(left, top + (BarHeight + BarSpacing) * 2f, mindValue, new Color(0.2f, 0.48f, 0.9f));
    }

    internal static void DrawPool(float left, float top, float value, Color fillColor, string label = null)
    {
        Rect bar = new Rect(left, top, BarWidth, BarHeight);
        DrawRectangle(bar, new Color(0.12f, 0.14f, 0.17f, 1f));
        bar.width *= Mathf.Clamp01(value / MaximumPool);
        DrawRectangle(bar, fillColor);

        if (barValueStyle == null)
        {
            barValueStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 10,
                fontStyle = FontStyle.Bold
            };
            barValueStyle.normal.textColor = Color.white;
        }

        GUI.Label(
            new Rect(left, top - 1f, BarWidth, BarHeight + 2f),
            label ?? Mathf.RoundToInt(value).ToString(),
            barValueStyle);
    }

    private static void DrawRectangle(Rect rectangle, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rectangle, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private static void ValidateAmount(float amount, string parameterName)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
        {
            throw new ArgumentOutOfRangeException(parameterName, "HAM amounts must be finite and non-negative.");
        }
    }
}
