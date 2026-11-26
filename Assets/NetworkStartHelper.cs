using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

public class NetworkStartHelper : MonoBehaviour
{
    private const int MaxPlayers = 8;
    private const float LoginPanelWidth = 360f;
    private const float LoginPanelHeight = 240f;
    private const float SessionPanelWidth = 240f;
    private const float SessionPanelHeight = 86f;
    private const float ScreenEdgePadding = 12f;

    [SerializeField] private string playerName = "Citizen";
    private string joinCode = string.Empty;
    private string sessionCode = string.Empty;
    private string sessionStatus;
    private bool isConnecting;
    private bool servicesInitialized;
    private ISession currentSession;

    private void OnGUI()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null) return;

        if (currentSession != null)
        {
            DrawSessionStatus();
            return;
        }

        Rect panel = new Rect(
            (Screen.width - LoginPanelWidth) * 0.5f,
            (Screen.height - LoginPanelHeight) * 0.5f,
            LoginPanelWidth,
            LoginPanelHeight);

        GUILayout.BeginArea(panel, GUI.skin.box);
        GUILayout.Label("Enter your player name");
        playerName = GUILayout.TextField(playerName, 32);
        GUILayout.Space(6f);
        GUILayout.Label("Host: create a session and share its code.");
        if (GUILayout.Button(isConnecting ? "Connecting..." : "Host", GUILayout.Height(30f))
            && !isConnecting)
        {
            StartSession(true);
        }

        GUILayout.Space(6f);
        GUILayout.Label("Join: enter the host's session code.");
        GUILayout.BeginHorizontal();
        joinCode = GUILayout.TextField(joinCode, 12);
        if (GUILayout.Button("Join", GUILayout.Width(90f), GUILayout.Height(25f))
            && !isConnecting)
        {
            StartSession(false);
        }
        GUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(sessionStatus))
        {
            GUILayout.Label(sessionStatus);
        }
        GUILayout.EndArea();
    }

    private void DrawSessionStatus()
    {
        Rect panel = new Rect(
            Screen.width - SessionPanelWidth - ScreenEdgePadding,
            Screen.height - SessionPanelHeight - ScreenEdgePadding,
            SessionPanelWidth,
            SessionPanelHeight);

        GUILayout.BeginArea(panel, GUI.skin.box);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Session active", GUILayout.Height(22f));

        if (!string.IsNullOrEmpty(sessionCode))
        {
            if (GUILayout.Button("Copy", GUILayout.Width(54f), GUILayout.Height(22f)))
            {
                GUIUtility.systemCopyBuffer = sessionCode;
            }
        }
        GUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(sessionCode))
        {
            GUILayout.Label($"Join code: {sessionCode}");
        }

        GUILayout.EndArea();
    }

    private async void StartSession(bool create)
    {
        if (isConnecting) return;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            sessionStatus = "NetworkManager is missing from the scene.";
            return;
        }

        if (!create && string.IsNullOrWhiteSpace(joinCode))
        {
            sessionStatus = "Enter the host's join code first.";
            return;
        }

        string enteredName = playerName.Trim();
        CharacterLoginManager.LocalCharacterName = string.IsNullOrEmpty(enteredName)
            ? "Citizen"
            : enteredName;

        isConnecting = true;
        sessionStatus = "Connecting to Unity services...";

        try
        {
            await InitializeServicesAsync();
            SessionOptions options = new SessionOptions { MaxPlayers = MaxPlayers }.WithRelayNetwork();
            currentSession = create
                ? await MultiplayerService.Instance.CreateSessionAsync(options)
                : await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode.Trim());

            sessionCode = currentSession.Code;
            sessionStatus = create ? "Session created." : "Joined session.";
        }
        catch (ServicesInitializationException exception)
        {
            sessionStatus = $"Unity services initialization failed: {exception.Message}";
        }
        catch (SessionException exception)
        {
            sessionStatus = $"Could not connect: {exception.Message}";
        }
        catch (RequestFailedException exception)
        {
            sessionStatus = $"Unity services request failed ({exception.ErrorCode}): {exception.Message}";
        }
        finally
        {
            isConnecting = false;
        }
    }

    private async Task InitializeServicesAsync()
    {
        if (!servicesInitialized)
        {
            await UnityServices.InitializeAsync();
            servicesInitialized = true;
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }
}