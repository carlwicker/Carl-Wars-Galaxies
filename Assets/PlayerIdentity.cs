using System.Text;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public sealed class PlayerIdentity : NetworkBehaviour
{
    [SerializeField] private TMP_Text nameplateText;

    private NameplateBackground nameplateBackground;

    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(
        "Player",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        PlayerName.OnValueChanged += OnPlayerNameChanged;
        UpdateNameplate(PlayerName.Value.ToString());

        if (IsOwner)
        {
            string characterName = string.IsNullOrWhiteSpace(CharacterLoginManager.LocalCharacterName)
                ? $"Player {OwnerClientId}"
                : CharacterLoginManager.LocalCharacterName.Trim();
            PlayerName.Value = TruncateNameToFit(characterName);
        }
    }

    public override void OnNetworkDespawn()
    {
        PlayerName.OnValueChanged -= OnPlayerNameChanged;
    }

    private void OnPlayerNameChanged(FixedString32Bytes oldName, FixedString32Bytes newName)
    {
        UpdateNameplate(newName.ToString());
    }

    private void UpdateNameplate(string newName)
    {
        if (nameplateText != null)
        {
            nameplateText.text = newName;
            nameplateText.fontSize = 32;
            nameplateText.color = Color.white;
            
            if (nameplateBackground == null)
            {
                nameplateBackground = nameplateText.GetComponent<NameplateBackground>();
                if (nameplateBackground == null)
                {
                    nameplateBackground = nameplateText.gameObject.AddComponent<NameplateBackground>();
                }
            }

            nameplateBackground.Refresh();
        }
    }

    private static string TruncateNameToFit(string value)
    {
        const int maxUtf8Bytes = 29;
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
