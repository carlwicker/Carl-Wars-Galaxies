using System.Text;
using Unity.Collections;
using Unity.Netcode;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerChat : NetworkBehaviour
{
    [SerializeField] private float bubbleDuration = 8f;
    [SerializeField] private float bubbleHeight = 3.35f;
    [SerializeField] private Transform visualRoot;

    public bool IsChatActive { get; private set; }

    private TMP_Text bubbleText;
    private UnifiedNameplate bubbleNameplate;
    private string chatInput = string.Empty;
    private int chatActivationFrame = -1;
    private bool focusInputOnNextGuiPass;
    private Coroutine hideBubbleCoroutine;

    private void Awake()
    {
        EnsureBubble();
    }

    public override void OnNetworkDespawn()
    {
        if (hideBubbleCoroutine != null)
        {
            StopCoroutine(hideBubbleCoroutine);
            hideBubbleCoroutine = null;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        bool enterPressed = keyboard.enterKey.wasPressedThisFrame
            || keyboard.numpadEnterKey.wasPressedThisFrame;

        if (IsChatActive)
        {
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelChat();
            }
            else if (enterPressed && chatActivationFrame != Time.frameCount)
            {
                SendChat();
            }
            return;
        }

        if (enterPressed)
        {
            IsChatActive = true;
            chatActivationFrame = Time.frameCount;
            focusInputOnNextGuiPass = true;
        }
    }

    private void OnGUI()
    {
        if (!IsOwner || !IsChatActive) return;

        Rect chatPanel = new Rect(12f, Screen.height - 72f, 420f, 60f);
        GUILayout.BeginArea(chatPanel);
        GUILayout.BeginHorizontal();
        GUI.SetNextControlName("PlayerChatInput");
        chatInput = GUILayout.TextField(chatInput, 128, GUILayout.Width(320f), GUILayout.Height(26f));
        if (focusInputOnNextGuiPass)
        {
            GUI.FocusControl("PlayerChatInput");
            focusInputOnNextGuiPass = false;
        }

        if (GUILayout.Button("Say", GUILayout.Width(80f), GUILayout.Height(26f)))
        {
            SendChat();
            GUIUtility.ExitGUI();
        }
        GUILayout.EndHorizontal();
        GUILayout.EndArea();

    }

    private void SendChat()
    {
        string message = chatInput.Trim();
        if (!string.IsNullOrEmpty(message))
        {
            SendChatServerRpc(new FixedString128Bytes(TruncateMessage(message)));
        }

        CloseChatWindow();
    }

    private void CancelChat()
    {
        CloseChatWindow();
    }

    private void CloseChatWindow()
    {
        chatInput = string.Empty;
        IsChatActive = false;
        chatActivationFrame = -1;
        focusInputOnNextGuiPass = false;
        GUI.FocusControl(null);
    }

    [ServerRpc]
    private void SendChatServerRpc(FixedString128Bytes message)
    {
        ShowMessageClientRpc(message);
    }

    [ClientRpc]
    private void ShowMessageClientRpc(FixedString128Bytes message)
    {
        if (bubbleText == null) return;

        string text = message.ToString().Trim();
        bubbleText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        bubbleText.text = string.IsNullOrEmpty(text) ? string.Empty : $"[{text}]";
        if (!string.IsNullOrEmpty(text))
        {
            bubbleNameplate.Refresh();
        }

        if (hideBubbleCoroutine != null)
        {
            StopCoroutine(hideBubbleCoroutine);
        }

        if (!string.IsNullOrEmpty(text))
        {
            hideBubbleCoroutine = StartCoroutine(HideBubbleAfterDelay());
        }
    }

    private System.Collections.IEnumerator HideBubbleAfterDelay()
    {
        yield return new WaitForSeconds(bubbleDuration);
        if (bubbleText != null)
        {
            bubbleText.gameObject.SetActive(false);
            bubbleText.text = string.Empty;
        }
        hideBubbleCoroutine = null;
    }

    private void EnsureBubble()
    {
        Transform bubbleParent = visualRoot != null ? visualRoot : transform;
        Transform existingBubble = bubbleParent.Find("Chat Bubble");
        if (existingBubble != null)
        {
            bubbleText = existingBubble.GetComponent<TMP_Text>();
            if (bubbleText != null)
            {
                ConfigureBubbleAppearance();
                return;
            }
        }

        GameObject bubbleObject = new GameObject("Chat Bubble");
        bubbleObject.transform.SetParent(bubbleParent, false);
        bubbleObject.transform.localPosition = new Vector3(0f, bubbleHeight, 0f);
        bubbleObject.transform.localScale = Vector3.one * 0.2f;

        TextMeshPro bubble = bubbleObject.AddComponent<TextMeshPro>();
        bubble.text = string.Empty;
        bubble.fontSize = 24f;
        bubble.alignment = TextAlignmentOptions.Center;
        bubble.textWrappingMode = TextWrappingModes.Normal;
        bubble.rectTransform.sizeDelta = new Vector2(30f, 5f);
        bubble.gameObject.AddComponent<LookAtCamera>();
        bubbleText = bubble;
        ConfigureBubbleAppearance();
        bubble.gameObject.SetActive(false);
    }

    private void ConfigureBubbleAppearance()
    {
        bubbleText.color = Color.white;
        bubbleNameplate = bubbleText.GetComponent<UnifiedNameplate>();
        if (bubbleNameplate == null)
        {
            bubbleNameplate = bubbleText.gameObject.AddComponent<UnifiedNameplate>();
        }
    }

    private static string TruncateMessage(string value)
    {
        const int maxUtf8Bytes = 125;
        int length = value.Length;

        while (length > 0 && Encoding.UTF8.GetByteCount(value.Substring(0, length)) > maxUtf8Bytes)
        {
            length--;
            if (length > 0 && char.IsHighSurrogate(value[length - 1]) && char.IsLowSurrogate(value[length]))
            {
                length--;
            }
        }

        return value.Substring(0, length);
    }
}
