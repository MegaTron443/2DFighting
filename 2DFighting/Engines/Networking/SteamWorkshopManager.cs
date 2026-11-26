using System;
using System.Collections.Generic;
using System.IO;
using Steamworks;

public static class SteamWorkshopManager
{
    private static Callback<ItemInstalled_t>? _itemInstalledCallback;
    private static Callback<DownloadItemResult_t>? _downloadItemResultCallback;
    private static readonly HashSet<ulong> _subscribedIds = new();
    private static readonly HashSet<ulong> _activeDownloads = new();

    public static IReadOnlyCollection<ulong> SubscribedIds => _subscribedIds;

    public static void Initialize()
    {
        if (!SteamManager.IsInitialized) return;

        _itemInstalledCallback = Callback<ItemInstalled_t>.Create(OnItemInstalled);
        _downloadItemResultCallback = Callback<DownloadItemResult_t>.Create(OnDownloadItemResult);
        RefreshSubscribedItems();
    }

    public static void Shutdown()
    {
        _itemInstalledCallback?.Dispose();
        _itemInstalledCallback = null;
        _downloadItemResultCallback?.Dispose();
        _downloadItemResultCallback = null;
        _subscribedIds.Clear();
        _activeDownloads.Clear();
    }

    public static void RefreshSubscribedItems()
    {
        if (!SteamManager.IsInitialized) return;

        _subscribedIds.Clear();
        uint count = SteamUGC.GetNumSubscribedItems();
        if (count == 0) return;

        var items = new PublishedFileId_t[count];
        uint fetched = SteamUGC.GetSubscribedItems(items, count);
        for (uint i = 0; i < fetched; i++)
        {
            ulong id = items[i].m_PublishedFileId;
            if (id == 0UL) continue;
            _subscribedIds.Add(id);
            TryEnsureInstalledItemRegistered(id);
        }
    }

    public static bool IsItemInstalled(ulong publishedFileId)
    {
        if (!SteamManager.IsInitialized || publishedFileId == 0UL) return false;
        uint state = SteamUGC.GetItemState(new PublishedFileId_t(publishedFileId));
        return (state & (uint)EItemState.k_EItemStateInstalled) != 0;
    }

    public static bool IsItemDownloading(ulong publishedFileId)
    {
        if (!SteamManager.IsInitialized || publishedFileId == 0UL) return false;
        uint state = SteamUGC.GetItemState(new PublishedFileId_t(publishedFileId));
        bool ugcDownloading = (state & ((uint)EItemState.k_EItemStateDownloading | (uint)EItemState.k_EItemStateDownloadPending)) != 0;
        return ugcDownloading || _activeDownloads.Contains(publishedFileId);
    }

    public static bool TryEnsureInstalledItemRegistered(ulong publishedFileId)
    {
        if (!SteamManager.IsInitialized || publishedFileId == 0UL) return false;

        var fileId = new PublishedFileId_t(publishedFileId);
        uint state = SteamUGC.GetItemState(fileId);
        if ((state & (uint)EItemState.k_EItemStateInstalled) == 0)
            return false;

        if (SteamUGC.GetItemInstallInfo(fileId, out _, out string folder, 2048, out _) &&
            !string.IsNullOrWhiteSpace(folder) &&
            Directory.Exists(folder))
        {
            return ModManager.RegisterWorkshopDirectory(publishedFileId, folder);
        }

        return false;
    }

    public static bool DownloadWorkshopItem(ulong publishedFileId, bool subscribe)
    {
        if (!SteamManager.IsInitialized || publishedFileId == 0UL) return false;

        var fileId = new PublishedFileId_t(publishedFileId);
        if (subscribe)
        {
            SteamUGC.SubscribeItem(fileId);
            _subscribedIds.Add(publishedFileId);
        }

        bool started = SteamUGC.DownloadItem(fileId, bHighPriority: true);
        if (started)
        {
            _activeDownloads.Add(publishedFileId);
        }
        return started;
    }

    public static float GetDownloadProgress(ulong publishedFileId)
    {
        if (!SteamManager.IsInitialized || publishedFileId == 0UL) return 0f;

        var fileId = new PublishedFileId_t(publishedFileId);
        if (SteamUGC.GetItemDownloadInfo(fileId, out ulong downloaded, out ulong total) && total > 0UL)
        {
            return Math.Clamp((float)((double)downloaded / total), 0f, 1f);
        }

        if (IsItemInstalled(publishedFileId) && !IsItemDownloading(publishedFileId))
            return 1f;

        return 0f;
    }

    private static void OnItemInstalled(ItemInstalled_t callback)
    {
        if (callback.m_unAppID != SteamUtils.GetAppID()) return;

        ulong id = callback.m_nPublishedFileId.m_PublishedFileId;
        _activeDownloads.Remove(id);
        bool registered = TryEnsureInstalledItemRegistered(id);
        Console.WriteLine($"[SteamWorkshopManager] Item installed: {id} (registered={registered})");
        SteamLobbyManager.OnWorkshopItemReady(id, succeeded: registered);
    }

    private static void OnDownloadItemResult(DownloadItemResult_t callback)
    {
        if (callback.m_unAppID != SteamUtils.GetAppID()) return;

        ulong id = callback.m_nPublishedFileId.m_PublishedFileId;
        _activeDownloads.Remove(id);

        if (callback.m_eResult == EResult.k_EResultOK)
        {
            bool registered = TryEnsureInstalledItemRegistered(id);
            Console.WriteLine($"[SteamWorkshopManager] Item downloaded: {id} (registered={registered})");
            SteamLobbyManager.OnWorkshopItemReady(id, succeeded: registered);
        }
        else
        {
            Console.WriteLine($"[SteamWorkshopManager] Download failed for item {id}: {callback.m_eResult}");
            SteamLobbyManager.OnWorkshopItemReady(id, succeeded: false);
        }
    }
}
