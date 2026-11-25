using System;
using Steamworks;

public static class SteamRichPresence
{
    private static string _lastStatus = string.Empty;
    private static string _lastConnect = string.Empty;

    public static void Initialize()
    {
        _lastStatus = string.Empty;
        _lastConnect = string.Empty;
    }

    public static void UpdateForSession(EngineServices services)
    {
        if (!SteamManager.IsInitialized) return;

        string status = ComputeStatusString(services);
        string connect = SteamLobbyManager.IsInMatchLobby && SteamLobbyManager.CurrentLobbyId != 0UL
            ? $"+connect_lobby {SteamLobbyManager.CurrentLobbyId}"
            : string.Empty;

        if (!string.Equals(status, _lastStatus, StringComparison.Ordinal))
        {
            _lastStatus = status;
            SteamFriends.SetRichPresence("status", status);
            SteamFriends.SetRichPresence("steam_display", "#StatusFull");
        }

        if (!string.Equals(connect, _lastConnect, StringComparison.Ordinal))
        {
            _lastConnect = connect;
            SteamFriends.SetRichPresence("connect", connect);
        }
    }

    public static void Clear()
    {
        if (!SteamManager.IsInitialized) return;
        _lastStatus = string.Empty;
        _lastConnect = string.Empty;
        SteamFriends.ClearRichPresence();
    }

    private static string ComputeStatusString(EngineServices services)
    {
        switch (GameState.CurrentState)
        {
            case State.MainMenu:
                if (SteamLobbyManager.State == LobbySearchState.Searching ||
                    SteamLobbyManager.State == LobbySearchState.Hosting)
                {
                    return string.Equals(SteamLobbyManager.QueuedMode, "2v2", StringComparison.OrdinalIgnoreCase)
                        ? "Queuing 2v2"
                        : "Queuing 1v1";
                }
                if (CameraManager.CurrentLobbyRoom == LobbyRoom.BottomTraining)
                    return "In Training Lab";
                return "In Lobby (Sparring)";

            case State.ChoiceMenu:
                return SteamLobbyManager.IsInMatchLobby
                    ? "Online Lobby: Selecting Fighter"
                    : "Selecting Fighter";

            case State.Playing:
                string p1Name = services.Player1?.CurrentCharacter?.Name ?? "Fighter 1";
                string p2Name = services.Player2?.CurrentCharacter?.Name ?? "Fighter 2";
                if (services.OnlineSession.IsActive)
                    return $"Online Match: {p1Name} vs {p2Name}";
                if (services.Match.IsTrainingMode)
                    return $"AI Training: {p1Name} vs {p2Name}";
                return $"In Match: {p1Name} vs {p2Name}";

            case State.Paused:
                return "Match Paused";

            case State.GameOver:
                return "Post-Match Results";

            case State.Settings:
                return "Configuring Settings";

            default:
                return "In Menus";
        }
    }
}
