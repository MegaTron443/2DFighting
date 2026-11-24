using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

public static class GeneticBalanceTrainer
{
    public const int PopulationSize = 12;
    public const int ElitismCount = 2;
    public const int TournamentSize = 3;
    public const int DefaultGenerations = 10;
    public const int MatchesPerCandidate = 6;
    public const int MaxTicksPerRound = 3600;

    private const int MinDamage = 4;
    private const int MaxDamage = 65;
    private const int MinStartupFrames = 2;
    private const int MaxStartupFrames = 30;
    private const int MinActiveFrames = 2;
    private const int MaxActiveFrames = 20;
    private const int MinRecoveryFrames = 4;
    private const int MaxRecoveryFrames = 40;
    private const float MinHitStun = 0.10f;
    private const float MaxHitStun = 1.60f;
    private const float MinCooldown = 0.0f;
    private const float MaxCooldown = 12.0f;
    private const float MinWalkSpeedPct = 0.12f;
    private const float MaxWalkSpeedPct = 0.50f;
    private const float MinJumpForcePct = -1.05f;
    private const float MaxJumpForcePct = -0.42f;
    private const float MutationRate = 0.35f;
    private const float MaxMutationPct = 0.15f;

    private static readonly object _stateLock = new();
    private static Task? _activeTask;
    private static CancellationTokenSource? _cts;

    private static bool _isRunning;
    private static string _currentCharacter = "";
    private static int _currentGeneration;
    private static int _totalGenerations;
    private static float _bestFitness;
    private static string _statusMessage = "";
    private static int _completedVersion;

    public static bool IsRunning
    {
        get { lock (_stateLock) return _isRunning; }
    }

    public static string CurrentCharacter
    {
        get { lock (_stateLock) return _currentCharacter; }
    }

    public static int CurrentGeneration
    {
        get { lock (_stateLock) return _currentGeneration; }
    }

    public static int TotalGenerations
    {
        get { lock (_stateLock) return _totalGenerations; }
    }

    public static float BestFitness
    {
        get { lock (_stateLock) return _bestFitness; }
    }

    public static string StatusMessage
    {
        get { lock (_stateLock) return _statusMessage; }
    }

    public static int CompletedVersion
    {
        get { lock (_stateLock) return _completedVersion; }
    }

    public static bool StartAsync(IReadOnlyList<string> rosterIds, int generations = DefaultGenerations)
    {
        if (rosterIds == null || rosterIds.Count == 0)
            return false;

        List<string> rosterCopy = new(rosterIds.Count);
        for (int i = 0; i < rosterIds.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(rosterIds[i]))
                rosterCopy.Add(rosterIds[i].Trim().ToLowerInvariant());
        }

        if (rosterCopy.Count == 0)
            return false;

        lock (_stateLock)
        {
            if (_isRunning)
                return false;

            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            _isRunning = true;
            _currentCharacter = rosterCopy[0].ToUpperInvariant();
            _currentGeneration = 0;
            _totalGenerations = Math.Max(1, generations);
            _bestFitness = 0f;
            _statusMessage = $"GA STARTING | {rosterCopy.Count} FIGHTERS | {generations} GENS";

            _activeTask = Task.Run(() => RunEvolutionLoop(rosterCopy, Math.Max(1, generations), token), token);
            return true;
        }
    }

    public static void Cancel()
    {
        lock (_stateLock)
        {
            if (_isRunning && _cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                _statusMessage = "GA CANCELLING...";
            }
        }
    }

    public static void StopAndWait(int timeoutMs = 1500)
    {
        Task? runningTask;
        lock (_stateLock)
        {
            runningTask = _activeTask;
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }

        if (runningTask != null)
        {
            try
            {
                runningTask.Wait(timeoutMs);
            }
            catch
            {
            }
        }

        lock (_stateLock)
        {
            _isRunning = false;
            _activeTask = null;
        }
    }

    public static void ResetStatus()
    {
        lock (_stateLock)
        {
            if (!_isRunning)
            {
                _statusMessage = "";
                _currentGeneration = 0;
                _bestFitness = 0f;
            }
        }
    }

    public static CharacterBalanceGenome? SeedFromRecentTelemetry(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return null;

        bool wasHeadless = PhysicsEngine.IsHeadlessThread;
        PhysicsEngine? prevPhysics = null;
        if (!wasHeadless)
        {
            prevPhysics = new PhysicsEngine(setAsMainInstance: false);
            PhysicsEngine.BindHeadlessInstance(prevPhysics);
        }

        try
        {
            var lua = new LuaEngine(isHeadless: true);
            lua.Initialize();
            Character baseChar = lua.CreateHeadlessCharacter(characterId.Trim().ToLowerInvariant());
            return SeedFromRecentTelemetry(characterId, baseChar);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (!wasHeadless)
            {
                PhysicsEngine.BindHeadlessInstance(null);
            }
        }
    }

    private static CharacterBalanceGenome SeedFromRecentTelemetry(string characterId, Character baseChar)
    {
        CharacterBalanceGenome seed = BalanceOverrideStore.TryGetGenome(characterId, baseChar.BaseScriptHash, out CharacterBalanceGenome? saved) && saved != null
            ? saved
            : CharacterBalanceGenome.FromCharacter(baseChar);

        List<string> usableSkills = GetUsableSkillNames(baseChar);
        List<MatchTelemetryRecord> recent = LeaderboardDatabase.GetRecentMatches(2000);
        var statsList = new List<FighterMatchStats>();

        for (int i = 0; i < recent.Count; i++)
        {
            MatchTelemetryRecord rec = recent[i];
            if (!string.Equals(rec.Mode, "AI_GA_Eval", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(rec.Mode, "VS_AI", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(rec.Player1.CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
                statsList.Add(rec.Player1);

            if (string.Equals(rec.Player2.CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
                statsList.Add(rec.Player2);
        }

        if (statsList.Count > 0)
        {
            EvaluateFitnessMetrics(statsList, usableSkills, out float winRate, out float avgCombo, out float diversity, out float fitness);
            seed.WinRate = winRate;
            seed.AvgMaxCombo = avgCombo;
            seed.SkillDiversity = diversity;
            seed.FitnessScore = fitness;
        }

        return seed;
    }

    private static void RunEvolutionLoop(List<string> rosterIds, int generations, CancellationToken ct)
    {
        var headlessPhysics = new PhysicsEngine(setAsMainInstance: false);
        PhysicsEngine.BindHeadlessInstance(headlessPhysics);

        Player? p1 = null;
        Player? p2 = null;
        bool wasCancelled = false;
        int evolvedCount = 0;

        try
        {
            var lua = new LuaEngine(isHeadless: true);
            lua.Initialize();

            var slotChars = new Dictionary<string, Character>[2]
            {
                new(StringComparer.OrdinalIgnoreCase),
                new(StringComparer.OrdinalIgnoreCase)
            };

            for (int i = 0; i < rosterIds.Count; i++)
            {
                string id = rosterIds[i];
                slotChars[0][id] = lua.CreateHeadlessCharacter(id);
                slotChars[1][id] = lua.CreateHeadlessCharacter(id);
            }

            var aiConfig = new AppConfig
            {
                AIDifficulty = AIDifficulty.Hard,
                Display = new DisplaySettings
                {
                    ScreenWidth = PhysicsEngine.HeadlessViewportWidth,
                    ScreenHeight = PhysicsEngine.HeadlessViewportHeight
                }
            };

            float sw = PhysicsEngine.HeadlessViewportWidth;
            Vector2 spawnP1 = new(sw * 0.15f, PhysicsEngine.GroundY * 0.9f);
            Vector2 spawnP2 = new(sw * 0.85f, PhysicsEngine.GroundY * 0.9f);

            p1 = Player.Create("GA P1", 0, spawnP1, aiConfig, isAI: true);
            p2 = Player.Create("GA P2", 1, spawnP2, aiConfig, isAI: true);
            p1.SetOpponent(p2);
            p2.SetOpponent(p1);
            p1.SetAIDifficulty(AIDifficulty.Hard);
            p2.SetAIDifficulty(AIDifficulty.Hard);

            var gameData = new GameData(p1, p2, new Timer(), lua, aiConfig, soundHandler: null, services: null);
            var rng = new Random();

            for (int cIdx = 0; cIdx < rosterIds.Count; cIdx++)
            {
                ct.ThrowIfCancellationRequested();

                string charId = rosterIds[cIdx];
                Character templateChar = slotChars[0][charId];
                CharacterBalanceGenome baseGenome = templateChar.BaseStatsSnapshot ?? CharacterBalanceGenome.FromCharacter(templateChar);
                List<string> usableSkills = GetUsableSkillNames(templateChar);
                List<string> opponents = BuildOpponentList(charId, rosterIds);

                CharacterBalanceGenome seed = SeedFromRecentTelemetry(charId, templateChar);
                var population = new List<CharacterBalanceGenome>(PopulationSize) { seed.Clone() };

                for (int i = 1; i < PopulationSize; i++)
                {
                    population.Add(MutateGenome(seed, baseGenome, templateChar, rng));
                }

                for (int gen = 1; gen <= generations; gen++)
                {
                    ct.ThrowIfCancellationRequested();

                    lock (_stateLock)
                    {
                        _currentCharacter = charId.ToUpperInvariant();
                        _currentGeneration = gen;
                        _totalGenerations = generations;
                    }

                    for (int pIdx = 0; pIdx < population.Count; pIdx++)
                    {
                        ct.ThrowIfCancellationRequested();
                        EvaluateCandidate(
                            charId,
                            population[pIdx],
                            usableSkills,
                            opponents,
                            slotChars,
                            headlessPhysics,
                            p1,
                            p2,
                            gameData,
                            rng,
                            ct);
                    }

                    population.Sort((a, b) => b.FitnessScore.CompareTo(a.FitnessScore));

                    CharacterBalanceGenome best = population[0].Clone();
                    best.Generation = gen;
                    BalanceOverrideStore.SaveGenome(best);

                    // Co-evolve opponent slot character copy immediately
                    CharacterLoader.ApplyGenome(slotChars[1][charId], best);

                    lock (_stateLock)
                    {
                        _bestFitness = best.FitnessScore;
                        _statusMessage =
                            $"GA GEN {gen}/{generations} | {charId.ToUpperInvariant()} | FIT: {best.FitnessScore:F2} | WIN: {(int)MathF.Round(best.WinRate * 100f)}% | DIV: {(int)MathF.Round(best.SkillDiversity * 100f)}%";
                        _completedVersion++;
                    }

                    if (gen < generations)
                    {
                        var nextGen = new List<CharacterBalanceGenome>(PopulationSize);
                        for (int e = 0; e < ElitismCount && e < population.Count; e++)
                        {
                            nextGen.Add(population[e].Clone());
                        }

                        while (nextGen.Count < PopulationSize)
                        {
                            CharacterBalanceGenome parentA = SelectTournament(population, rng);
                            CharacterBalanceGenome parentB = SelectTournament(population, rng);
                            CharacterBalanceGenome child = Crossover(parentA, parentB, rng);
                            child = MutateGenome(child, baseGenome, templateChar, rng);
                            nextGen.Add(child);
                        }

                        population = nextGen;
                    }
                }

                evolvedCount++;
            }
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GeneticBalanceTrainer] Evolution failed: {ex.Message}");
            lock (_stateLock)
            {
                _statusMessage = $"GA ERROR: {ex.Message}";
                _isRunning = false;
                _completedVersion++;
            }
            return;
        }
        finally
        {
            p1?.Dispose();
            p2?.Dispose();
            PhysicsEngine.BindHeadlessInstance(null);
        }

        lock (_stateLock)
        {
            _isRunning = false;
            _activeTask = null;
            _completedVersion++;
            if (wasCancelled)
            {
                _statusMessage = evolvedCount > 0
                    ? $"GA STOPPED ({evolvedCount}/{rosterIds.Count} SAVED) | LAST FIT: {_bestFitness:F2}"
                    : "GA AUTO-BALANCE CANCELLED";
            }
            else
            {
                _statusMessage = $"GA COMPLETE | {evolvedCount} FIGHTERS | BEST FIT: {_bestFitness:F2}";
            }
        }
    }

    private static void EvaluateCandidate(
        string candidateId,
        CharacterBalanceGenome candidate,
        List<string> usableSkills,
        List<string> opponents,
        Dictionary<string, Character>[] slotChars,
        PhysicsEngine physics,
        Player p1,
        Player p2,
        GameData data,
        Random rng,
        CancellationToken ct)
    {
        var collectedStats = new List<FighterMatchStats>(MatchesPerCandidate);

        for (int matchIdx = 0; matchIdx < MatchesPerCandidate; matchIdx++)
        {
            ct.ThrowIfCancellationRequested();

            string oppId = opponents[(matchIdx + rng.Next(opponents.Count)) % opponents.Count];
            bool candidateIsP1 = (matchIdx % 2) == 0;

            Character p1Char = candidateIsP1 ? slotChars[0][candidateId] : slotChars[0][oppId];
            Character p2Char = candidateIsP1 ? slotChars[1][oppId] : slotChars[1][candidateId];

            if (candidateIsP1)
            {
                CharacterLoader.ApplyGenome(p1Char, candidate);
                ApplyCurrentOpponentBalance(p2Char, oppId);
            }
            else
            {
                ApplyCurrentOpponentBalance(p1Char, oppId);
                CharacterLoader.ApplyGenome(p2Char, candidate);
            }

            p1.ChangeCharacter(p1Char);
            p2.ChangeCharacter(p2Char);

            physics.Match.IsTrainingMode = true;
            physics.Match.CurrentMatchMode = "AI_GA_Eval";
            physics.Match.ResetMatch(p1, p2, physics.Projectiles);

            int p1Rounds = 0;
            int p2Rounds = 0;

            while (p1Rounds < MatchManager.RoundsToWin && p2Rounds < MatchManager.RoundsToWin)
            {
                ct.ThrowIfCancellationRequested();

                for (int tick = 0; tick < MaxTicksPerRound; tick++)
                {
                    if ((tick & 255) == 0)
                        ct.ThrowIfCancellationRequested();

                    physics.StepHeadlessTick(p1, p2, data);
                    if (!p1.IsAlive || !p2.IsAlive)
                        break;
                }

                bool p1WonRound = false;
                bool p2WonRound = false;

                if (!p1.IsAlive && !p2.IsAlive)
                {
                    p1Rounds++;
                    p2Rounds++;
                }
                else if (!p2.IsAlive || p1.Health > p2.Health)
                {
                    p1WonRound = true;
                    p1Rounds++;
                }
                else if (!p1.IsAlive || p2.Health > p1.Health)
                {
                    p2WonRound = true;
                    p2Rounds++;
                }
                else
                {
                    p1Rounds++;
                    p2Rounds++;
                }

                p1.RoundsWon = p1Rounds;
                p2.RoundsWon = p2Rounds;
                p1.NotifyRoundEnd(p1WonRound);
                p2.NotifyRoundEnd(p2WonRound);

                if (p1Rounds < MatchManager.RoundsToWin && p2Rounds < MatchManager.RoundsToWin)
                {
                    physics.Match.StartNextRound(p1, p2, physics.Projectiles);
                }
            }

            MatchTelemetryRecord record = physics.Match.Telemetry.FinalizeMatch(p1, p2);
            FighterMatchStats candidateStats = candidateIsP1 ? record.Player1 : record.Player2;

            // If timeout rounds forced both to 2 wins evenly, determine match outcome consistently
            if (p1Rounds == p2Rounds)
            {
                candidateStats.WonMatch = candidateIsP1
                    ? record.Player1.TotalDamageDealt >= record.Player2.TotalDamageDealt
                    : record.Player2.TotalDamageDealt > record.Player1.TotalDamageDealt;
            }

            collectedStats.Add(candidateStats);
        }

        EvaluateFitnessMetrics(collectedStats, usableSkills, out float winRate, out float avgCombo, out float diversity, out float fitness);
        candidate.WinRate = winRate;
        candidate.AvgMaxCombo = avgCombo;
        candidate.SkillDiversity = diversity;
        candidate.FitnessScore = fitness;
    }

    private static void ApplyCurrentOpponentBalance(Character opponentChar, string oppId)
    {
        if (BalanceOverrideStore.IsEnabled &&
            BalanceOverrideStore.TryGetGenome(oppId, opponentChar.BaseScriptHash, out CharacterBalanceGenome? oppGenome) &&
            oppGenome != null)
        {
            CharacterLoader.ApplyGenome(opponentChar, oppGenome);
        }
        else
        {
            CharacterLoader.ResetToBaseStats(opponentChar);
        }
    }

    internal static void EvaluateFitnessMetrics(
        List<FighterMatchStats> matches,
        List<string> usableSkillNames,
        out float winRate,
        out float avgMaxCombo,
        out float skillDiversity,
        out float fitnessScore)
    {
        if (matches.Count == 0)
        {
            winRate = 0.5f;
            avgMaxCombo = 0f;
            skillDiversity = 0f;
            fitnessScore = 0f;
            return;
        }

        int wins = 0;
        int totalMaxCombo = 0;
        int totalMultiHitCombos = 0;
        var totalSkillUses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < matches.Count; i++)
        {
            FighterMatchStats m = matches[i];
            if (m.WonMatch) wins++;
            totalMaxCombo += m.MaxComboHits;
            totalMultiHitCombos += m.MultiHitCombosCount;

            foreach (var kvp in m.SkillUseCounts)
            {
                if (kvp.Value <= 0) continue;
                totalSkillUses.TryGetValue(kvp.Key, out int prev);
                totalSkillUses[kvp.Key] = prev + kvp.Value;
            }
        }

        int matchCount = matches.Count;
        winRate = wins / (float)matchCount;
        avgMaxCombo = totalMaxCombo / (float)matchCount;
        float avgMultiHit = totalMultiHitCombos / (float)matchCount;

        // 1. Balance term: peaks at 1.0 when winRate == 0.50
        float balanceTerm = 1.0f - 2.0f * MathF.Abs(winRate - 0.50f);

        // 2. Combo term: normalized AvgMaxCombo + MultiHitCombosCount per match
        float comboDepthNorm = Math.Clamp((avgMaxCombo - 1.0f) / 5.0f, 0f, 1f);
        float multiHitNorm = Math.Clamp(avgMultiHit / 2.5f, 0f, 1f);
        float comboTerm = (0.55f * comboDepthNorm) + (0.45f * multiHitNorm);

        // 3. Diversity term & anti-spam / dead-move penalties
        long sumUses = 0;
        int maxUses = 0;
        int usedSkillKinds = 0;

        int kitSize = Math.Max(usableSkillNames.Count, totalSkillUses.Count);
        if (usableSkillNames.Count > 0)
        {
            for (int s = 0; s < usableSkillNames.Count; s++)
            {
                totalSkillUses.TryGetValue(usableSkillNames[s], out int c);
                if (c > 0)
                {
                    usedSkillKinds++;
                    sumUses += c;
                    if (c > maxUses) maxUses = c;
                }
            }
        }
        else
        {
            foreach (int c in totalSkillUses.Values)
            {
                if (c > 0)
                {
                    usedSkillKinds++;
                    sumUses += c;
                    if (c > maxUses) maxUses = c;
                }
            }
        }

        float entropyNorm = 0f;
        if (sumUses > 0 && kitSize > 1)
        {
            double entropyBits = 0.0;
            foreach (int c in totalSkillUses.Values)
            {
                if (c <= 0) continue;
                double p = (double)c / sumUses;
                entropyBits -= p * Math.Log(p, 2.0);
            }
            double maxEntropyBits = Math.Log(kitSize, 2.0);
            entropyNorm = maxEntropyBits > 0.0 ? Math.Clamp((float)(entropyBits / maxEntropyBits), 0f, 1f) : 0f;
        }
        skillDiversity = entropyNorm;

        float maxSkillShare = sumUses > 0 ? maxUses / (float)sumUses : 1.0f;
        int unusedCount = Math.Max(0, kitSize - usedSkillKinds);
        float unusedSkillFraction = kitSize > 0 ? unusedCount / (float)kitSize : 0f;

        float spamPenalty = 1.5f * maxSkillShare * maxSkillShare;
        float deadMovePenalty = 0.5f * unusedSkillFraction;

        fitnessScore = (0.50f * balanceTerm) + (0.25f * comboTerm) + (0.35f * entropyNorm) - spamPenalty - deadMovePenalty;
    }

    private static CharacterBalanceGenome SelectTournament(List<CharacterBalanceGenome> population, Random rng)
    {
        CharacterBalanceGenome best = population[rng.Next(population.Count)];
        for (int i = 1; i < TournamentSize; i++)
        {
            CharacterBalanceGenome candidate = population[rng.Next(population.Count)];
            if (candidate.FitnessScore > best.FitnessScore)
                best = candidate;
        }
        return best;
    }

    private static CharacterBalanceGenome Crossover(CharacterBalanceGenome a, CharacterBalanceGenome b, Random rng)
    {
        var child = a.Clone();
        if (rng.NextDouble() < 0.5)
            child.WalkSpeedPct = b.WalkSpeedPct;
        if (rng.NextDouble() < 0.5)
            child.JumpForcePct = b.JumpForcePct;

        foreach (var kvp in child.Skills)
        {
            if (rng.NextDouble() < 0.5 && b.Skills.TryGetValue(kvp.Key, out SkillBalanceGene? geneB))
            {
                child.Skills[kvp.Key] = geneB.Clone();
            }
        }

        child.FitnessScore = 0f;
        child.WinRate = 0f;
        child.AvgMaxCombo = 0f;
        child.SkillDiversity = 0f;
        return child;
    }

    private static CharacterBalanceGenome MutateGenome(
        CharacterBalanceGenome source,
        CharacterBalanceGenome baseGenome,
        Character baseChar,
        Random rng)
    {
        CharacterBalanceGenome mutated = source.Clone();

        if (rng.NextDouble() < MutationRate)
        {
            mutated.WalkSpeedPct = MutateFloat(mutated.WalkSpeedPct, MinWalkSpeedPct, MaxWalkSpeedPct, rng);
        }

        if (rng.NextDouble() < MutationRate)
        {
            float absJump = MutateFloat(MathF.Abs(mutated.JumpForcePct), MathF.Abs(MaxJumpForcePct), MathF.Abs(MinJumpForcePct), rng);
            mutated.JumpForcePct = -absJump;
        }

        foreach (var kvp in mutated.Skills)
        {
            string skillName = kvp.Key;
            SkillBalanceGene gene = kvp.Value;
            baseGenome.Skills.TryGetValue(skillName, out SkillBalanceGene? baseGene);
            baseChar.Skills.TryGetValue(skillName, out SkillData? baseSkillData);

            bool hasBaseDamage = (baseGene?.Damage ?? gene.Damage) > 0;
            bool hasBaseHitstun = (baseGene?.HitStun ?? gene.HitStun) > 0.01f;
            bool hasBaseCooldown = (baseGene?.CooldownTime ?? gene.CooldownTime) > 0.01f;
            bool isCustomLogic = baseSkillData?.CustomLogic == true;

            if (hasBaseDamage && rng.NextDouble() < MutationRate)
            {
                gene.Damage = MutateInt(gene.Damage, MinDamage, MaxDamage, rng);
            }

            if (rng.NextDouble() < MutationRate)
            {
                gene.StartupFrames = MutateInt(gene.StartupFrames, MinStartupFrames, MaxStartupFrames, rng);
            }

            if (rng.NextDouble() < MutationRate)
            {
                gene.ActiveFrames = MutateInt(gene.ActiveFrames, MinActiveFrames, MaxActiveFrames, rng);
            }

            if (rng.NextDouble() < MutationRate)
            {
                gene.RecoveryFrames = MutateInt(gene.RecoveryFrames, MinRecoveryFrames, MaxRecoveryFrames, rng);
            }

            // Preserve enough total duration on CustomLogic moves so scripted hit/projectile frames still fire
            if (isCustomLogic && baseGene != null)
            {
                int baseTotal = baseGene.StartupFrames + baseGene.ActiveFrames + baseGene.RecoveryFrames;
                int minTotal = Math.Max(8, (int)MathF.Ceiling(baseTotal * 0.80f));
                int currentTotal = gene.StartupFrames + gene.ActiveFrames + gene.RecoveryFrames;
                if (currentTotal < minTotal)
                {
                    gene.RecoveryFrames = Math.Clamp(gene.RecoveryFrames + (minTotal - currentTotal), MinRecoveryFrames, MaxRecoveryFrames);
                }
            }

            if (hasBaseHitstun && rng.NextDouble() < MutationRate)
            {
                gene.HitStun = MutateFloat(gene.HitStun, MinHitStun, MaxHitStun, rng);
            }

            if (hasBaseCooldown && rng.NextDouble() < MutationRate)
            {
                gene.CooldownTime = MutateFloat(gene.CooldownTime, 0.15f, MaxCooldown, rng);
            }
            else if (!hasBaseCooldown)
            {
                gene.CooldownTime = MinCooldown;
            }
        }

        return mutated;
    }

    private static int MutateInt(int value, int min, int max, Random rng)
    {
        float gaussian = SampleGaussian(rng);
        float pct = Math.Clamp(gaussian * (MaxMutationPct * 0.55f), -MaxMutationPct, MaxMutationPct);
        int delta = (int)MathF.Round(value * pct);
        if (delta == 0)
            delta = gaussian >= 0f ? 1 : -1;
        return Math.Clamp(value + delta, min, max);
    }

    private static float MutateFloat(float value, float min, float max, Random rng)
    {
        float gaussian = SampleGaussian(rng);
        float pct = Math.Clamp(gaussian * (MaxMutationPct * 0.55f), -MaxMutationPct, MaxMutationPct);
        return Math.Clamp(value * (1.0f + pct), min, max);
    }

    private static float SampleGaussian(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2));
    }

    private static List<string> GetUsableSkillNames(Character character)
    {
        var names = new List<string>(character.Skills.Count);
        foreach (var kvp in character.Skills)
        {
            if (kvp.Value.TriggerMask != 0)
                names.Add(kvp.Key);
        }
        return names;
    }

    private static List<string> BuildOpponentList(string candidateId, List<string> rosterIds)
    {
        var list = new List<string>(rosterIds.Count);
        for (int i = 0; i < rosterIds.Count; i++)
        {
            if (!string.Equals(rosterIds[i], candidateId, StringComparison.OrdinalIgnoreCase))
                list.Add(rosterIds[i]);
        }

        if (list.Count == 0)
            list.Add(candidateId);

        return list;
    }
}
