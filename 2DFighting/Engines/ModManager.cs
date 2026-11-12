using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

public static class ModManager
{
    public static string BaseDir => AppDomain.CurrentDomain.BaseDirectory;
    public static string ModsDir => Path.Combine(BaseDir, "Mods");
    public static string CacheDir => Path.Combine(BaseDir, "Cache");
    public static string ExtractedModsCacheDir => Path.Combine(CacheDir, "Mods");

    private static readonly List<string> _contentRoots = new();

    private static readonly Dictionary<string, string> _characterScripts = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> ContentRoots => _contentRoots;
    public static IReadOnlyDictionary<string, string> CharacterScripts => _characterScripts;
    public static string[] AvailableFighters { get; private set; } = Array.Empty<string>();

    public static void Initialize()
    {
        Directory.CreateDirectory(ModsDir);
        Directory.CreateDirectory(CacheDir);
        Directory.CreateDirectory(ExtractedModsCacheDir);

        _contentRoots.Clear();
        _characterScripts.Clear();

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
        for (int i = _contentRoots.Count - 1; i >= 0; i--)
        {
            string root = _contentRoots[i];
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
                }
            }
        }

        AvailableFighters = _characterScripts.Keys.ToArray();
    }

    public static string? ResolveCharacterScript(string characterId)
    {
        if (_characterScripts.TryGetValue(characterId, out string? path) && File.Exists(path))
            return path;

        return null;
    }

    /// <summary>
    /// Searches all active Mod roots and BaseDir for a relative file path (case-insensitive directory matching).
    /// </summary>
    public static string? ResolveFile(params string[] relativeSegments)
    {
        string relPath = Path.Combine(relativeSegments);
        foreach (string root in _contentRoots)
        {
            string candidate = Path.Combine(root, relPath);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>
    /// Returns all matching directories across BaseDir and Mods for a relative path (e.g. Assets/Sounds/Brawler).
    /// </summary>
    public static List<string> ResolveDirectories(params string[] relativeSegments)
    {
        var dirs = new List<string>();
        string relPath = Path.Combine(relativeSegments);

        foreach (string root in _contentRoots)
        {
            string candidate = Path.Combine(root, relPath);
            if (Directory.Exists(candidate) && !dirs.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                dirs.Add(candidate);
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