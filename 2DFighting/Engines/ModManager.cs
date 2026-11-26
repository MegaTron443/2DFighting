using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

public enum ModFilterMode
{
    VanillaOnly,
    VanillaAndMods,
    ModsOnly
}

public static class ModManager
{
    public static string BaseDir => AppDomain.CurrentDomain.BaseDirectory;
    public static string ModsDir => Path.Combine(BaseDir, "Mods");
    public static string CacheDir => Path.Combine(BaseDir, "Cache");
    public static string ExtractedModsCacheDir => Path.Combine(CacheDir, "Mods");

    private static readonly List<string> _contentRoots = new();
    private static readonly Dictionary<string, ulong> _rootWorkshopIds = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<ulong, string> _workshopRoots = new();

    private static readonly Dictionary<string, string> _characterScripts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> _vanillaCharacterScripts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> _modCharacterScripts = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ulong> _characterWorkshopIds = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<ulong, List<string>> _workshopCharacters = new();

    private static readonly Dictionary<ModFilterMode, string[]> _filteredFightersCache = new();
    private static int _filteredCacheVersion = -1;

    private static ModFilterMode _activeModFilter = ModFilterMode.VanillaAndMods;

    public static IReadOnlyList<string> ContentRoots => _contentRoots;
    public static IReadOnlyDictionary<string, string> CharacterScripts => _characterScripts;
    public static string[] AvailableFighters { get; private set; } = Array.Empty<string>();
    public static int CatalogVersion { get; private set; } = 0;

    public static ModFilterMode ActiveModFilter
    {
        get => _activeModFilter;
        set
        {
            if (_activeModFilter != value)
            {
                _activeModFilter = value;
                CatalogVersion++;
            }
        }
    }

    public static void Initialize()
    {
        Directory.CreateDirectory(ModsDir);
        Directory.CreateDirectory(CacheDir);
        Directory.CreateDirectory(ExtractedModsCacheDir);

        _contentRoots.Clear();
        _rootWorkshopIds.Clear();
        _workshopRoots.Clear();
        _characterScripts.Clear();
        _vanillaCharacterScripts.Clear();
        _modCharacterScripts.Clear();
        _characterWorkshopIds.Clear();
        _workshopCharacters.Clear();

        foreach (string zipPath in Directory.GetFiles(ModsDir, "*.zip", SearchOption.TopDirectoryOnly))
        {
            SyncZipMod(zipPath);
        }

        foreach (string dir in Directory.GetDirectories(ModsDir))
        {
            string dirName = Path.GetFileName(dir);
            if (dirName.Equals("Assets", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("Scripts", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("Characters", StringComparison.OrdinalIgnoreCase))
            {
                if (!_contentRoots.Contains(ModsDir))
                    _contentRoots.Add(ModsDir);
            }
            else
            {
                _contentRoots.Add(dir);
            }
        }

        _contentRoots.Add(BaseDir);

        DiscoverCharacters();

        if (SteamManager.IsInitialized)
        {
            SteamWorkshopManager.RefreshSubscribedItems();
        }
    }

    private static void SyncZipMod(string zipPath)
    {
        try
        {
            string modName = Path.GetFileNameWithoutExtension(zipPath);
            string extractDir = Path.Combine(ExtractedModsCacheDir, modName);
            string hashFile = Path.Combine(extractDir, ".modhash");

            string currentHash = ComputeFileHash(zipPath);
            bool needsExtract = true;

            if (Directory.Exists(extractDir) && File.Exists(hashFile))
            {
                string cachedHash = File.ReadAllText(hashFile).Trim();
                if (string.Equals(cachedHash, currentHash, StringComparison.OrdinalIgnoreCase))
                {
                    needsExtract = false;
                }
            }

            if (needsExtract)
            {
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, recursive: true);

                Directory.CreateDirectory(extractDir);
                ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);
                File.WriteAllText(hashFile, currentHash);

                DiagnosticsLogger.LogSilentIssueOnce(
                    $"mod_extracted_{modName}",
                    $"Extracted mod archive '{Path.GetFileName(zipPath)}' to Cache/Mods/{modName} (Hash: {currentHash[..8]}).",
                    LogSeverity.Info
                );
            }

            // Handle zips that have a single top-level wrapper folder (e.g. MyMod.zip/MyMod/Scripts/...)
            string effectiveRoot = ResolveEffectiveModRoot(extractDir);
            _contentRoots.Add(effectiveRoot);
        }
        catch (Exception ex)
        {
            DiagnosticsLogger.LogSilentIssueOnce(
                $"mod_zip_err_{zipPath}",
                $"Failed to load mod zip '{Path.GetFileName(zipPath)}': {ex.Message}",
                LogSeverity.Error
            );
        }
    }

    private static string ResolveEffectiveModRoot(string extractDir)
    {
        if (Directory.Exists(Path.Combine(extractDir, "Scripts")) ||
            Directory.Exists(Path.Combine(extractDir, "Assets")) ||
            Directory.Exists(Path.Combine(extractDir, "Characters")))
        {
            return extractDir;
        }

        string[] subDirs = Directory.GetDirectories(extractDir);
        if (subDirs.Length == 1)
        {
            return subDirs[0];
        }

        return extractDir;
    }

    private static void DiscoverCharacters()
    {
        _characterScripts.Clear();
        _vanillaCharacterScripts.Clear();
        _modCharacterScripts.Clear();
        _characterWorkshopIds.Clear();
        _workshopCharacters.Clear();

        for (int i = _contentRoots.Count - 1; i >= 0; i--)
        {
            string root = _contentRoots[i];
            bool isVanillaRoot = root.Equals(BaseDir, StringComparison.OrdinalIgnoreCase);
            ulong workshopId = (!isVanillaRoot && _rootWorkshopIds.TryGetValue(root, out ulong wid)) ? wid : 0UL;

            string[] scriptFolders =
            {
                Path.Combine(root, "Scripts", "characters"),
                Path.Combine(root, "Scripts", "Characters"),
                Path.Combine(root, "Scripts", "Chars"),
                Path.Combine(root, "Scripts", "lua")
            };

            foreach (string folder in scriptFolders)
            {
                if (!Directory.Exists(folder)) continue;

                foreach (string luaFile in Directory.GetFiles(folder, "*.lua"))
                {
                    string id = Path.GetFileNameWithoutExtension(luaFile).ToLowerInvariant();
                    _characterScripts[id] = luaFile;
                    if (isVanillaRoot)
                    {
                        _vanillaCharacterScripts[id] = luaFile;
                        _characterWorkshopIds[id] = 0UL;
                    }
                    else
                    {
                        _modCharacterScripts[id] = luaFile;
                        _characterWorkshopIds[id] = workshopId;
                    }

                    if (workshopId != 0UL)
                    {
                        if (!_workshopCharacters.TryGetValue(workshopId, out List<string>? charList))
                        {
                            charList = new List<string>();
                            _workshopCharacters[workshopId] = charList;
                        }
                        if (!charList.Contains(id, StringComparer.OrdinalIgnoreCase))
                            charList.Add(id);
                    }
                }
            }
        }

        AvailableFighters = _characterScripts.Keys
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        CatalogVersion++;
    }

    public static bool RegisterWorkshopDirectory(ulong workshopId, string installFolder)
    {
        if (workshopId == 0UL || string.IsNullOrWhiteSpace(installFolder) || !Directory.Exists(installFolder))
            return false;

        string effectiveRoot = ResolveEffectiveModRoot(installFolder);
        if (_workshopRoots.TryGetValue(workshopId, out string? oldRoot) &&
            !string.Equals(oldRoot, effectiveRoot, StringComparison.OrdinalIgnoreCase))
        {
            _contentRoots.RemoveAll(r => string.Equals(r, oldRoot, StringComparison.OrdinalIgnoreCase));
            _rootWorkshopIds.Remove(oldRoot);
        }

        if (!_contentRoots.Contains(effectiveRoot, StringComparer.OrdinalIgnoreCase))
        {
            _contentRoots.Insert(0, effectiveRoot);
        }

        _rootWorkshopIds[effectiveRoot] = workshopId;
        _workshopRoots[workshopId] = effectiveRoot;
        DiscoverCharacters();
        return true;
    }

    public static ulong GetCharacterWorkshopId(string characterId) =>
        GetCharacterWorkshopId(characterId, _activeModFilter);

    public static ulong GetCharacterWorkshopId(string characterId, ModFilterMode filter)
    {
        if (string.IsNullOrWhiteSpace(characterId) || filter == ModFilterMode.VanillaOnly)
            return 0UL;

        return _characterWorkshopIds.TryGetValue(characterId, out ulong wid) ? wid : 0UL;
    }

    public static ulong GetWorkshopIdForScriptPath(string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath)) return 0UL;

        string fullScript = Path.GetFullPath(scriptPath);
        foreach (var kvp in _rootWorkshopIds)
        {
            string fullRoot = Path.GetFullPath(kvp.Key).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                              + Path.DirectorySeparatorChar;
            if (fullScript.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }
        return 0UL;
    }

    public static bool TryGetWorkshopRoot(ulong workshopId, out string? rootPath)
    {
        return _workshopRoots.TryGetValue(workshopId, out rootPath);
    }

    public static string? ResolveCharacterScriptForWorkshop(ulong workshopId, string? characterId = null)
    {
        if (workshopId == 0UL || !_workshopRoots.TryGetValue(workshopId, out string? root) || string.IsNullOrWhiteSpace(root))
            return null;

        string[] scriptFolders =
        {
            Path.Combine(root, "Scripts", "characters"),
            Path.Combine(root, "Scripts", "Characters"),
            Path.Combine(root, "Scripts", "Chars"),
            Path.Combine(root, "Scripts", "lua")
        };

        foreach (string folder in scriptFolders)
        {
            if (!Directory.Exists(folder)) continue;

            foreach (string luaFile in Directory.GetFiles(folder, "*.lua"))
            {
                string id = Path.GetFileNameWithoutExtension(luaFile);
                if (string.IsNullOrWhiteSpace(characterId) ||
                    string.Equals(id, characterId, StringComparison.OrdinalIgnoreCase))
                {
                    return luaFile;
                }
            }
        }

        return null;
    }

    public static string[] GetFilteredFighters(ModFilterMode filter)
    {
        if (_filteredCacheVersion != CatalogVersion)
        {
            _filteredFightersCache.Clear();
            _filteredCacheVersion = CatalogVersion;
        }

        if (_filteredFightersCache.TryGetValue(filter, out string[]? cached))
            return cached;

        IEnumerable<string> keys = filter switch
        {
            ModFilterMode.VanillaOnly => _vanillaCharacterScripts.Keys,
            ModFilterMode.ModsOnly    => _modCharacterScripts.Keys,
            _                         => _characterScripts.Keys
        };

        string[] result = keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
        _filteredFightersCache[filter] = result;
        return result;
    }

    public static string? ResolveCharacterScript(string characterId) =>
        ResolveCharacterScript(characterId, _activeModFilter);

    public static string? ResolveCharacterScript(string characterId, ModFilterMode filter)
    {
        if (filter == ModFilterMode.VanillaOnly &&
            _vanillaCharacterScripts.TryGetValue(characterId, out string? vanillaPath) &&
            File.Exists(vanillaPath))
        {
            return vanillaPath;
        }

        if (filter == ModFilterMode.ModsOnly &&
            _modCharacterScripts.TryGetValue(characterId, out string? modPath) &&
            File.Exists(modPath))
        {
            return modPath;
        }

        if (_characterScripts.TryGetValue(characterId, out string? path) && File.Exists(path))
            return path;

        return null;
    }

    private static bool TryNormalizeSafeRelativePath(string[]? relativeSegments, out string normalizedRelPath)
    {
        normalizedRelPath = string.Empty;
        if (relativeSegments == null || relativeSegments.Length == 0)
            return false;

        for (int i = 0; i < relativeSegments.Length; i++)
        {
            string seg = relativeSegments[i];
            if (string.IsNullOrWhiteSpace(seg))
                return false;

            if (Path.IsPathRooted(seg) ||
                Path.IsPathFullyQualified(seg) ||
                seg.Contains(':') ||
                seg.StartsWith("\\\\", StringComparison.Ordinal) ||
                seg.StartsWith("//", StringComparison.Ordinal) ||
                seg.Contains("..", StringComparison.Ordinal))
            {
                return false;
            }
        }

        normalizedRelPath = Path.Combine(relativeSegments);
        if (string.IsNullOrWhiteSpace(normalizedRelPath) ||
            Path.IsPathRooted(normalizedRelPath) ||
            Path.IsPathFullyQualified(normalizedRelPath) ||
            normalizedRelPath.Contains("..", StringComparison.Ordinal))
        {
            normalizedRelPath = string.Empty;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Searches all active Mod roots and BaseDir for a relative file path (case-insensitive directory matching).
    /// Rejects directory traversal ('..') and rooted/drive paths.
    /// </summary>
    public static string? ResolveFile(params string[] relativeSegments)
    {
        if (!TryNormalizeSafeRelativePath(relativeSegments, out string relPath))
            return null;

        foreach (string root in _contentRoots)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                              + Path.DirectorySeparatorChar;
            string fullCandidate = Path.GetFullPath(Path.Combine(root, relPath));
            if (!fullCandidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                continue;

            if (File.Exists(fullCandidate))
                return fullCandidate;
        }
        return null;
    }

    /// <summary>
    /// Returns all matching directories across BaseDir and Mods for a relative path (e.g. Assets/Sounds/Brawler).
    /// Rejects directory traversal ('..') and rooted/drive paths.
    /// </summary>
    public static List<string> ResolveDirectories(params string[] relativeSegments)
    {
        var dirs = new List<string>();
        if (!TryNormalizeSafeRelativePath(relativeSegments, out string relPath))
            return dirs;

        foreach (string root in _contentRoots)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                              + Path.DirectorySeparatorChar;
            string fullCandidate = Path.GetFullPath(Path.Combine(root, relPath));
            if (!fullCandidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                continue;

            if (Directory.Exists(fullCandidate) && !dirs.Contains(fullCandidate, StringComparer.OrdinalIgnoreCase))
            {
                dirs.Add(fullCandidate);
            }
        }
        return dirs;
    }

    public static string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        byte[] hashBytes = SHA256.HashData(stream);
        return Convert.ToHexString(hashBytes);
    }

    public static string ComputeStringHash(string content)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        byte[] hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}