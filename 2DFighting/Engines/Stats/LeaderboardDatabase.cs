using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

public enum LeaderboardSortMode
{
    Rating,
    WinRate,
    MaxCombo,
    Diversity
}

public static class LeaderboardDatabase
{
    private const int MaxRecentMatches = 2000;
    private const double EloKFactor = 32.0;
    private const int EvalSaveInterval = 5;

    private readonly record struct LeaderboardKey(ulong SteamId, string CharacterId, ModFilterMode FilterMode);

    private sealed class PersistedDatabase
    {
        public List<LeaderboardEntry> Entries { get; set; } = new();
        public List<MatchTelemetryRecord> RecentMatches { get; set; } = new();
    }

    private static readonly object _sync = new();
    private static readonly Dictionary<LeaderboardKey, LeaderboardEntry> _entries = new();
    private static readonly List<MatchTelemetryRecord> _recentMatches = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static bool _initialized = false;
    private static bool _dirty = false;
    private static int _unsavedEvalMatches = 0;
    private static int _version = 0;

    public static int Version
    {
        get
        {
            lock (_sync)
            {
                return _version;
            }
        }
    }

    private static string DatabasePath => Path.Combine(ModManager.CacheDir, "leaderboard_db.json");

    public static void Initialize()
    {
        lock (_sync)
        {
            InitializeLocked();
        }
    }

    private static void InitializeLocked()
    {
        _entries.Clear();
        _recentMatches.Clear();
        _dirty = false;
        _unsavedEvalMatches = 0;

        string path = DatabasePath;
        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    PersistedDatabase? db = JsonSerializer.Deserialize<PersistedDatabase>(json, JsonOptions);
                    if (db != null)
                    {
                        foreach (LeaderboardEntry entry in db.Entries)
                        {
                            string charId = NormalizeCharacterId(entry.CharacterId);
                            entry.CharacterId = charId;
                            entry.AggregateSkillUseCounts ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                            var key = new LeaderboardKey(entry.SteamId, charId, entry.FilterMode);
                            _entries[key] = entry;
                        }

                        if (db.RecentMatches.Count > 0)
                        {
                            int start = Math.Max(0, db.RecentMatches.Count - MaxRecentMatches);
                            for (int i = start; i < db.RecentMatches.Count; i++)
                                _recentMatches.Add(db.RecentMatches[i]);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LeaderboardDatabase] Load skipped: {ex.Message}");
        }

        _initialized = true;
        _version++;
    }

    public static void RecordMatch(MatchTelemetryRecord? record)
    {
        if (record == null) return;

        lock (_sync)
        {
            if (!_initialized)
                InitializeLocked();

            _recentMatches.Add(record);
            if (_recentMatches.Count > MaxRecentMatches)
                _recentMatches.RemoveRange(0, _recentMatches.Count - MaxRecentMatches);

            LeaderboardEntry entry1 = GetOrCreateEntryLocked(record.Player1, record.ModFilter);
            LeaderboardEntry entry2 = GetOrCreateEntryLocked(record.Player2, record.ModFilter);

            double score1 = record.Player1.WonMatch ? 1.0 : (record.Player2.WonMatch ? 0.0 : 0.5);
            double score2 = 1.0 - score1;
            int rating1 = entry1.Rating;
            int rating2 = entry2.Rating;

            DateTime now = record.TimestampUtc == default ? DateTime.UtcNow : record.TimestampUtc;

            if (ReferenceEquals(entry1, entry2))
            {
                int delta = (int)Math.Round(EloKFactor * (score1 - 0.5));
                entry1.Rating = Math.Max(100, rating1 + delta);
                ApplyFighterStatsLocked(entry1, record.Player1, won: record.Player1.WonMatch, lost: record.Player2.WonMatch, now);
            }
            else
            {
                double expected1 = 1.0 / (1.0 + Math.Pow(10.0, (rating2 - rating1) / 400.0));
                double expected2 = 1.0 - expected1;

                entry1.Rating = Math.Max(100, (int)Math.Round(rating1 + EloKFactor * (score1 - expected1)));
                entry2.Rating = Math.Max(100, (int)Math.Round(rating2 + EloKFactor * (score2 - expected2)));

                ApplyFighterStatsLocked(entry1, record.Player1, won: record.Player1.WonMatch, lost: record.Player2.WonMatch, now);
                ApplyFighterStatsLocked(entry2, record.Player2, won: record.Player2.WonMatch, lost: record.Player1.WonMatch, now);
            }

            _dirty = true;
            _version++;

            bool isGaEval = string.Equals(record.Mode, "AI_GA_Eval", StringComparison.Ordinal);
            if (isGaEval)
            {
                _unsavedEvalMatches++;
                if (_unsavedEvalMatches >= EvalSaveInterval)
                {
                    SaveLocked();
                }
            }
            else
            {
                _unsavedEvalMatches = 0;
                SaveLocked();
            }

            if (!isGaEval)
            {
                SteamLeaderboardSync.EnqueueIfLocalPlayer(entry1);
                if (!ReferenceEquals(entry1, entry2))
                    SteamLeaderboardSync.EnqueueIfLocalPlayer(entry2);
            }
        }
    }

    public static List<string> GetDistinctCharacterIds(ModFilterMode filter)
    {
        lock (_sync)
        {
            if (!_initialized)
                InitializeLocked();

            return _entries.Values
                .Where(e => e.FilterMode == filter &&
                            !string.IsNullOrWhiteSpace(e.CharacterId) &&
                            !string.Equals(e.CharacterId, "unknown", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.CharacterId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public static List<LeaderboardEntry> GetTopEntries(
        ModFilterMode filter,
        string? characterFilter = null,
        LeaderboardSortMode sortBy = LeaderboardSortMode.Rating,
        int limit = 50)
    {
        lock (_sync)
        {
            if (!_initialized)
                InitializeLocked();

            IEnumerable<LeaderboardEntry> query = _entries.Values.Where(e => e.FilterMode == filter);

            if (!string.IsNullOrWhiteSpace(characterFilter) &&
                !string.Equals(characterFilter, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                string normalizedChar = NormalizeCharacterId(characterFilter);
                query = query.Where(e => string.Equals(e.CharacterId, normalizedChar, StringComparison.OrdinalIgnoreCase));
            }

            IOrderedEnumerable<LeaderboardEntry> ordered = sortBy switch
            {
                LeaderboardSortMode.WinRate => query
                    .OrderByDescending(GetWinRate)
                    .ThenByDescending(e => e.Rating)
                    .ThenByDescending(e => e.Wins)
                    .ThenByDescending(e => e.BestCombo),

                LeaderboardSortMode.MaxCombo => query
                    .OrderByDescending(e => e.BestCombo)
                    .ThenByDescending(e => e.Rating)
                    .ThenByDescending(e => e.Wins),

                LeaderboardSortMode.Diversity => query
                    .OrderByDescending(e => e.SkillDiversityScore)
                    .ThenByDescending(e => e.Rating)
                    .ThenByDescending(e => e.Wins),

                _ => query
                    .OrderByDescending(e => e.Rating)
                    .ThenByDescending(e => e.Wins)
                    .ThenByDescending(e => e.BestCombo)
                    .ThenByDescending(e => e.TotalDamage)
            };

            return ordered.Take(Math.Max(0, limit)).ToList();
        }
    }

    private static double GetWinRate(LeaderboardEntry entry)
    {
        int totalGames = entry.Wins + entry.Losses;
        return totalGames > 0 ? (double)entry.Wins / totalGames : 0.0;
    }

    public static List<MatchTelemetryRecord> GetRecentMatches(int maxCount = 100)
    {
        lock (_sync)
        {
            if (!_initialized)
                InitializeLocked();

            int take = Math.Clamp(maxCount, 0, _recentMatches.Count);
            var list = new List<MatchTelemetryRecord>(take);
            for (int i = _recentMatches.Count - 1; i >= _recentMatches.Count - take; i--)
                list.Add(_recentMatches[i]);
            return list;
        }
    }

    public static void Flush()
    {
        lock (_sync)
        {
            if (!_initialized || !_dirty)
                return;
            SaveLocked();
        }
    }

    private static LeaderboardEntry GetOrCreateEntryLocked(FighterMatchStats stats, ModFilterMode filter)
    {
        string charId = NormalizeCharacterId(stats.CharacterId);
        var key = new LeaderboardKey(stats.SteamId, charId, filter);
        if (!_entries.TryGetValue(key, out LeaderboardEntry? entry))
        {
            entry = new LeaderboardEntry
            {
                SteamId = stats.SteamId,
                PersonaName = string.IsNullOrWhiteSpace(stats.DisplayName) ? "Player" : stats.DisplayName,
                CharacterId = charId,
                FilterMode = filter,
                Rating = 1000,
                LastUpdatedUtc = DateTime.UtcNow
            };
            _entries[key] = entry;
        }
        return entry;
    }

    private static void ApplyFighterStatsLocked(LeaderboardEntry entry, FighterMatchStats stats, bool won, bool lost, DateTime timestampUtc)
    {
        if (!string.IsNullOrWhiteSpace(stats.DisplayName))
            entry.PersonaName = stats.DisplayName;

        if (won) entry.Wins++;
        else if (lost) entry.Losses++;

        if (stats.MaxComboHits > entry.BestCombo)
            entry.BestCombo = stats.MaxComboHits;

        entry.TotalDamage += Math.Max(0, stats.TotalDamageDealt);

        foreach (var kvp in stats.SkillUseCounts)
        {
            if (kvp.Value <= 0) continue;
            entry.AggregateSkillUseCounts.TryGetValue(kvp.Key, out int prev);
            entry.AggregateSkillUseCounts[kvp.Key] = prev + kvp.Value;
        }

        entry.SkillDiversityScore = ComputeNormalizedShannonEntropy(entry.AggregateSkillUseCounts);
        entry.LastUpdatedUtc = timestampUtc;
    }

    private static float ComputeNormalizedShannonEntropy(Dictionary<string, int> counts)
    {
        if (counts.Count <= 1) return 0f;

        long total = 0;
        int nonZeroCategories = 0;
        foreach (int count in counts.Values)
        {
            if (count > 0)
            {
                total += count;
                nonZeroCategories++;
            }
        }

        if (total <= 0 || nonZeroCategories <= 1)
            return 0f;

        double entropy = 0.0;
        foreach (int count in counts.Values)
        {
            if (count <= 0) continue;
            double p = (double)count / total;
            entropy -= p * Math.Log(p);
        }

        double maxEntropy = Math.Log(nonZeroCategories);
        if (maxEntropy <= 0.0) return 0f;

        return Math.Clamp((float)(entropy / maxEntropy), 0f, 1f);
    }

    private static string NormalizeCharacterId(string? characterId)
    {
        return string.IsNullOrWhiteSpace(characterId)
            ? "unknown"
            : characterId.Trim().ToLowerInvariant();
    }

    private static void SaveLocked()
    {
        string path = DatabasePath;
        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var payload = new PersistedDatabase
            {
                Entries = _entries.Values.ToList(),
                RecentMatches = _recentMatches
            };

            string json = JsonSerializer.Serialize(payload, JsonOptions);
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, path, overwrite: true);

            _dirty = false;
            _unsavedEvalMatches = 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LeaderboardDatabase] Save failed: {ex.Message}");
        }
    }
}
