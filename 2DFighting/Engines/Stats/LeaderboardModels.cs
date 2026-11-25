using System;
using System.Collections.Generic;

public interface IPlayerIdentityProvider
{
    ulong SteamId { get; }
    string DisplayName { get; }
}

public sealed class LocalIdentityProvider : IPlayerIdentityProvider
{
    public ulong SteamId { get; }
    public string DisplayName { get; }

    public LocalIdentityProvider(AppConfig? config = null)
    {
        SteamId = config?.SteamId ?? 0UL;
        string userName = Environment.UserName;
        DisplayName = string.IsNullOrWhiteSpace(userName) ? "LocalPlayer" : userName.Trim();
    }
}

public sealed class FighterMatchStats
{
    public ulong SteamId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public string ScriptHash { get; set; } = string.Empty;
    public bool WonMatch { get; set; }
    public int RoundsWon { get; set; }
    public int TotalDamageDealt { get; set; }
    public int MaxComboHits { get; set; }
    public int MultiHitCombosCount { get; set; }
    public Dictionary<string, int> SkillUseCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> SkillHitCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public void ResetForMatch(ulong steamId, string displayName, string characterId, string scriptHash)
    {
        SteamId = steamId;
        DisplayName = displayName;
        CharacterId = characterId;
        ScriptHash = scriptHash;
        WonMatch = false;
        RoundsWon = 0;
        TotalDamageDealt = 0;
        MaxComboHits = 0;
        MultiHitCombosCount = 0;
        SkillUseCounts.Clear();
        SkillHitCounts.Clear();
    }

    public FighterMatchStats Clone()
    {
        return new FighterMatchStats
        {
            SteamId = SteamId,
            DisplayName = DisplayName,
            CharacterId = CharacterId,
            ScriptHash = ScriptHash,
            WonMatch = WonMatch,
            RoundsWon = RoundsWon,
            TotalDamageDealt = TotalDamageDealt,
            MaxComboHits = MaxComboHits,
            MultiHitCombosCount = MultiHitCombosCount,
            SkillUseCounts = new Dictionary<string, int>(SkillUseCounts, StringComparer.OrdinalIgnoreCase),
            SkillHitCounts = new Dictionary<string, int>(SkillHitCounts, StringComparer.OrdinalIgnoreCase)
        };
    }
}

public sealed class MatchTelemetryRecord
{
    public string MatchId { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string Mode { get; set; } = "1v1_Local";
    public ModFilterMode ModFilter { get; set; } = ModFilterMode.VanillaAndMods;
    public FighterMatchStats Player1 { get; set; } = new();
    public FighterMatchStats Player2 { get; set; } = new();
}

public sealed class LeaderboardEntry
{
    public ulong SteamId { get; set; }
    public string PersonaName { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public ModFilterMode FilterMode { get; set; } = ModFilterMode.VanillaAndMods;
    public int Rating { get; set; } = 1000;
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int BestCombo { get; set; }
    public long TotalDamage { get; set; }
    public float SkillDiversityScore { get; set; }
    public Dictionary<string, int> AggregateSkillUseCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class MatchTelemetryTracker
{
    public sealed class TrackerSnapshot
    {
        public FighterMatchStats Fighter0 = new();
        public FighterMatchStats Fighter1 = new();
        public bool MultiHit0;
        public bool MultiHit1;
        public int LastCombo0;
        public int LastCombo1;
        public bool IsMatchActive;
        public bool HasRecordedCurrentMatch;
    }

    private readonly FighterMatchStats[] _fighters = { new(), new() };
    private readonly bool[] _multiHitRecordedForCombo = new bool[2];
    private readonly int[] _lastComboCount = new int[2];

    public bool IsMatchActive { get; private set; }
    public bool HasRecordedCurrentMatch { get; private set; }
    public string Mode { get; private set; } = "1v1_Local";
    public ModFilterMode ModFilter { get; private set; } = ModFilterMode.VanillaAndMods;

    public TrackerSnapshot CaptureSnapshot()
    {
        return new TrackerSnapshot
        {
            Fighter0 = _fighters[0].Clone(),
            Fighter1 = _fighters[1].Clone(),
            MultiHit0 = _multiHitRecordedForCombo[0],
            MultiHit1 = _multiHitRecordedForCombo[1],
            LastCombo0 = _lastComboCount[0],
            LastCombo1 = _lastComboCount[1],
            IsMatchActive = IsMatchActive,
            HasRecordedCurrentMatch = HasRecordedCurrentMatch
        };
    }

    public void RestoreSnapshot(TrackerSnapshot snapshot)
    {
        _fighters[0] = snapshot.Fighter0.Clone();
        _fighters[1] = snapshot.Fighter1.Clone();
        _multiHitRecordedForCombo[0] = snapshot.MultiHit0;
        _multiHitRecordedForCombo[1] = snapshot.MultiHit1;
        _lastComboCount[0] = snapshot.LastCombo0;
        _lastComboCount[1] = snapshot.LastCombo1;
        IsMatchActive = snapshot.IsMatchActive;
        HasRecordedCurrentMatch = snapshot.HasRecordedCurrentMatch;
    }

    public void SetSlotIdentity(int slotIndex, ulong steamId, string displayName)
    {
        if (slotIndex is not (0 or 1)) return;
        _fighters[slotIndex].SteamId = steamId;
        if (!string.IsNullOrWhiteSpace(displayName))
            _fighters[slotIndex].DisplayName = displayName;
    }

    public void BeginMatch(Player p1, Player p2, string mode, ModFilterMode modFilter, IPlayerIdentityProvider? identity)
    {
        Mode = string.IsNullOrWhiteSpace(mode) ? "1v1_Local" : mode;
        ModFilter = modFilter;
        IsMatchActive = true;
        HasRecordedCurrentMatch = false;
        _multiHitRecordedForCombo[0] = false;
        _multiHitRecordedForCombo[1] = false;
        _lastComboCount[0] = 0;
        _lastComboCount[1] = 0;

        InitFighter(0, p1, !p1.IsAI ? identity : null);
        InitFighter(1, p2, null);
    }

    public void StopTracking()
    {
        IsMatchActive = false;
    }

    public void RecordSkillUse(Player? player, string? skillName)
    {
        if (!IsMatchActive || player == null) return;
        int idx = GetSlotIndex(player);
        if (idx < 0) return;

        FighterMatchStats stats = _fighters[idx];
        SyncCharacterMeta(stats, player.CurrentCharacter);

        string key = string.IsNullOrWhiteSpace(skillName) ? "unknown" : skillName;
        stats.SkillUseCounts.TryGetValue(key, out int count);
        stats.SkillUseCounts[key] = count + 1;
    }

    public void RecordDamageDealt(Player? attacker, int damage)
    {
        if (!IsMatchActive || attacker == null || damage <= 0) return;
        int idx = GetSlotIndex(attacker);
        if (idx < 0) return;

        FighterMatchStats stats = _fighters[idx];
        SyncCharacterMeta(stats, attacker.CurrentCharacter);
        stats.TotalDamageDealt += damage;
    }

    public void RecordCleanHit(Player? attacker, string? skillName, int newComboCount)
    {
        if (!IsMatchActive || attacker == null) return;
        int idx = GetSlotIndex(attacker);
        if (idx < 0) return;

        FighterMatchStats stats = _fighters[idx];
        SyncCharacterMeta(stats, attacker.CurrentCharacter);

        string key = string.IsNullOrWhiteSpace(skillName) ? "basic_attack" : skillName;
        stats.SkillHitCounts.TryGetValue(key, out int hits);
        stats.SkillHitCounts[key] = hits + 1;

        if (newComboCount > stats.MaxComboHits)
            stats.MaxComboHits = newComboCount;

        if (newComboCount <= 1 || newComboCount < _lastComboCount[idx])
            _multiHitRecordedForCombo[idx] = false;

        _lastComboCount[idx] = newComboCount;

        if (newComboCount >= 3 && !_multiHitRecordedForCombo[idx])
        {
            _multiHitRecordedForCombo[idx] = true;
            stats.MultiHitCombosCount++;
        }
    }

    public MatchTelemetryRecord FinalizeMatch(Player p1, Player p2)
    {
        HasRecordedCurrentMatch = true;
        IsMatchActive = false;

        SyncCharacterMeta(_fighters[0], p1.CurrentCharacter);
        SyncCharacterMeta(_fighters[1], p2.CurrentCharacter);

        _fighters[0].RoundsWon = p1.RoundsWon;
        _fighters[1].RoundsWon = p2.RoundsWon;
        _fighters[0].WonMatch = p1.RoundsWon > p2.RoundsWon;
        _fighters[1].WonMatch = p2.RoundsWon > p1.RoundsWon;

        return new MatchTelemetryRecord
        {
            MatchId = Guid.NewGuid().ToString("N"),
            TimestampUtc = DateTime.UtcNow,
            Mode = Mode,
            ModFilter = ModFilter,
            Player1 = _fighters[0].Clone(),
            Player2 = _fighters[1].Clone()
        };
    }

    private void InitFighter(int slotIndex, Player player, IPlayerIdentityProvider? identity)
    {
        ulong steamId = identity?.SteamId ?? 0UL;
        string displayName;
        if (identity != null && !string.IsNullOrWhiteSpace(identity.DisplayName))
        {
            displayName = identity.DisplayName;
        }
        else if (player.IsAI)
        {
            displayName = slotIndex == 0 ? "CPU 1" : "CPU 2";
        }
        else
        {
            displayName = string.IsNullOrWhiteSpace(player.Name) ? $"Player {slotIndex + 1}" : player.Name;
        }

        string charId = ResolveCharacterId(player.CurrentCharacter);
        string scriptHash = player.CurrentCharacter?.ScriptHash ?? string.Empty;
        _fighters[slotIndex].ResetForMatch(steamId, displayName, charId, scriptHash);
    }

    private static void SyncCharacterMeta(FighterMatchStats stats, Character? character)
    {
        if (character == null) return;
        string charId = ResolveCharacterId(character);
        if (!string.IsNullOrEmpty(charId))
            stats.CharacterId = charId;
        if (!string.IsNullOrEmpty(character.ScriptHash))
            stats.ScriptHash = character.ScriptHash;
    }

    private static string ResolveCharacterId(Character? character)
    {
        if (character == null) return string.Empty;
        if (!string.IsNullOrWhiteSpace(character.CharacterId))
            return character.CharacterId;
        return character.Name.Trim().ToLowerInvariant();
    }

    private static int GetSlotIndex(Player player)
    {
        if (string.Equals(player.Name, "P1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(player.Name, "Player 1", StringComparison.OrdinalIgnoreCase) ||
            player.PlayerIndex == 0)
        {
            return 0;
        }
        if (player.PlayerIndex == 1)
            return 1;
        return player.PlayerIndex is 0 or 1 ? player.PlayerIndex : -1;
    }
}
