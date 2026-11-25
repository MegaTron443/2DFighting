using System;
using System.Collections.Generic;
using Steamworks;

public static class SteamLeaderboardSync
{
    private readonly record struct PendingUpload(
        string BoardName,
        int Score,
        ELeaderboardUploadScoreMethod UploadMethod);

    private static readonly object _queueLock = new();
    private static readonly Queue<PendingUpload> _pendingUploads = new();
    private static readonly Dictionary<string, SteamLeaderboard_t> _boardHandles = new(StringComparer.Ordinal);

    private static CallResult<LeaderboardFindResult_t>? _findResult;
    private static CallResult<LeaderboardScoreUploaded_t>? _uploadResult;
    private static PendingUpload? _inFlight;
    private static bool _waitingForFind;
    private static bool _waitingForUpload;

    public static void Initialize()
    {
        if (!SteamManager.IsInitialized) return;

        _findResult = CallResult<LeaderboardFindResult_t>.Create(OnLeaderboardFound);
        _uploadResult = CallResult<LeaderboardScoreUploaded_t>.Create(OnScoreUploaded);
    }

    public static void Shutdown()
    {
        lock (_queueLock)
        {
            _pendingUploads.Clear();
            _boardHandles.Clear();
            _inFlight = null;
            _waitingForFind = false;
            _waitingForUpload = false;
        }

        _findResult?.Dispose();
        _findResult = null;
        _uploadResult?.Dispose();
        _uploadResult = null;
    }

    public static void EnqueueIfLocalPlayer(LeaderboardEntry? entry)
    {
        if (entry == null || !SteamManager.IsInitialized) return;
        if (entry.SteamId == 0UL || entry.SteamId != SteamManager.LocalSteamId) return;

        string filterTag = entry.FilterMode.ToString();
        lock (_queueLock)
        {
            _pendingUploads.Enqueue(new PendingUpload(
                $"2DFighting_{filterTag}_Elo",
                entry.Rating,
                ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodForceUpdate));

            _pendingUploads.Enqueue(new PendingUpload(
                $"2DFighting_{filterTag}_BestCombo",
                entry.BestCombo,
                ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest));

            _pendingUploads.Enqueue(new PendingUpload(
                $"2DFighting_{filterTag}_Wins",
                entry.Wins,
                ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodForceUpdate));
        }
    }

    public static void Pump()
    {
        if (!SteamManager.IsInitialized) return;

        PendingUpload next;
        SteamLeaderboard_t cachedBoard = default;
        bool hasCachedBoard = false;

        lock (_queueLock)
        {
            if (_inFlight.HasValue || _waitingForFind || _waitingForUpload || _pendingUploads.Count == 0)
                return;

            next = _pendingUploads.Dequeue();
            _inFlight = next;
            hasCachedBoard = _boardHandles.TryGetValue(next.BoardName, out cachedBoard) && cachedBoard.m_SteamLeaderboard != 0UL;
            if (hasCachedBoard)
                _waitingForUpload = true;
            else
                _waitingForFind = true;
        }

        if (hasCachedBoard)
        {
            SteamAPICall_t uploadCall = SteamUserStats.UploadLeaderboardScore(
                cachedBoard,
                next.UploadMethod,
                next.Score,
                null,
                0);
            _uploadResult?.Set(uploadCall);
        }
        else
        {
            SteamAPICall_t findCall = SteamUserStats.FindOrCreateLeaderboard(
                next.BoardName,
                ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending,
                ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric);
            _findResult?.Set(findCall);
        }
    }

    private static void OnLeaderboardFound(LeaderboardFindResult_t callback, bool ioFailure)
    {
        PendingUpload? current;
        lock (_queueLock)
        {
            _waitingForFind = false;
            current = _inFlight;
            if (ioFailure || callback.m_bLeaderboardFound == 0 || !current.HasValue)
            {
                _inFlight = null;
                return;
            }

            _boardHandles[current.Value.BoardName] = callback.m_hSteamLeaderboard;
            _waitingForUpload = true;
        }

        SteamAPICall_t uploadCall = SteamUserStats.UploadLeaderboardScore(
            callback.m_hSteamLeaderboard,
            current.Value.UploadMethod,
            current.Value.Score,
            null,
            0);
        _uploadResult?.Set(uploadCall);
    }

    private static void OnScoreUploaded(LeaderboardScoreUploaded_t callback, bool ioFailure)
    {
        lock (_queueLock)
        {
            _waitingForUpload = false;
            _inFlight = null;
        }
        _ = callback;
        _ = ioFailure;
    }
}
