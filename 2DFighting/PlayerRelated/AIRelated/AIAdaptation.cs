using System.Collections.Generic;

public enum OpponentPattern
{
    None,
    JumpInSpam,
    LowAttackSpam,
    HighOverheadSpam,
    GuardBreakSpam,
    FastPokeSpam
}

public struct RecordedAction
{
    public GameAction Action;
    public float Timestamp;

    public RecordedAction(GameAction action, float time)
    {
        Action = action;
        Timestamp = time;
    }
}

public class OpponentPatternTracker
{
    private readonly List<RecordedAction> _actionHistory = new();
    private const float MEMORY_DURATION_SECONDS = 4.5f;
    private const int SPAM_THRESHOLD = 3;

    public void RecordAction(GameAction action, float currentTime)
    {
        _actionHistory.Add(new RecordedAction(action, currentTime));
        CleanOldHistory(currentTime);
    }

    public void CleanOldHistory(float currentTime)
    {
        _actionHistory.RemoveAll(a => (currentTime - a.Timestamp) > MEMORY_DURATION_SECONDS);
    }

    public OpponentPattern DetectSpamPattern()
    {
        if (CountAction(GameAction.Jump) >= SPAM_THRESHOLD) return OpponentPattern.JumpInSpam;
        if (CountAction(GameAction.LowAttack) >= SPAM_THRESHOLD) return OpponentPattern.LowAttackSpam;
        if (CountAction(GameAction.OverheadAttack) >= SPAM_THRESHOLD) return OpponentPattern.HighOverheadSpam;
        if (CountAction(GameAction.GuardBreak) >= 2) return OpponentPattern.GuardBreakSpam;
        if (CountAction(GameAction.FastPoke) >= SPAM_THRESHOLD + 1) return OpponentPattern.FastPokeSpam;
        return OpponentPattern.None;
    }

    private int CountAction(GameAction targetAction)
    {
        int count = 0;
        foreach (var record in _actionHistory)
        {
            if (record.Action == targetAction) count++;
        }
        return count;
    }
}