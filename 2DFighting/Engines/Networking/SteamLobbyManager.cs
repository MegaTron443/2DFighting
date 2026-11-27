using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Steamworks;

public enum LobbySearchState
{
    Idle,
    Searching,
    Hosting,
    MatchedInLobby,
    Playing,
    Error
}

public enum ModHandshakeState
{
    Pending,
    WaitingForPeer,
    SyncingMod,
    Verified,
    MismatchBlocked
}

public readonly record struct PendingOnlineMatchStart(
    uint MatchSeed,
    int InputDelayFrames,
    bool RollbackEnabled);

public static class SteamLobbyManager
{
    private const string LobbyKeyGame = "game";
    private const string LobbyKeyMode = "mode";
    private const string LobbyKeyModFilter = "mod_filter";
    private const string LobbyKeyGaEnabled = "ga_enabled";
    private const string LobbyKeyState = "state";
    private const string LobbyKeyMatchSeed = "match_seed";
    private const string LobbyKeyVisibility = "visibility";
    private const string LobbyGameId = "2DFighting";

    private const string MemberKeyCharId = "char_id";
    private const string MemberKeyScriptHash = "script_hash";
    private const string MemberKeyBaseHash = "base_hash";
    private const string MemberKeyReady = "ready";
    private const string MemberKeyWorkshopId = "workshop_id";
    private const string MemberKeyPartnerCharId = "partner_char_id";
    private const string MemberKeyPartnerScriptHash = "partner_script_hash";
    private const string MemberKeyPartnerBaseHash = "partner_base_hash";
    private const string MemberKeyPartnerWorkshopId = "partner_workshop_id";

    private sealed class IncomingModTransfer
    {
        public int TransferId;
        public string CharId = "";
        public int TotalChunks;
        public byte[][] Chunks = Array.Empty<byte[]>();
        public int ReceivedCount;
    }

    private static CallResult<LobbyMatchList_t>? _lobbyMatchListCall;
    private static CallResult<LobbyCreated_t>? _lobbyCreatedCall;
    private static Callback<LobbyEnter_t>? _lobbyEnterCallback;
    private static Callback<LobbyDataUpdate_t>? _lobbyDataUpdateCallback;
    private static Callback<LobbyChatUpdate_t>? _lobbyChatUpdateCallback;
    private static Callback<GameLobbyJoinRequested_t>? _joinRequestedCallback;

    private static CSteamID _currentLobby = CSteamID.Nil;
    private static IncomingModTransfer? _activeModTransfer;
    private static float _modRequestCooldown = 0f;
    private static int _modRequestAttempts = 0;
    private static float _hostSessionPingCooldown = 0f;
    private static bool _joiningViaFriendInvite = false;
    private static ulong _pendingWorkshopDownloadId = 0UL;
    private static readonly HashSet<ulong> _completedWorkshopDownloadIds = new();
    private static bool _activeModRequestIsPartner = false;
    private static string _activeModRequestCharId = "";
    private static string _activeModRequestHash = "";

    public static LobbySearchState State { get; private set; } = LobbySearchState.Idle;
    public static ModHandshakeState HandshakeState { get; private set; } = ModHandshakeState.Pending;
    public static string QueuedMode { get; private set; } = "1v1";
    public static bool Is2v2Lobby => string.Equals(QueuedMode, "2v2", StringComparison.OrdinalIgnoreCase);
    public static ModFilterMode LobbyModFilter { get; private set; } = ModFilterMode.VanillaAndMods;
    public static bool LobbyGaEnabled { get; private set; } = true;
    public static bool IsPrivateOrFriendsLobby { get; private set; }
    public static string StatusText { get; private set; } = "";

    public static ulong CurrentLobbyId => _currentLobby.IsValid() ? _currentLobby.m_SteamID : 0UL;
    public static bool IsInMatchLobby => SteamManager.IsInitialized && _currentLobby.IsValid() && CurrentLobbyId != 0UL;
    public static bool IsHost { get; private set; }
    public static int LocalSlot { get; private set; }
    public static int PeerSlot => LocalSlot == 0 ? 1 : 0;

    public static ulong PeerSteamId { get; private set; }
    public static string PeerPersonaName { get; private set; } = "Opponent";

    public static string LocalCharId { get; private set; } = "";
    public static string LocalScriptHash { get; private set; } = "";
    public static string LocalBaseScriptHash { get; private set; } = "";
    public static bool LocalReady { get; private set; }
    public static ulong LocalWorkshopId { get; private set; }
    public static string LocalPartnerCharId { get; private set; } = "";
    public static string LocalPartnerScriptHash { get; private set; } = "";
    public static string LocalPartnerBaseScriptHash { get; private set; } = "";
    public static ulong LocalPartnerWorkshopId { get; private set; }

    public static string PeerCharId { get; private set; } = "";
    public static string PeerScriptHash { get; private set; } = "";
    public static string PeerBaseScriptHash { get; private set; } = "";
    public static bool PeerReady { get; private set; }
    public static bool PeerGaEnabled { get; private set; } = true;
    public static ulong PeerWorkshopId { get; private set; }
    public static string PeerPartnerCharId { get; private set; } = "";
    public static string PeerPartnerScriptHash { get; private set; } = "";
    public static string PeerPartnerBaseScriptHash { get; private set; } = "";
    public static ulong PeerPartnerWorkshopId { get; private set; }
    public static ulong ActiveSyncWorkshopId { get; private set; }

    public static string? PeerCustomScriptPath { get; private set; }
    public static CharacterBalanceGenome? PeerCustomBalanceGenome { get; private set; }
    public static string? PeerPartnerCustomScriptPath { get; private set; }
    public static CharacterBalanceGenome? PeerPartnerCustomBalanceGenome { get; private set; }

    public static bool ShouldEnterCharacterSelect { get; private set; }
    public static PendingOnlineMatchStart? PendingStart { get; private set; }

    public static void Initialize()
    {
        if (!SteamManager.IsInitialized) return;

        _lobbyMatchListCall = CallResult<LobbyMatchList_t>.Create(OnLobbyMatchList);
        _lobbyCreatedCall = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
        _lobbyEnterCallback = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        _lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
        _lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
        _joinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
    }

    public static void Shutdown()
    {
        LeaveLobby();
        _lobbyMatchListCall?.Dispose();
        _lobbyMatchListCall = null;
        _lobbyCreatedCall?.Dispose();
        _lobbyCreatedCall = null;
        _lobbyEnterCallback?.Dispose();
        _lobbyEnterCallback = null;
        _lobbyDataUpdateCallback?.Dispose();
        _lobbyDataUpdateCallback = null;
        _lobbyChatUpdateCallback?.Dispose();
        _lobbyChatUpdateCallback = null;
        _joinRequestedCallback?.Dispose();
        _joinRequestedCallback = null;
    }

    public static void ConsumeEnterCharacterSelectFlag() => ShouldEnterCharacterSelect = false;
    public static void ConsumePendingStart() => PendingStart = null;

    public static bool IsPeerAuthorized(ulong steamId)
    {
        if (!IsInMatchLobby || steamId == 0UL) return false;
        if (steamId == PeerSteamId) return true;

        int memberCount = SteamMatchmaking.GetNumLobbyMembers(_currentLobby);
        for (int i = 0; i < memberCount; i++)
        {
            if (SteamMatchmaking.GetLobbyMemberByIndex(_currentLobby, i).m_SteamID == steamId)
                return true;
        }
        return false;
    }

    public static void StartMatchmaking(string mode, ModFilterMode filter, bool gaEnabled)
    {
        if (!SteamManager.IsInitialized)
        {
            State = LobbySearchState.Error;
            StatusText = "STEAM OFFLINE";
            return;
        }

        LeaveLobby();

        QueuedMode = string.Equals(mode, "2v2", StringComparison.OrdinalIgnoreCase) ? "2v2" : "1v1";
        LobbyModFilter = filter;
        LobbyGaEnabled = gaEnabled;
        State = LobbySearchState.Searching;
        StatusText = $"SEARCHING {QueuedMode.ToUpperInvariant()} LOBBIES...";

        SteamMatchmaking.AddRequestLobbyListStringFilter(LobbyKeyGame, LobbyGameId, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListStringFilter(LobbyKeyMode, QueuedMode, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListStringFilter(LobbyKeyModFilter, filter.ToString(), ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListStringFilter(LobbyKeyGaEnabled, gaEnabled ? "1" : "0", ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListStringFilter(LobbyKeyState, "queuing", ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(10);

        SteamAPICall_t call = SteamMatchmaking.RequestLobbyList();
        _lobbyMatchListCall?.Set(call);
    }

    public static void LeaveLobby()
    {
        if (PeerSteamId != 0UL)
        {
            SteamNetTransport.CloseSessionWithPeer(PeerSteamId);
        }

        if (SteamManager.IsInitialized && _currentLobby.IsValid())
        {
            SteamMatchmaking.LeaveLobby(_currentLobby);
        }

        _currentLobby = CSteamID.Nil;
        IsHost = false;
        IsPrivateOrFriendsLobby = false;
        _joiningViaFriendInvite = false;
        LocalSlot = 0;
        PeerSteamId = 0UL;
        PeerPersonaName = "Opponent";
        LocalCharId = "";
        LocalScriptHash = "";
        LocalBaseScriptHash = "";
        LocalReady = false;
        LocalWorkshopId = 0UL;
        LocalPartnerCharId = "";
        LocalPartnerScriptHash = "";
        LocalPartnerBaseScriptHash = "";
        LocalPartnerWorkshopId = 0UL;
        ResetPeerHandshakeState();
        _hostSessionPingCooldown = 0f;
        PendingStart = null;
        ShouldEnterCharacterSelect = false;
        State = LobbySearchState.Idle;
        StatusText = "";
    }

    private static bool CanSendP2PToPeer()
    {
        return IsInMatchLobby &&
               PeerSteamId != 0UL &&
               (IsHost || SteamNetTransport.IsPeerSessionActive(PeerSteamId));
    }

    private static void ResetPeerHandshakeState()
    {
        PeerCharId = "";
        PeerScriptHash = "";
        PeerBaseScriptHash = "";
        PeerReady = false;
        PeerWorkshopId = 0UL;
        PeerPartnerCharId = "";
        PeerPartnerScriptHash = "";
        PeerPartnerBaseScriptHash = "";
        PeerPartnerWorkshopId = 0UL;
        ActiveSyncWorkshopId = 0UL;
        PeerCustomScriptPath = null;
        PeerCustomBalanceGenome = null;
        PeerPartnerCustomScriptPath = null;
        PeerPartnerCustomBalanceGenome = null;
        _activeModTransfer = null;
        _modRequestCooldown = 0f;
        _modRequestAttempts = 0;
        _hostSessionPingCooldown = 0f;
        _pendingWorkshopDownloadId = 0UL;
        _completedWorkshopDownloadIds.Clear();
        _activeModRequestIsPartner = false;
        _activeModRequestCharId = "";
        _activeModRequestHash = "";
        HandshakeState = PeerSteamId != 0UL ? ModHandshakeState.WaitingForPeer : ModHandshakeState.Pending;
    }

    public static void SetLocalSelection(
        string charId,
        string scriptHash,
        string baseScriptHash,
        bool ready,
        ulong workshopId = 0UL,
        string partnerCharId = "",
        string partnerScriptHash = "",
        string partnerBaseScriptHash = "",
        ulong partnerWorkshopId = 0UL)
    {
        LocalCharId = charId ?? "";
        LocalScriptHash = scriptHash ?? "";
        LocalBaseScriptHash = baseScriptHash ?? "";
        LocalReady = ready;
        LocalWorkshopId = workshopId;
        LocalPartnerCharId = partnerCharId ?? "";
        LocalPartnerScriptHash = partnerScriptHash ?? "";
        LocalPartnerBaseScriptHash = partnerBaseScriptHash ?? "";
        LocalPartnerWorkshopId = partnerWorkshopId;

        if (!IsInMatchLobby) return;

        CSteamID current = _currentLobby;
        SteamMatchmaking.SetLobbyMemberData(current, MemberKeyCharId, LocalCharId);
        SteamMatchmaking.SetLobbyMemberData(current, MemberKeyScriptHash, LocalScriptHash);
        SteamMatchmaking.SetLobbyMemberData(current, MemberKeyBaseHash, LocalBaseScriptHash);
        SteamMatchmaking.SetLobbyMemberData(current, MemberKeyReady, ready ? "1" : "0");
        SteamMatchmaking.SetLobbyMemberData(current, MemberKeyWorkshopId, LocalWorkshopId.ToString());
        SteamMatchmaking.SetLobbyMemberData(current, MemberKeyPartnerCharId, LocalPartnerCharId);
        SteamMatchmaking.SetLobbyMemberData(current, MemberKeyPartnerScriptHash, LocalPartnerScriptHash);
        SteamMatchmaking.SetLobbyMemberData(current, MemberKeyPartnerBaseHash, LocalPartnerBaseScriptHash);
        SteamMatchmaking.SetLobbyMemberData(current, MemberKeyPartnerWorkshopId, LocalPartnerWorkshopId.ToString());

        if (!string.IsNullOrEmpty(LocalCharId) && CanSendP2PToPeer())
        {
            SteamNetTransport.SendFighterSelection(
                PeerSteamId,
                LocalCharId,
                LocalScriptHash,
                LocalBaseScriptHash,
                LocalReady,
                BalanceOverrideStore.IsEnabled,
                LocalWorkshopId,
                LocalPartnerCharId,
                LocalPartnerScriptHash,
                LocalPartnerBaseScriptHash,
                LocalPartnerWorkshopId);
        }

        EvaluateHandshake();
    }

    public static void OnPeerSessionEstablished(ulong peerId)
    {
        if (!IsInMatchLobby || peerId != PeerSteamId) return;

        if (!IsHost && !string.IsNullOrEmpty(LocalCharId))
        {
            SteamNetTransport.SendFighterSelection(
                PeerSteamId,
                LocalCharId,
                LocalScriptHash,
                LocalBaseScriptHash,
                LocalReady,
                BalanceOverrideStore.IsEnabled,
                LocalWorkshopId,
                LocalPartnerCharId,
                LocalPartnerScriptHash,
                LocalPartnerBaseScriptHash,
                LocalPartnerWorkshopId);
        }

        if (HandshakeState == ModHandshakeState.SyncingMod &&
            ActiveSyncWorkshopId == 0UL &&
            IsPrivateOrFriendsLobby &&
            !string.IsNullOrEmpty(_activeModRequestCharId) &&
            _modRequestAttempts == 0)
        {
            _modRequestAttempts = 1;
            _modRequestCooldown = 2.0f;
            SteamNetTransport.SendModScriptRequest(PeerSteamId, _activeModRequestCharId, _activeModRequestHash);
        }
    }

    public static void OnPeerSessionFailed(ulong peerId)
    {
        if (!IsInMatchLobby || peerId != PeerSteamId) return;

        if (IsHost)
        {
            _hostSessionPingCooldown = 0.5f;
        }
    }

    public static void Tick()
    {
        if (!IsInMatchLobby || PeerSteamId == 0UL) return;

        if (IsHost && State == LobbySearchState.MatchedInLobby)
        {
            _hostSessionPingCooldown = MathF.Max(0f, _hostSessionPingCooldown - 0.016f);
            if (_hostSessionPingCooldown <= 0f)
            {
                SteamNetTransport.SendPing(PeerSteamId, Stopwatch.GetTimestamp());
                _hostSessionPingCooldown = SteamNetTransport.IsPeerSessionActive(PeerSteamId) ? 2.5f : 1.5f;
            }
        }

        if (HandshakeState == ModHandshakeState.SyncingMod && ActiveSyncWorkshopId != 0UL)
        {
            int pct = (int)Math.Round(SteamWorkshopManager.GetDownloadProgress(ActiveSyncWorkshopId) * 100f);
            StatusText = $"DOWNLOADING WORKSHOP MOD: {pct}%...";
            if (!SteamWorkshopManager.IsItemDownloading(ActiveSyncWorkshopId) &&
                SteamWorkshopManager.IsItemInstalled(ActiveSyncWorkshopId) &&
                !_completedWorkshopDownloadIds.Contains(ActiveSyncWorkshopId))
            {
                OnWorkshopItemReady(ActiveSyncWorkshopId, succeeded: true);
            }
            return;
        }

        if (HandshakeState == ModHandshakeState.SyncingMod &&
            ActiveSyncWorkshopId == 0UL &&
            !string.IsNullOrEmpty(_activeModRequestCharId) &&
            IsPrivateOrFriendsLobby &&
            CanSendP2PToPeer())
        {
            if (_modRequestAttempts == 0)
            {
                _modRequestAttempts = 1;
                _modRequestCooldown = 2.0f;
                SteamNetTransport.SendModScriptRequest(PeerSteamId, _activeModRequestCharId, _activeModRequestHash);
            }
            else if (_modRequestCooldown > 0f)
            {
                _modRequestCooldown = MathF.Max(0f, _modRequestCooldown - 0.016f);
                if (_modRequestCooldown <= 0f && _modRequestAttempts < 3)
                {
                    _modRequestAttempts++;
                    _modRequestCooldown = 2.0f;
                    SteamNetTransport.SendModScriptRequest(PeerSteamId, _activeModRequestCharId, _activeModRequestHash);
                }
                else if (_modRequestCooldown <= 0f && _modRequestAttempts >= 3)
                {
                    HandshakeState = ModHandshakeState.MismatchBlocked;
                    StatusText = $"MOD TRANSFER FAILED ({_activeModRequestCharId.ToUpperInvariant()})";
                }
            }
        }
    }

    public static bool TryRequestMatchStart()
    {
        if (!IsInMatchLobby || PeerSteamId == 0UL) return false;
        if (!LocalReady || !PeerReady || HandshakeState != ModHandshakeState.Verified) return false;

        if (!IsHost)
        {
            SetLocalSelection(
                LocalCharId,
                LocalScriptHash,
                LocalBaseScriptHash,
                ready: true,
                LocalWorkshopId,
                LocalPartnerCharId,
                LocalPartnerScriptHash,
                LocalPartnerBaseScriptHash,
                LocalPartnerWorkshopId);
            return false;
        }

        uint seed = unchecked((uint)DateTime.UtcNow.Ticks);
        if (seed == 0u) seed = DeterministicRng.DefaultSeed;

        SteamMatchmaking.SetLobbyData(_currentLobby, LobbyKeyState, "playing");
        SteamMatchmaking.SetLobbyData(_currentLobby, LobbyKeyMatchSeed, seed.ToString());
        SteamNetTransport.SendMatchStart(PeerSteamId, seed, OnlineMatchSession.DefaultInputDelayFrames, OnlineMatchSession.DefaultRollbackEnabled);

        State = LobbySearchState.Playing;
        PendingStart = new PendingOnlineMatchStart(seed, OnlineMatchSession.DefaultInputDelayFrames, OnlineMatchSession.DefaultRollbackEnabled);
        return true;
    }

    private static void OnLobbyMatchList(LobbyMatchList_t callback, bool ioFailure)
    {
        if (ioFailure)
        {
            State = LobbySearchState.Error;
            StatusText = "MATCHMAKING QUERY FAILED";
            return;
        }

        if (callback.m_nLobbiesMatching > 0)
        {
            CSteamID targetLobby = SteamMatchmaking.GetLobbyByIndex(0);
            StatusText = "JOINING MATCH LOBBY...";
            SteamMatchmaking.JoinLobby(targetLobby);
        }
        else
        {
            StatusText = "CREATING MATCH LOBBY...";
            SteamAPICall_t createCall = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
            _lobbyCreatedCall?.Set(createCall);
        }
    }

    private static void OnLobbyCreated(LobbyCreated_t callback, bool ioFailure)
    {
        if (ioFailure || callback.m_eResult != EResult.k_EResultOK)
        {
            State = LobbySearchState.Error;
            StatusText = "LOBBY CREATION FAILED";
            return;
        }

        _currentLobby = new CSteamID(callback.m_ulSteamIDLobby);
        IsHost = true;
        LocalSlot = 0;

        SteamMatchmaking.SetLobbyData(_currentLobby, LobbyKeyGame, LobbyGameId);
        SteamMatchmaking.SetLobbyData(_currentLobby, LobbyKeyMode, QueuedMode);
        SteamMatchmaking.SetLobbyData(_currentLobby, LobbyKeyModFilter, LobbyModFilter.ToString());
        SteamMatchmaking.SetLobbyData(_currentLobby, LobbyKeyGaEnabled, LobbyGaEnabled ? "1" : "0");
        SteamMatchmaking.SetLobbyData(_currentLobby, LobbyKeyVisibility, IsPrivateOrFriendsLobby ? "friends" : "public");
        SteamMatchmaking.SetLobbyData(_currentLobby, LobbyKeyState, "queuing");

        State = LobbySearchState.Hosting;
        StatusText = $"HOSTING {QueuedMode.ToUpperInvariant()} — WAITING FOR OPPONENT...";
    }

    private static void OnLobbyEntered(LobbyEnter_t callback)
    {
        if (callback.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            State = LobbySearchState.Error;
            StatusText = "FAILED TO ENTER LOBBY";
            return;
        }

        _currentLobby = new CSteamID(callback.m_ulSteamIDLobby);

        string remoteFilterStr = SteamMatchmaking.GetLobbyData(_currentLobby, LobbyKeyModFilter);
        string remoteGaStr = SteamMatchmaking.GetLobbyData(_currentLobby, LobbyKeyGaEnabled);
        string remoteMode = SteamMatchmaking.GetLobbyData(_currentLobby, LobbyKeyMode);

        if (!string.IsNullOrEmpty(remoteFilterStr) &&
            Enum.TryParse<ModFilterMode>(remoteFilterStr, out ModFilterMode remoteFilter))
        {
            bool remoteGa = string.Equals(remoteGaStr, "1", StringComparison.Ordinal);
            if (remoteFilter != ModManager.ActiveModFilter || remoteGa != BalanceOverrideStore.IsEnabled)
            {
                LeaveLobby();
                State = LobbySearchState.Error;
                StatusText = "MOD FILTER OR GA SETTING MISMATCH";
                return;
            }
            LobbyModFilter = remoteFilter;
            LobbyGaEnabled = remoteGa;
        }

        if (!string.IsNullOrEmpty(remoteMode))
            QueuedMode = remoteMode;

        CSteamID owner = SteamMatchmaking.GetLobbyOwner(_currentLobby);
        IsHost = owner.m_SteamID == SteamManager.LocalSteamId;
        LocalSlot = IsHost ? 0 : 1;

        string visibility = SteamMatchmaking.GetLobbyData(_currentLobby, LobbyKeyVisibility) ?? "";
        IsPrivateOrFriendsLobby =
            string.Equals(visibility, "private", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(visibility, "friends", StringComparison.OrdinalIgnoreCase) ||
            (_joiningViaFriendInvite && !string.Equals(visibility, "public", StringComparison.OrdinalIgnoreCase));
        _joiningViaFriendInvite = false;

        RefreshPeerFromLobby();
        if (PeerSteamId != 0UL)
        {
            State = LobbySearchState.MatchedInLobby;
            StatusText = $"MATCHED WITH {PeerPersonaName.ToUpperInvariant()}";
            ShouldEnterCharacterSelect = true;
            PullPeerMemberData();
            if (!string.IsNullOrEmpty(LocalCharId))
            {
                SetLocalSelection(
                    LocalCharId,
                    LocalScriptHash,
                    LocalBaseScriptHash,
                    LocalReady,
                    LocalWorkshopId,
                    LocalPartnerCharId,
                    LocalPartnerScriptHash,
                    LocalPartnerBaseScriptHash,
                    LocalPartnerWorkshopId);
            }
            else if (IsHost)
            {
                SteamNetTransport.SendPing(PeerSteamId, Stopwatch.GetTimestamp());
                _hostSessionPingCooldown = 1.5f;
            }
        }
        else
        {
            State = LobbySearchState.Hosting;
            StatusText = $"HOSTING {QueuedMode.ToUpperInvariant()} — WAITING FOR OPPONENT...";
        }
    }

    private static void OnLobbyDataUpdate(LobbyDataUpdate_t callback)
    {
        if (!IsInMatchLobby || callback.m_ulSteamIDLobby != CurrentLobbyId || callback.m_bSuccess == 0)
            return;

        if (callback.m_ulSteamIDMember != SteamManager.LocalSteamId && callback.m_ulSteamIDMember == PeerSteamId)
        {
            PullPeerMemberData();
        }
    }

    private static void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
    {
        if (!IsInMatchLobby || callback.m_ulSteamIDLobby != CurrentLobbyId)
            return;

        EChatMemberStateChange change = (EChatMemberStateChange)callback.m_rgfChatMemberStateChange;
        if ((change & EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
        {
            RefreshPeerFromLobby();
            if (PeerSteamId != 0UL)
            {
                State = LobbySearchState.MatchedInLobby;
                StatusText = $"OPPONENT JOINED: {PeerPersonaName.ToUpperInvariant()}";
                ShouldEnterCharacterSelect = true;
                if (IsHost)
                {
                    SteamMatchmaking.SetLobbyData(_currentLobby, LobbyKeyState, "selecting");
                    if (!string.IsNullOrEmpty(LocalCharId))
                    {
                        SetLocalSelection(
                            LocalCharId,
                            LocalScriptHash,
                            LocalBaseScriptHash,
                            LocalReady,
                            LocalWorkshopId,
                            LocalPartnerCharId,
                            LocalPartnerScriptHash,
                            LocalPartnerBaseScriptHash,
                            LocalPartnerWorkshopId);
                    }
                    else
                    {
                        SteamNetTransport.SendPing(PeerSteamId, Stopwatch.GetTimestamp());
                    }
                    _hostSessionPingCooldown = 1.5f;
                }
            }
        }
        else if ((change & (EChatMemberStateChange.k_EChatMemberStateChangeLeft |
                            EChatMemberStateChange.k_EChatMemberStateChangeDisconnected |
                            EChatMemberStateChange.k_EChatMemberStateChangeKicked |
                            EChatMemberStateChange.k_EChatMemberStateChangeBanned)) != 0)
        {
            if (callback.m_ulSteamIDUserChanged == PeerSteamId)
            {
                SteamNetTransport.CloseSessionWithPeer(PeerSteamId);
                PeerSteamId = 0UL;
                PeerPersonaName = "Opponent";
                ResetPeerHandshakeState();
                PendingStart = null;

                if (IsHost)
                {
                    SteamMatchmaking.SetLobbyData(_currentLobby, LobbyKeyState, "queuing");
                    State = LobbySearchState.Hosting;
                    StatusText = "OPPONENT LEFT — WAITING...";
                }
                else
                {
                    LeaveLobby();
                    StatusText = "HOST DISCONNECTED";
                }
            }
        }
    }

    private static void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        LeaveLobby();
        _joiningViaFriendInvite = true;
        State = LobbySearchState.Searching;
        StatusText = "JOINING FRIEND'S LOBBY...";
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private static void RefreshPeerFromLobby()
    {
        if (!IsInMatchLobby) return;

        int count = SteamMatchmaking.GetNumLobbyMembers(_currentLobby);
        for (int i = 0; i < count; i++)
        {
            CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobby, i);
            if (member.m_SteamID != 0UL && member.m_SteamID != SteamManager.LocalSteamId)
            {
                PeerSteamId = member.m_SteamID;
                string name = SteamFriends.GetFriendPersonaName(member);
                PeerPersonaName = string.IsNullOrWhiteSpace(name) ? $"Player {PeerSteamId}" : name.Trim();
                return;
            }
        }
    }

    private static void PullPeerMemberData()
    {
        if (!IsInMatchLobby || PeerSteamId == 0UL) return;

        CSteamID peer = new CSteamID(PeerSteamId);
        string charId = SteamMatchmaking.GetLobbyMemberData(_currentLobby, peer, MemberKeyCharId) ?? "";
        string scriptHash = SteamMatchmaking.GetLobbyMemberData(_currentLobby, peer, MemberKeyScriptHash) ?? "";
        string baseHash = SteamMatchmaking.GetLobbyMemberData(_currentLobby, peer, MemberKeyBaseHash) ?? "";
        bool ready = string.Equals(SteamMatchmaking.GetLobbyMemberData(_currentLobby, peer, MemberKeyReady), "1", StringComparison.Ordinal);
        string workshopStr = SteamMatchmaking.GetLobbyMemberData(_currentLobby, peer, MemberKeyWorkshopId) ?? "";
        ulong.TryParse(workshopStr, out ulong workshopId);

        string partnerCharId = SteamMatchmaking.GetLobbyMemberData(_currentLobby, peer, MemberKeyPartnerCharId) ?? "";
        string partnerScriptHash = SteamMatchmaking.GetLobbyMemberData(_currentLobby, peer, MemberKeyPartnerScriptHash) ?? "";
        string partnerBaseHash = SteamMatchmaking.GetLobbyMemberData(_currentLobby, peer, MemberKeyPartnerBaseHash) ?? "";
        string partnerWorkshopStr = SteamMatchmaking.GetLobbyMemberData(_currentLobby, peer, MemberKeyPartnerWorkshopId) ?? "";
        ulong.TryParse(partnerWorkshopStr, out ulong partnerWorkshopId);

        if (!string.IsNullOrEmpty(charId))
        {
            OnRemoteFighterSelection(
                PeerSteamId,
                charId,
                scriptHash,
                baseHash,
                ready,
                LobbyGaEnabled,
                workshopId,
                partnerCharId,
                partnerScriptHash,
                partnerBaseHash,
                partnerWorkshopId);
        }
    }

    public static void OnRemoteFighterSelection(
        ulong senderId,
        string charId,
        string scriptHash,
        string baseScriptHash,
        bool ready,
        bool gaEnabled,
        ulong workshopId = 0UL,
        string partnerCharId = "",
        string partnerScriptHash = "",
        string partnerBaseScriptHash = "",
        ulong partnerWorkshopId = 0UL)
    {
        if (senderId != PeerSteamId || string.IsNullOrEmpty(charId)) return;

        bool changedPrimary = !string.Equals(PeerCharId, charId, StringComparison.OrdinalIgnoreCase) ||
                              !string.Equals(PeerScriptHash, scriptHash, StringComparison.OrdinalIgnoreCase) ||
                              PeerWorkshopId != workshopId;

        bool changedPartner = !string.Equals(PeerPartnerCharId, partnerCharId, StringComparison.OrdinalIgnoreCase) ||
                              !string.Equals(PeerPartnerScriptHash, partnerScriptHash, StringComparison.OrdinalIgnoreCase) ||
                              PeerPartnerWorkshopId != partnerWorkshopId;

        PeerCharId = charId;
        PeerScriptHash = scriptHash;
        PeerBaseScriptHash = baseScriptHash;
        PeerReady = ready;
        PeerGaEnabled = gaEnabled;
        PeerWorkshopId = workshopId;
        PeerPartnerCharId = partnerCharId ?? "";
        PeerPartnerScriptHash = partnerScriptHash ?? "";
        PeerPartnerBaseScriptHash = partnerBaseScriptHash ?? "";
        PeerPartnerWorkshopId = partnerWorkshopId;

        if (changedPrimary)
        {
            PeerCustomScriptPath = null;
            PeerCustomBalanceGenome = null;
        }
        if (changedPartner)
        {
            PeerPartnerCustomScriptPath = null;
            PeerPartnerCustomBalanceGenome = null;
        }

        if (changedPrimary || changedPartner)
        {
            _activeModTransfer = null;
            _modRequestAttempts = 0;
            _modRequestCooldown = 0f;
            _pendingWorkshopDownloadId = 0UL;
            _completedWorkshopDownloadIds.Clear();
            EvaluateHandshake();
        }
    }

    private static bool TryMatchScriptHash(
        string? scriptPath,
        string charId,
        string expectedEffectiveHash,
        out CharacterBalanceGenome? matchedGenome)
    {
        matchedGenome = null;
        if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            return false;

        string localRaw = File.ReadAllText(scriptPath);
        string localBaseHash = ModManager.ComputeStringHash(localRaw);
        string localEffectiveHash = localBaseHash;

        if (BalanceOverrideStore.IsEnabled &&
            BalanceOverrideStore.TryGetGenome(charId, localBaseHash, out CharacterBalanceGenome? genome) &&
            genome != null)
        {
            localEffectiveHash = $"{localBaseHash}_ga_{genome.ComputeCompactSignature()}";
            matchedGenome = genome;
        }

        return string.Equals(localEffectiveHash, expectedEffectiveHash, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EvaluateSingleFighterSync(
        string charId,
        string expectedHash,
        ulong workshopId,
        bool isPartner)
    {
        if (workshopId != 0UL)
        {
            SteamWorkshopManager.TryEnsureInstalledItemRegistered(workshopId);

            string? workshopScript = ModManager.ResolveCharacterScriptForWorkshop(workshopId, charId)
                                     ?? ModManager.ResolveCharacterScript(charId);

            if (TryMatchScriptHash(workshopScript, charId, expectedHash, out CharacterBalanceGenome? matchedGenome))
            {
                bool matchesDefault = string.Equals(ModManager.ResolveCharacterScript(charId), workshopScript, StringComparison.OrdinalIgnoreCase);
                if (!isPartner)
                {
                    PeerCustomScriptPath = matchesDefault ? null : workshopScript;
                    PeerCustomBalanceGenome = matchesDefault ? null : matchedGenome;
                }
                else
                {
                    PeerPartnerCustomScriptPath = matchesDefault ? null : workshopScript;
                    PeerPartnerCustomBalanceGenome = matchesDefault ? null : matchedGenome;
                }
                return true;
            }

            if (_completedWorkshopDownloadIds.Contains(workshopId))
            {
                HandshakeState = ModHandshakeState.MismatchBlocked;
                StatusText = $"WORKSHOP MOD HASH MISMATCH ({charId.ToUpperInvariant()})";
                return false;
            }

            HandshakeState = ModHandshakeState.SyncingMod;
            ActiveSyncWorkshopId = workshopId;
            if (_pendingWorkshopDownloadId != workshopId)
            {
                _pendingWorkshopDownloadId = workshopId;
                bool started = SteamWorkshopManager.DownloadWorkshopItem(workshopId, subscribe: true);
                if (!started && !SteamWorkshopManager.IsItemDownloading(workshopId))
                {
                    HandshakeState = ModHandshakeState.MismatchBlocked;
                    StatusText = $"WORKSHOP DOWNLOAD FAILED ({workshopId})";
                    return false;
                }
            }

            int pct = (int)Math.Round(SteamWorkshopManager.GetDownloadProgress(workshopId) * 100f);
            StatusText = $"DOWNLOADING WORKSHOP MOD: {pct}%...";
            return false;
        }

        string? localScriptPath = ModManager.ResolveCharacterScript(charId);
        if (TryMatchScriptHash(localScriptPath, charId, expectedHash, out _))
        {
            if (!isPartner)
            {
                PeerCustomScriptPath = null;
                PeerCustomBalanceGenome = null;
            }
            else
            {
                PeerPartnerCustomScriptPath = null;
                PeerPartnerCustomBalanceGenome = null;
            }
            return true;
        }

        string? existingNetPath = isPartner ? PeerPartnerCustomScriptPath : PeerCustomScriptPath;
        if (!string.IsNullOrEmpty(existingNetPath) &&
            TryMatchScriptHash(existingNetPath, charId, expectedHash, out _))
        {
            return true;
        }

        if (!IsPrivateOrFriendsLobby)
        {
            HandshakeState = ModHandshakeState.MismatchBlocked;
            StatusText = "MOD MUST BE UPLOADED TO STEAM WORKSHOP";
            return false;
        }

        HandshakeState = ModHandshakeState.SyncingMod;
        ActiveSyncWorkshopId = 0UL;
        _activeModRequestIsPartner = isPartner;
        _activeModRequestCharId = charId;
        _activeModRequestHash = expectedHash;
        StatusText = $"SYNCING MOD: {charId.ToUpperInvariant()}...";

        if (CanSendP2PToPeer())
        {
            _modRequestAttempts = 1;
            _modRequestCooldown = 2.0f;
            SteamNetTransport.SendModScriptRequest(PeerSteamId, charId, expectedHash);
        }
        else
        {
            _modRequestAttempts = 0;
            _modRequestCooldown = 0f;
        }

        return false;
    }

    private static void EvaluateHandshake()
    {
        ActiveSyncWorkshopId = 0UL;
        _activeModRequestIsPartner = false;
        _activeModRequestCharId = "";
        _activeModRequestHash = "";

        if (PeerSteamId == 0UL || string.IsNullOrWhiteSpace(PeerCharId))
        {
            HandshakeState = ModHandshakeState.WaitingForPeer;
            return;
        }

        if (PeerGaEnabled != BalanceOverrideStore.IsEnabled)
        {
            HandshakeState = ModHandshakeState.MismatchBlocked;
            StatusText = "GA BALANCE TOGGLE MISMATCH";
            return;
        }

        if (!EvaluateSingleFighterSync(PeerCharId, PeerScriptHash, PeerWorkshopId, isPartner: false))
            return;

        if (Is2v2Lobby)
        {
            if (string.IsNullOrWhiteSpace(PeerPartnerCharId))
            {
                HandshakeState = ModHandshakeState.WaitingForPeer;
                StatusText = "WAITING FOR OPPONENT PARTNER...";
                return;
            }

            if (!EvaluateSingleFighterSync(PeerPartnerCharId, PeerPartnerScriptHash, PeerPartnerWorkshopId, isPartner: true))
                return;
        }

        HandshakeState = ModHandshakeState.Verified;
        StatusText = Is2v2Lobby
            ? $"{PeerCharId.ToUpperInvariant()} + {PeerPartnerCharId.ToUpperInvariant()} VERIFIED"
            : $"{PeerCharId.ToUpperInvariant()} HASH VERIFIED";
    }

    public static void OnWorkshopItemReady(ulong workshopId, bool succeeded)
    {
        bool relevant = workshopId == PeerWorkshopId ||
                        (Is2v2Lobby && workshopId == PeerPartnerWorkshopId) ||
                        workshopId == ActiveSyncWorkshopId;
        if (!IsInMatchLobby || PeerSteamId == 0UL || !relevant) return;

        if (_pendingWorkshopDownloadId == workshopId)
            _pendingWorkshopDownloadId = 0UL;

        if (!succeeded)
        {
            HandshakeState = ModHandshakeState.MismatchBlocked;
            StatusText = $"WORKSHOP DOWNLOAD FAILED ({workshopId})";
            return;
        }

        _completedWorkshopDownloadIds.Add(workshopId);
        EvaluateHandshake();

        if (HandshakeState == ModHandshakeState.Verified &&
            EngineServices.Current != null &&
            GameState.CurrentState == global::State.ChoiceMenu)
        {
            InstantiatePeerCharactersInSelect(EngineServices.Current);
        }
    }

    private static void InstantiatePeerCharactersInSelect(EngineServices services)
    {
        services.Lua.ClearCharacterCache();
        Player? opp = PeerSlot == 0 ? services.Player1 : services.Player2;
        if (opp == null || string.IsNullOrEmpty(PeerCharId)) return;

        Character primaryLoaded;
        if (!string.IsNullOrEmpty(PeerCustomScriptPath) && File.Exists(PeerCustomScriptPath))
        {
            bool applyStored = PeerCustomBalanceGenome == null && BalanceOverrideStore.IsEnabled;
            primaryLoaded = services.Lua.CreateCharacterFromPath(PeerCustomScriptPath, PeerSlot, applyStoredBalance: applyStored);
            if (PeerCustomBalanceGenome != null)
                CharacterLoader.ApplyGenome(primaryLoaded, PeerCustomBalanceGenome);
        }
        else
        {
            primaryLoaded = services.Lua.CreateCharacter(PeerCharId, slot: PeerSlot);
        }

        Character? partnerLoaded = opp.PartnerCharacter;
        if (Is2v2Lobby && !string.IsNullOrEmpty(PeerPartnerCharId))
        {
            if (!string.IsNullOrEmpty(PeerPartnerCustomScriptPath) && File.Exists(PeerPartnerCustomScriptPath))
            {
                bool applyPartnerStored = PeerPartnerCustomBalanceGenome == null && BalanceOverrideStore.IsEnabled;
                partnerLoaded = services.Lua.CreateCharacterFromPath(PeerPartnerCustomScriptPath, PeerSlot + 2, applyStoredBalance: applyPartnerStored);
                if (PeerPartnerCustomBalanceGenome != null)
                    CharacterLoader.ApplyGenome(partnerLoaded, PeerPartnerCustomBalanceGenome);
            }
            else
            {
                partnerLoaded = services.Lua.CreateCharacter(PeerPartnerCharId, slot: PeerSlot + 2);
            }
        }

        opp.SetTeam(primaryLoaded, partnerLoaded);
    }

    public static void OnModScriptRequestReceived(ulong senderId, string charId, string expectedHash)
    {
        if (senderId != PeerSteamId || string.IsNullOrWhiteSpace(charId)) return;

        string? localPath = ModManager.ResolveCharacterScript(charId);
        if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath)) return;

        string rawLua = File.ReadAllText(localPath);
        string baseHash = ModManager.ComputeStringHash(rawLua);
        string effectiveHash = baseHash;
        string? balanceJson = null;

        if (BalanceOverrideStore.IsEnabled &&
            BalanceOverrideStore.TryGetGenome(charId, baseHash, out CharacterBalanceGenome? genome) &&
            genome != null)
        {
            effectiveHash = $"{baseHash}_ga_{genome.ComputeCompactSignature()}";
            balanceJson = JsonSerializer.Serialize(genome);
        }

        if (!string.IsNullOrEmpty(expectedHash) &&
            !string.Equals(effectiveHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            // Still send current version; receiver verifies
        }

        SteamNetTransport.SendModScriptBundle(senderId, charId, effectiveHash, baseHash, rawLua, balanceJson);
    }

    public static void OnModScriptChunkReceived(
        ulong senderId,
        int transferId,
        string charId,
        ushort chunkIdx,
        ushort totalChunks,
        byte[] bytes,
        EngineServices? services)
    {
        if (senderId != PeerSteamId || totalChunks == 0 || chunkIdx >= totalChunks)
            return;

        if (_activeModTransfer == null || _activeModTransfer.TransferId != transferId)
        {
            _activeModTransfer = new IncomingModTransfer
            {
                TransferId = transferId,
                CharId = charId,
                TotalChunks = totalChunks,
                Chunks = new byte[totalChunks][],
                ReceivedCount = 0
            };
        }

        if (_activeModTransfer.Chunks[chunkIdx] == null)
        {
            _activeModTransfer.Chunks[chunkIdx] = bytes;
            _activeModTransfer.ReceivedCount++;
        }

        if (_activeModTransfer.ReceivedCount < _activeModTransfer.TotalChunks)
            return;

        try
        {
            using var joined = new MemoryStream();
            for (int i = 0; i < _activeModTransfer.TotalChunks; i++)
            {
                byte[] part = _activeModTransfer.Chunks[i];
                if (part != null) joined.Write(part, 0, part.Length);
            }
            joined.Position = 0;

            using var gzip = new GZipStream(joined, CompressionMode.Decompress);
            using var br = new BinaryReader(gzip, Encoding.UTF8);

            string rxCharId = br.ReadString();
            string rxEffectiveHash = br.ReadString();
            string rxBaseHash = br.ReadString();
            string rxLuaSource = br.ReadString();
            bool hasBalance = br.ReadBoolean();
            string? rxBalanceJson = hasBalance ? br.ReadString() : null;

            string computedBase = ModManager.ComputeStringHash(rxLuaSource);
            string computedEffective = computedBase;
            CharacterBalanceGenome? rxGenome = null;

            if (!string.IsNullOrEmpty(rxBalanceJson))
            {
                rxGenome = JsonSerializer.Deserialize<CharacterBalanceGenome>(rxBalanceJson);
                if (rxGenome != null)
                {
                    computedEffective = $"{computedBase}_ga_{rxGenome.ComputeCompactSignature()}";
                }
            }

            bool isPartner = _activeModRequestIsPartner ||
                             (Is2v2Lobby &&
                              string.Equals(rxCharId, PeerPartnerCharId, StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(rxCharId, PeerCharId, StringComparison.OrdinalIgnoreCase));
            string expectedPeerHash = isPartner ? PeerPartnerScriptHash : PeerScriptHash;

            bool hashValid = string.Equals(computedEffective, rxEffectiveHash, StringComparison.OrdinalIgnoreCase) &&
                             (string.IsNullOrEmpty(expectedPeerHash) ||
                              string.Equals(computedEffective, expectedPeerHash, StringComparison.OrdinalIgnoreCase));

            if (!hashValid)
            {
                HandshakeState = ModHandshakeState.MismatchBlocked;
                StatusText = $"MOD HASH MISMATCH ({rxCharId.ToUpperInvariant()})";
                SteamNetTransport.SendModScriptAck(senderId, rxCharId, rxEffectiveHash, accepted: false);
                return;
            }

            string netModsDir = Path.Combine(ModManager.CacheDir, "NetMods");
            Directory.CreateDirectory(netModsDir);
            string safeChar = SanitizeId(rxCharId);
            string shortHash = computedEffective.Length >= 8 ? computedEffective[..8] : computedEffective;
            string destPath = Path.Combine(netModsDir, $"{safeChar}_{shortHash}.lua");
            File.WriteAllText(destPath, rxLuaSource);

            if (isPartner)
            {
                PeerPartnerCustomScriptPath = destPath;
                PeerPartnerCustomBalanceGenome = rxGenome;
            }
            else
            {
                PeerCustomScriptPath = destPath;
                PeerCustomBalanceGenome = rxGenome;
            }

            EvaluateHandshake();
            if (HandshakeState == ModHandshakeState.Verified)
            {
                StatusText = $"{rxCharId.ToUpperInvariant()} MOD SYNCED & VERIFIED";
            }

            // Pre-instantiate into opponent slot immediately if inside CharacterSelect
            if (services != null && GameState.CurrentState == global::State.ChoiceMenu)
            {
                InstantiatePeerCharactersInSelect(services);
            }

            SteamNetTransport.SendModScriptAck(senderId, rxCharId, rxEffectiveHash, accepted: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SteamLobbyManager] Failed to receive mod bundle: {ex.Message}");
            HandshakeState = ModHandshakeState.MismatchBlocked;
            StatusText = "MOD TRANSFER ERROR";
        }
        finally
        {
            _activeModTransfer = null;
        }
    }

    public static void OnModScriptAckReceived(ulong senderId, string charId, string scriptHash, bool accepted)
    {
        if (senderId != PeerSteamId) return;
        if (!accepted)
        {
            StatusText = $"PEER REJECTED {charId.ToUpperInvariant()}";
        }
    }

    public static void OnMatchStartReceived(ulong senderId, uint matchSeed, int inputDelayFrames, bool rollbackEnabled)
    {
        if (senderId != PeerSteamId) return;
        if (HandshakeState != ModHandshakeState.Verified) return;

        State = LobbySearchState.Playing;
        PendingStart = new PendingOnlineMatchStart(matchSeed, inputDelayFrames, rollbackEnabled);
    }

    private static string SanitizeId(string id)
    {
        string lower = id.Trim().ToLowerInvariant();
        foreach (char c in Path.GetInvalidFileNameChars())
            lower = lower.Replace(c, '_');
        return lower;
    }
}
