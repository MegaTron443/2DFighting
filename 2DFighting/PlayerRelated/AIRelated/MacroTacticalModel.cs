using System;
using System.Buffers;
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

    public readonly void CopyTo(Span<float> destination)
    {
        destination[0]  = DistancePct;
        destination[1]  = SelfHealthRatio;
        destination[2]  = OpponentHealthRatio;
        destination[3]  = OpponentAirborne;
        destination[4]  = OpponentBlocking;
        destination[5]  = OpponentCrouching;
        destination[6]  = OpponentInRecovery;
        destination[7]  = IncomingProjectileThreat;
        destination[8]  = SelfCornered;
        destination[9]  = OpponentCornered;
        destination[10] = ZonerAffinity;
        destination[11] = RushdownAffinity;
        destination[12] = GrapplerAffinity;
        destination[13] = TricksterTrapAffinity;
    }

    public readonly float[] ToArray()
    {
        float[] arr = new float[14];
        CopyTo(arr);
        return arr;
    }
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
    private readonly float[] _flatW1;
    private readonly float[] _flatW2;
    private readonly Random _rng = new();
    private float _baselineReturn = 0f;

    public int EpisodesTrained => _weights.EpisodesTrained;

    public MacroTacticalModel()
    {
        _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "AI", "macro_policy.json");
        _weights = LoadOrCreatePretrainedWeights(_modelPath);

        _flatW1 = Flatten(_weights.W1, _weights.HiddenSize, _weights.InputSize);
        _flatW2 = Flatten(_weights.W2, _weights.OutputSize, _weights.HiddenSize);
    }

    /// <summary>
    /// Zero-allocation inference for standard gameplay (no training trace recorded).
    /// </summary>
    public MacroTactic SampleTactic(
        in MacroStateVector state,
        AIDifficulty difficulty,
        OpponentPattern spamPattern)
    {
        int I = _weights.InputSize;
        int H = _weights.HiddenSize;
        int O = _weights.OutputSize;

        Span<float> x = stackalloc float[I];
        Span<float> hidden = stackalloc float[H];
        Span<float> logits = stackalloc float[O];
        Span<float> probs = stackalloc float[O];

        state.CopyTo(x);
        int chosen = ForwardPass(x, hidden, logits, probs, difficulty, spamPattern, state.SelfCornered > 0.5f);
        return (MacroTactic)chosen;
    }

    /// <summary>
    /// Pooled-allocation forward pass for RL training episodes.
    /// Arrays are automatically returned to ArrayPool when TrainOnEpisode or DiscardTrajectory is called.
    /// </summary>
    public MacroTactic SampleTacticWithTrace(
        in MacroStateVector state,
        AIDifficulty difficulty,
        OpponentPattern spamPattern,
        out ExperienceStep stepTrace)
    {
        int I = _weights.InputSize;
        int H = _weights.HiddenSize;
        int O = _weights.OutputSize;

        float[] xArr = ArrayPool<float>.Shared.Rent(I);
        float[] hiddenArr = ArrayPool<float>.Shared.Rent(H);
        float[] probsArr = ArrayPool<float>.Shared.Rent(O);
        Span<float> logits = stackalloc float[O];

        Span<float> x = xArr.AsSpan(0, I);
        Span<float> hidden = hiddenArr.AsSpan(0, H);
        Span<float> probs = probsArr.AsSpan(0, O);

        state.CopyTo(x);
        int chosen = ForwardPass(x, hidden, logits, probs, difficulty, spamPattern, state.SelfCornered > 0.5f);

        stepTrace = new ExperienceStep
        {
            Input = xArr,
            Hidden = hiddenArr,
            Probs = probsArr,
            ChosenAction = chosen,
            Reward = 0f
        };

        return (MacroTactic)chosen;
    }

    private int ForwardPass(
        ReadOnlySpan<float> x,
        Span<float> hidden,
        Span<float> logits,
        Span<float> probs,
        AIDifficulty difficulty,
        OpponentPattern spamPattern,
        bool selfCornered)
    {
        int I = _weights.InputSize;
        int H = _weights.HiddenSize;
        int O = _weights.OutputSize;

        for (int h = 0; h < H; h++)
        {
            float sum = _weights.B1[h];
            int rowOffset = h * I;
            for (int i = 0; i < I; i++)
            {
                sum += _flatW1[rowOffset + i] * x[i];
            }
            hidden[h] = MathF.Tanh(sum);
        }

        for (int o = 0; o < O; o++)
        {
            float sum = _weights.B2[o];
            int rowOffset = o * H;
            for (int h = 0; h < H; h++)
            {
                sum += _flatW2[rowOffset + h] * hidden[h];
            }
            logits[o] = sum;
        }

        ApplySpamAdaptationBias(logits, spamPattern, selfCornered);

        float invTemp = difficulty switch
        {
            AIDifficulty.Hard   => 1f / 0.28f,
            AIDifficulty.Medium => 1f / 0.55f,
            _                   => 1f / 1.05f
        };

        float maxLogit = float.MinValue;
        for (int o = 0; o < O; o++)
        {
            logits[o] *= invTemp;
            if (logits[o] > maxLogit) maxLogit = logits[o];
        }

        float sumExp = 0f;
        for (int o = 0; o < O; o++)
        {
            probs[o] = MathF.Exp(logits[o] - maxLogit);
            sumExp += probs[o];
        }

        float invSumExp = 1f / sumExp;
        float roll = (float)_rng.NextDouble();
        float cumulative = 0f;
        int chosen = O - 1;
        bool picked = false;

        for (int o = 0; o < O; o++)
        {
            probs[o] *= invSumExp;
            if (!picked)
            {
                cumulative += probs[o];
                if (roll <= cumulative)
                {
                    chosen = o;
                    picked = true;
                }
            }
        }

        return chosen;
    }

    /// <summary>
    /// REINFORCE Policy-Gradient update with discounted returns & baseline subtraction.
    /// Automatically recycles pooled buffers from the trajectory and clears the list.
    /// </summary>
    public void TrainOnEpisode(List<ExperienceStep> trajectory, float terminalReward, float learningRate = 0.012f, float gamma = 0.94f)
    {
        int T = trajectory.Count;
        if (T == 0) return;

        float[] returns = ArrayPool<float>.Shared.Rent(T);
        Span<float> dLogits = stackalloc float[_weights.OutputSize];
        Span<float> dHidden = stackalloc float[_weights.HiddenSize];

        try
        {
            float runningG = terminalReward;
            for (int t = T - 1; t >= 0; t--)
            {
                runningG = trajectory[t].Reward + (gamma * runningG);
                returns[t] = runningG;
            }

            int H = _weights.HiddenSize;
            int O = _weights.OutputSize;
            int I = _weights.InputSize;
            float stepLr = learningRate / MathF.Sqrt(T);

            for (int t = 0; t < T; t++)
            {
                ExperienceStep step = trajectory[t];
                float G = returns[t];
                _baselineReturn = (0.98f * _baselineReturn) + (0.02f * G);
                float advantage = Math.Clamp(G - _baselineReturn, -3.5f, 3.5f);

                // Output layer gradients: dLogit[o] = advantage * (1(o == a) - P(o))
                for (int o = 0; o < O; o++)
                {
                    float indicator = (o == step.ChosenAction) ? 1f : 0f;
                    dLogits[o] = advantage * (indicator - step.Probs[o]);
                }

                // Backprop into Hidden before updating W2
                for (int h = 0; h < H; h++)
                {
                    float gradH = 0f;
                    for (int o = 0; o < O; o++)
                    {
                        gradH += _flatW2[(o * H) + h] * dLogits[o];
                    }
                    dHidden[h] = gradH * (1f - (step.Hidden[h] * step.Hidden[h]));
                }

                // Update W2 and B2
                for (int o = 0; o < O; o++)
                {
                    _weights.B2[o] = Math.Clamp(_weights.B2[o] + (stepLr * dLogits[o]), -6f, 6f);
                    int rowOffset = o * H;
                    float lrDLogit = stepLr * dLogits[o];
                    for (int h = 0; h < H; h++)
                    {
                        _flatW2[rowOffset + h] = Math.Clamp(_flatW2[rowOffset + h] + (lrDLogit * step.Hidden[h]), -6f, 6f);
                    }
                }

                // Update W1 and B1
                for (int h = 0; h < H; h++)
                {
                    _weights.B1[h] = Math.Clamp(_weights.B1[h] + (stepLr * dHidden[h]), -6f, 6f);
                    int rowOffset = h * I;
                    float lrDHidden = stepLr * dHidden[h];
                    for (int i = 0; i < I; i++)
                    {
                        _flatW1[rowOffset + i] = Math.Clamp(_flatW1[rowOffset + i] + (lrDHidden * step.Input[i]), -6f, 6f);
                    }
                }
            }

            _weights.EpisodesTrained++;
            if (!PhysicsEngine.IsTrainingMode)
            {
                SaveWeights();
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(returns);
            ReturnTrajectoryBuffers(trajectory);
            trajectory.Clear();
        }
    }

    /// <summary>
    /// Call this if an episode is aborted/reset without calling TrainOnEpisode so pooled buffers are not leaked.
    /// </summary>
    public static void DiscardTrajectory(List<ExperienceStep> trajectory)
    {
        if (trajectory.Count == 0) return;
        ReturnTrajectoryBuffers(trajectory);
        trajectory.Clear();
    }

    private static void ReturnTrajectoryBuffers(List<ExperienceStep> trajectory)
    {
        for (int t = 0; t < trajectory.Count; t++)
        {
            var step = trajectory[t];
            if (step.Input != null) ArrayPool<float>.Shared.Return(step.Input);
            if (step.Hidden != null) ArrayPool<float>.Shared.Return(step.Hidden);
            if (step.Probs != null) ArrayPool<float>.Shared.Return(step.Probs);
        }
    }

    public void SaveWeights()
    {
        try
        {
            SyncFlatToJagged();

            string? dir = Path.GetDirectoryName(_modelPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(_weights, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_modelPath, json);
        }
        catch { }
    }

    private static float[] Flatten(float[][] jagged, int rows, int cols)
    {
        float[] flat = new float[rows * cols];
        for (int r = 0; r < rows; r++)
        {
            Array.Copy(jagged[r], 0, flat, r * cols, cols);
        }
        return flat;
    }

    private void SyncFlatToJagged()
    {
        for (int h = 0; h < _weights.HiddenSize; h++)
        {
            Array.Copy(_flatW1, h * _weights.InputSize, _weights.W1[h], 0, _weights.InputSize);
        }

        for (int o = 0; o < _weights.OutputSize; o++)
        {
            Array.Copy(_flatW2, o * _weights.HiddenSize, _weights.W2[o], 0, _weights.HiddenSize);
        }
    }

    private static void ApplySpamAdaptationBias(Span<float> logits, OpponentPattern pattern, bool selfCornered)
    {
        if (selfCornered)
        {
            logits[(int)MacroTactic.ZoningKeepaway]      -= 3.5f;
            logits[(int)MacroTactic.DefensiveTurtleParry] -= 2.2f;
            logits[(int)MacroTactic.AntiZoningCounter]    += 2.4f;
            logits[(int)MacroTactic.RushdownPressure]     += 2.0f;
        }

        switch (pattern)
        {
            case OpponentPattern.ProjectileSpam:
                logits[(int)MacroTactic.AntiZoningCounter]    += 3.6f;
                logits[(int)MacroTactic.RushdownPressure]     += 1.8f;
                logits[(int)MacroTactic.ZoningKeepaway]       -= 2.5f;
                logits[(int)MacroTactic.DefensiveTurtleParry] -= 2.0f;
                break;
            case OpponentPattern.JumpInSpam:
                logits[(int)MacroTactic.AntiAirIntercept]     += 3.0f;
                break;
            case OpponentPattern.LowAttackSpam:
            case OpponentPattern.HighOverheadSpam:
                logits[(int)MacroTactic.DefensiveTurtleParry] += 1.8f;
                logits[(int)MacroTactic.WhiffPunishCounter]   += 1.8f;
                break;
            case OpponentPattern.FastPokeSpam:
                logits[(int)MacroTactic.DefensiveTurtleParry] += 2.2f;
                logits[(int)MacroTactic.WhiffPunishCounter]   += 2.0f;
                break;
            case OpponentPattern.GuardBreakSpam:
                logits[(int)MacroTactic.RushdownPressure]     += 2.5f;
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
                if (loaded != null &&
                    loaded.W1 != null && loaded.W1.Length == loaded.HiddenSize &&
                    loaded.W2 != null && loaded.W2.Length == loaded.OutputSize &&
                    loaded.B1 != null && loaded.B1.Length == loaded.HiddenSize &&
                    loaded.B2 != null && loaded.B2.Length == loaded.OutputSize)
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