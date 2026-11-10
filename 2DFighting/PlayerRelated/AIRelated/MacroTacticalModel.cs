using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public enum MacroTactic
{
    NeutralFootsies      = 0,
    RushdownPressure     = 1,
    ZoningKeepaway       = 2,
    TrapAndSpaceControl  = 3,
    MixupGuardBreak      = 4,
    DefensiveTurtleParry = 5,
    AntiAirIntercept     = 6,
    WhiffPunishCounter   = 7,
    AntiZoningCounter    = 8  // Jump-in over fireballs, teleport behind, armor through, or parry!
}

public struct MacroStateVector
{
    public float DistancePct;              // [0]
    public float SelfHealthRatio;          // [1]
    public float OpponentHealthRatio;      // [2]
    public float OpponentAirborne;         // [3]
    public float OpponentBlocking;         // [4]
    public float OpponentCrouching;        // [5]
    public float OpponentInRecovery;       // [6]
    public float IncomingProjectileThreat; // [7]
    public float SelfCornered;             // [8]
    public float OpponentCornered;         // [9]
    public float ZonerAffinity;            // [10]
    public float RushdownAffinity;         // [11]
    public float GrapplerAffinity;         // [12]
    public float TricksterTrapAffinity;    // [13]

    public float[] ToArray() => new float[]
    {
        DistancePct,
        SelfHealthRatio,
        OpponentHealthRatio,
        OpponentAirborne,
        OpponentBlocking,
        OpponentCrouching,
        OpponentInRecovery,
        IncomingProjectileThreat,
        SelfCornered,
        OpponentCornered,
        ZonerAffinity,
        RushdownAffinity,
        GrapplerAffinity,
        TricksterTrapAffinity
    };
}

public class MacroModelWeights
{
    public int EpisodesTrained { get; set; } = 0;
    public int InputSize { get; set; } = 14;
    public int HiddenSize { get; set; } = 12;
    public int OutputSize { get; set; } = 9;
    public float[][] W1 { get; set; } = Array.Empty<float[]>();
    public float[] B1 { get; set; } = Array.Empty<float>();
    public float[][] W2 { get; set; } = Array.Empty<float[]>();
    public float[] B2 { get; set; } = Array.Empty<float>();
}

public struct ExperienceStep
{
    public float[] Input;
    public float[] Hidden;
    public float[] Probs;
    public int ChosenAction;
    public float Reward;
}

public class MacroTacticalModel
{
    public static MacroTacticalModel SharedInstance { get; } = new MacroTacticalModel();

    private readonly string _modelPath;
    private readonly MacroModelWeights _weights;
    private readonly Random _rng = new();
    private float _baselineReturn = 0f;

    public int EpisodesTrained => _weights.EpisodesTrained;

    public MacroTacticalModel()
    {
        _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "AI", "macro_policy.json");
        _weights = LoadOrCreatePretrainedWeights(_modelPath);
    }

    public MacroTactic SampleTacticWithTrace(
        MacroStateVector state,
        AIDifficulty difficulty,
        OpponentPattern spamPattern,
        out ExperienceStep stepTrace)
    {
        float[] x = state.ToArray();
        int H = _weights.HiddenSize;
        int O = _weights.OutputSize;

        float[] hidden = new float[H];
        for (int h = 0; h < H; h++)
        {
            float sum = _weights.B1[h];
            float[] row = _weights.W1[h];
            for (int i = 0; i < x.Length; i++)
            {
                sum += row[i] * x[i];
            }
            hidden[h] = MathF.Tanh(sum);
        }

        float[] logits = new float[O];
        for (int o = 0; o < O; o++)
        {
            float sum = _weights.B2[o];
            float[] row = _weights.W2[o];
            for (int h = 0; h < H; h++)
            {
                sum += row[h] * hidden[h];
            }
            logits[o] = sum;
        }

        ApplySpamAdaptationBias(logits, spamPattern, state.SelfCornered > 0.5f);

        float temperature = difficulty switch
        {
            AIDifficulty.Hard   => 0.28f,
            AIDifficulty.Medium => 0.55f,
            _                   => 1.05f
        };

        float maxLogit = float.MinValue;
        for (int o = 0; o < O; o++)
        {
            logits[o] /= temperature;
            if (logits[o] > maxLogit) maxLogit = logits[o];
        }

        float[] probs = new float[O];
        float sumExp = 0f;
        for (int o = 0; o < O; o++)
        {
            probs[o] = MathF.Exp(logits[o] - maxLogit);
            sumExp += probs[o];
        }

        for (int o = 0; o < O; o++)
        {
            probs[o] /= sumExp;
        }

        float roll = (float)_rng.NextDouble();
        float cumulative = 0f;
        int chosen = 0;
        for (int o = 0; o < O; o++)
        {
            cumulative += probs[o];
            if (roll <= cumulative)
            {
                chosen = o;
                break;
            }
        }

        stepTrace = new ExperienceStep
        {
            Input = x,
            Hidden = hidden,
            Probs = probs,
            ChosenAction = chosen,
            Reward = 0f
        };

        return (MacroTactic)chosen;
    }

    /// <summary>
    /// REINFORCE Policy-Gradient update with discounted returns & baseline subtraction.
    /// Penalizes getting cornered or hit by projectiles; rewards advancing, parrying, and dealing damage.
    /// </summary>
    public void TrainOnEpisode(List<ExperienceStep> trajectory, float terminalReward, float learningRate = 0.012f, float gamma = 0.94f)
    {
        if (trajectory.Count == 0) return;

        int T = trajectory.Count;
        float[] returns = new float[T];
        float runningG = terminalReward;

        for (int t = T - 1; t >= 0; t--)
        {
            runningG = trajectory[t].Reward + (gamma * runningG);
            returns[t] = runningG;
        }

        int H = _weights.HiddenSize;
        int O = _weights.OutputSize;
        int I = _weights.InputSize;

        for (int t = 0; t < T; t++)
        {
            var step = trajectory[t];
            float G = returns[t];
            _baselineReturn = (0.98f * _baselineReturn) + (0.02f * G);
            float advantage = Math.Clamp(G - _baselineReturn, -3.5f, 3.5f);

            // Output layer gradients: dLogit[o] = advantage * (1(o == a) - P(o))
            Span<float> dLogits = stackalloc float[O];
            for (int o = 0; o < O; o++)
            {
                float indicator = (o == step.ChosenAction) ? 1f : 0f;
                dLogits[o] = advantage * (indicator - step.Probs[o]);
            }

            // Backprop into Hidden before updating W2
            Span<float> dHidden = stackalloc float[H];
            for (int h = 0; h < H; h++)
            {
                float gradH = 0f;
                for (int o = 0; o < O; o++)
                {
                    gradH += _weights.W2[o][h] * dLogits[o];
                }
                // Derivative of tanh(z) = 1 - tanh^2(z)
                dHidden[h] = gradH * (1f - (step.Hidden[h] * step.Hidden[h]));
            }

            // Update W2 and B2
            float stepLr = learningRate / MathF.Sqrt(T);
            for (int o = 0; o < O; o++)
            {
                _weights.B2[o] = Math.Clamp(_weights.B2[o] + (stepLr * dLogits[o]), -6f, 6f);
                for (int h = 0; h < H; h++)
                {
                    _weights.W2[o][h] = Math.Clamp(_weights.W2[o][h] + (stepLr * dLogits[o] * step.Hidden[h]), -6f, 6f);
                }
            }

            // Update W1 and B1
            for (int h = 0; h < H; h++)
            {
                _weights.B1[h] = Math.Clamp(_weights.B1[h] + (stepLr * dHidden[h]), -6f, 6f);
                for (int i = 0; i < I; i++)
                {
                    _weights.W1[h][i] = Math.Clamp(_weights.W1[h][i] + (stepLr * dHidden[h] * step.Input[i]), -6f, 6f);
                }
            }
        }

        _weights.EpisodesTrained++;
        SaveWeights();
    }

    public void SaveWeights()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_modelPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(_weights, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_modelPath, json);
        }
        catch { }
    }

    private static void ApplySpamAdaptationBias(float[] logits, OpponentPattern pattern, bool selfCornered)
    {
        if (selfCornered)
        {
            // Never retreat or turtle passively when already backed into the corner!
            logits[(int)MacroTactic.ZoningKeepaway] -= 3.5f;
            logits[(int)MacroTactic.DefensiveTurtleParry] -= 2.2f;
            logits[(int)MacroTactic.AntiZoningCounter] += 2.4f;
            logits[(int)MacroTactic.RushdownPressure] += 2.0f;
        }

        switch (pattern)
        {
            case OpponentPattern.ProjectileSpam:
                logits[(int)MacroTactic.AntiZoningCounter] += 3.6f;
                logits[(int)MacroTactic.RushdownPressure] += 1.8f;
                logits[(int)MacroTactic.ZoningKeepaway] -= 2.5f;
                logits[(int)MacroTactic.DefensiveTurtleParry] -= 2.0f;
                break;
            case OpponentPattern.JumpInSpam:
                logits[(int)MacroTactic.AntiAirIntercept] += 3.0f;
                break;
            case OpponentPattern.LowAttackSpam:
            case OpponentPattern.HighOverheadSpam:
                logits[(int)MacroTactic.DefensiveTurtleParry] += 1.8f;
                logits[(int)MacroTactic.WhiffPunishCounter] += 1.8f;
                break;
            case OpponentPattern.FastPokeSpam:
                logits[(int)MacroTactic.DefensiveTurtleParry] += 2.2f;
                logits[(int)MacroTactic.WhiffPunishCounter] += 2.0f;
                break;
            case OpponentPattern.GuardBreakSpam:
                logits[(int)MacroTactic.RushdownPressure] += 2.5f;
                break;
        }
    }

    private static MacroModelWeights LoadOrCreatePretrainedWeights(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<MacroModelWeights>(json);
                if (loaded != null && loaded.W1.Length == 12 && loaded.OutputSize == 9)
                {
                    return loaded;
                }
            }
        }
        catch { }

        return CreateEmbeddedPretrainedWeights();
    }

    private static MacroModelWeights CreateEmbeddedPretrainedWeights()
    {
        return new MacroModelWeights
        {
            EpisodesTrained = 0,
            InputSize = 14,
            HiddenSize = 12,
            OutputSize = 9,
            B1 = new float[] { 0.8f, -0.6f, -1.0f, -0.8f, -0.9f, -0.7f, -0.5f, -0.5f, -0.4f, -0.8f, -0.6f, 0.0f },
            W1 = new float[][]
            {
                // Dist, SelfHP, OppHP, Air, Block, Crouch, Recov, Proj, SelfCrn, OppCrn, Zoner, Rush, Grap, Trap
                new float[] { -3.2f,  0.4f, -0.3f, -0.5f,  0.2f,  0.0f,  0.5f, -0.8f,  0.0f,  0.6f, -0.8f,  2.2f,  1.2f,  0.0f }, // H0: Close range
                new float[] {  3.0f,  0.2f,  0.0f, -0.4f,  0.0f,  0.0f,  0.0f,  1.2f, -1.5f,  0.0f,  2.0f, -0.5f, -0.8f,  1.6f }, // H1: Far range
                new float[] { -1.2f,  0.0f,  0.0f,  4.2f, -1.0f, -1.0f,  0.0f,  0.0f,  0.0f,  0.0f,  0.4f,  0.4f,  0.4f,  0.4f }, // H2: Opponent airborne
                new float[] { -2.2f,  0.0f,  0.0f, -1.0f,  3.5f,  1.8f, -0.5f,  0.0f,  0.0f,  1.2f, -0.4f,  1.5f,  2.2f,  0.5f }, // H3: Opponent turtling
                new float[] { -1.5f,  0.0f,  0.0f,  0.0f, -1.2f,  0.0f,  4.0f, -0.5f,  0.0f,  0.5f,  0.5f,  1.2f,  1.2f,  0.8f }, // H4: Whiff recovery
                new float[] {  0.8f, -0.4f,  0.4f, -0.5f, -0.5f,  0.0f,  0.8f,  4.2f,  2.2f,  0.0f, -0.4f,  1.5f,  1.2f,  1.2f }, // H5: Incoming Projectile / Cornered
                new float[] {  1.8f,  0.3f,  0.0f,  0.0f,  0.4f,  0.0f,  0.0f, -1.2f, -2.2f,  0.5f,  3.4f, -1.2f, -1.0f,  0.8f }, // H6: Zoner identity (not cornered)
                new float[] {  1.4f,  0.4f,  0.0f, -0.5f,  0.8f,  0.4f,  0.4f, -0.8f, -1.2f,  0.8f,  0.8f, -0.4f,  0.0f,  3.5f }, // H7: Trap/Trickster identity
                new float[] { -1.4f,  0.5f,  0.0f, -0.4f,  1.5f,  0.8f,  0.6f,  0.0f,  0.0f,  1.0f, -1.2f,  0.5f,  3.4f, -0.4f }, // H8: Grappler/Armor identity
                new float[] { -1.2f, -1.0f,  0.5f,  0.0f,  0.0f,  0.0f,  0.0f,  1.8f,  4.0f, -1.0f,  0.5f,  1.5f,  1.2f,  1.5f }, // H9: Self cornered escape
                new float[] { -1.5f,  0.5f, -1.0f,  0.0f,  1.5f,  1.0f,  0.5f,  0.0f, -1.0f,  3.2f,  0.5f,  1.8f,  1.8f,  1.2f }, // H10: Opponent cornered
                new float[] {  0.5f,  1.8f, -1.5f, -0.5f,  0.0f,  0.0f,  0.0f, -0.5f, -0.5f,  0.0f,  0.8f,  0.5f,  0.5f,  0.8f }  // H11: Solid neutral lead
            },
            B2 = new float[] { 0.30f, 0.25f, 0.05f, 0.05f, 0.10f, 0.0f, -0.40f, -0.20f, 0.20f },
            W2 = new float[][]
            {
                // H0     H1     H2     H3     H4     H5     H6     H7     H8     H9    H10    H11
                new float[] {  0.6f,  0.8f, -1.2f, -0.5f, -0.8f, -0.8f,  0.4f,  0.4f,  0.4f, -1.2f,  0.2f,  1.8f }, // 0: NeutralFootsies
                new float[] {  2.2f, -0.4f, -1.0f,  0.4f,  0.8f,  0.8f, -1.4f, -0.4f,  1.2f,  2.2f,  1.8f,  0.4f }, // 1: RushdownPressure
                new float[] { -1.5f,  1.6f, -0.6f,  0.2f,  0.0f, -2.2f,  3.0f,  0.8f, -1.8f, -3.0f,  0.4f,  0.8f }, // 2: ZoningKeepaway
                new float[] { -1.2f,  1.6f, -0.8f,  0.8f,  0.2f, -1.2f,  1.0f,  3.2f, -0.8f, -1.5f,  1.2f,  0.6f }, // 3: TrapAndSpaceControl
                new float[] {  1.5f, -1.4f, -1.5f,  3.2f,  0.4f, -1.0f, -0.8f,  0.4f,  2.6f,  0.8f,  2.2f,  0.2f }, // 4: MixupGuardBreak
                new float[] {  0.8f, -1.2f, -0.4f, -0.8f, -0.5f,  0.5f, -0.2f,  0.0f,  0.8f, -2.5f, -0.8f,  0.4f }, // 5: DefensiveTurtleParry
                new float[] {  0.2f, -0.5f,  4.5f, -1.5f, -0.5f,  0.4f,  0.2f,  0.0f,  0.4f,  0.2f,  0.4f,  0.0f }, // 6: AntiAirIntercept
                new float[] {  0.8f,  0.4f, -0.6f, -0.6f,  4.2f,  0.5f,  0.4f,  0.8f,  0.8f,  0.8f,  0.8f,  0.4f }, // 7: WhiffPunishCounter
                new float[] { -0.5f,  1.4f, -1.2f, -0.5f,  1.2f,  3.8f, -0.4f,  0.8f,  1.6f,  3.2f,  0.4f,  0.2f }  // 8: AntiZoningCounter
            }
        };
    }
}