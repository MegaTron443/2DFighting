public enum OpponentPattern
    {
        None,
        ProjectileSpam,
        JumpInSpam,
        LowAttackSpam,
        HighOverheadSpam,
        RepeatedCombo
    }

    public struct RecordedAction
    {
        public GameAction Action;
        public SpecialMotion Motion;
        public float Timestamp;

        public RecordedAction(GameAction action, SpecialMotion motion, float time)
        {
            Action = action;
            Motion = motion;
            Timestamp = time;
        }
    }

    public class OpponentPatternTracker
    {
        private readonly List<RecordedAction> _actionHistory = new List<RecordedAction>();
        private readonly List<string> _comboHistory = new List<string>();
        
        private const float MEMORY_DURATION_SECONDS = 5.0f;
        private const int SPAM_THRESHOLD = 3;

        public void RecordAction(GameAction action, SpecialMotion motion, float currentTime)
        {
            _actionHistory.Add(new RecordedAction(action, motion, currentTime));
            CleanOldHistory(currentTime);
        }

        public void RecordComboSequence(string comboSignature)
        {
            _comboHistory.Add(comboSignature);
            if (_comboHistory.Count > 5)
            {
                _comboHistory.RemoveAt(0);
            }
        }

        public void CleanOldHistory(float currentTime)
        {
            _actionHistory.RemoveAll(a => (currentTime - a.Timestamp) > MEMORY_DURATION_SECONDS);
        }

        public OpponentPattern DetectSpamPattern()
        {
            if (IsSpammingProjectiles()) return OpponentPattern.ProjectileSpam;
            if (IsSpammingJumps()) return OpponentPattern.JumpInSpam;
            if (IsSpammingLowAttacks()) return OpponentPattern.LowAttackSpam;
            if (IsRepeatingSameCombo()) return OpponentPattern.RepeatedCombo;

            return OpponentPattern.None;
        }

        private bool IsSpammingProjectiles()
        {
            int fireballCount = 0;
            foreach (var record in _actionHistory)
            {
                if (record.Motion == SpecialMotion.QuarterCircleForward)
                {
                    fireballCount++;
                }
            }
            return fireballCount >= SPAM_THRESHOLD;
        }

        private bool IsSpammingJumps()
        {
            int jumpCount = 0;
            foreach (var record in _actionHistory)
            {
                if (record.Action == GameAction.Jump)
                {
                    jumpCount++;
                }
            }
            return jumpCount >= SPAM_THRESHOLD;
        }

        private bool IsSpammingLowAttacks()
        {
            int lowCount = 0;
            foreach (var record in _actionHistory)
            {
                if (record.Action == GameAction.CrouchAttack)
                {
                    lowCount++;
                }
            }
            return lowCount >= SPAM_THRESHOLD;
        }

        private bool IsRepeatingSameCombo()
        {
            if (_comboHistory.Count < SPAM_THRESHOLD) return false;

            string lastCombo = _comboHistory[_comboHistory.Count - 1];
            int matchCount = 0;

            for (int i = _comboHistory.Count - 1; i >= 0; i--)
            {
                if (_comboHistory[i] == lastCombo) matchCount++;
            }

            return matchCount >= SPAM_THRESHOLD;
        }
    }