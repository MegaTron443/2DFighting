using System;
using Steamworks;

public sealed class SteamIdentityProvider : IPlayerIdentityProvider
{
    private readonly AppConfig? _fallbackConfig;

    public SteamIdentityProvider(AppConfig? fallbackConfig = null)
    {
        _fallbackConfig = fallbackConfig;
    }

    public ulong SteamId
    {
        get
        {
            if (SteamManager.IsInitialized)
            {
                ulong liveId = SteamUser.GetSteamID().m_SteamID;
                if (liveId != 0UL) return liveId;
            }
            return SteamManager.LocalSteamId != 0UL
                ? SteamManager.LocalSteamId
                : (_fallbackConfig?.SteamId ?? 0UL);
        }
    }

    public string DisplayName
    {
        get
        {
            if (SteamManager.IsInitialized)
            {
                string persona = SteamFriends.GetPersonaName();
                if (!string.IsNullOrWhiteSpace(persona))
                    return persona.Trim();
            }
            return !string.IsNullOrWhiteSpace(SteamManager.LocalPersonaName)
                ? SteamManager.LocalPersonaName
                : Environment.UserName;
        }
    }
}

public static class SteamManager
{
    public static bool IsInitialized { get; private set; }
    public static ulong LocalSteamId { get; private set; }
    public static string LocalPersonaName { get; private set; } = Environment.UserName;
    public static string StatusMessage { get; private set; } = "STEAM OFFLINE (LOCAL MODE)";

    public static void Initialize()
    {
        if (IsInitialized) return;

        try
        {
            if (!SteamAPI.Init())
            {
                StatusMessage = "STEAM NOT RUNNING (LOCAL MODE)";
                Console.WriteLine("[SteamManager] SteamAPI.Init() returned false; running in local/offline mode.");
                return;
            }

            IsInitialized = true;
            LocalSteamId = SteamUser.GetSteamID().m_SteamID;
            string persona = SteamFriends.GetPersonaName();
            LocalPersonaName = string.IsNullOrWhiteSpace(persona) ? Environment.UserName : persona.Trim();
            StatusMessage = $"STEAM CONNECTED ({LocalPersonaName})";

            SteamNetworkingUtils.InitRelayNetworkAccess();
            SteamNetTransport.Initialize();
            SteamLobbyManager.Initialize();
            SteamLeaderboardSync.Initialize();
            SteamRichPresence.Initialize();

            Console.WriteLine($"[SteamManager] Initialized Steamworks for '{LocalPersonaName}' ({LocalSteamId}).");
        }
        catch (DllNotFoundException ex)
        {
            IsInitialized = false;
            StatusMessage = "STEAM SDK DLL MISSING (LOCAL MODE)";
            Console.WriteLine($"[SteamManager] Steam native library not found: {ex.Message}");
        }
        catch (Exception ex)
        {
            IsInitialized = false;
            StatusMessage = "STEAM INIT FAILED (LOCAL MODE)";
            Console.WriteLine($"[SteamManager] Initialization failed: {ex.Message}");
        }
    }

    public static void RunCallbacks()
    {
        if (!IsInitialized) return;

        SteamAPI.RunCallbacks();
        SteamLeaderboardSync.Pump();
        SteamLobbyManager.Tick();
    }

    public static void Shutdown()
    {
        if (!IsInitialized) return;

        try
        {
            SteamRichPresence.Clear();
            SteamLobbyManager.Shutdown();
            SteamNetTransport.Shutdown();
            SteamLeaderboardSync.Shutdown();
            SteamAPI.Shutdown();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SteamManager] Shutdown error: {ex.Message}");
        }
        finally
        {
            IsInitialized = false;
            StatusMessage = "STEAM SHUTDOWN";
        }
    }
}
