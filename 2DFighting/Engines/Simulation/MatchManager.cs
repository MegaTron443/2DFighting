using System;

public class MatchManager
{
    public const int RoundsToWin = 2;
    public const float DefaultRoundDuration = 99f;
    private const float RoundTransitionDuration = 2.0f;
    private const float TrainingTransitionDuration = 0.05f;

    public bool IsTrainingMode { get; set; } = false;
    public string CurrentMatchMode { get; set; } = "1v1_Local";
    public MatchTelemetryTracker Telemetry { get; } = new();
    public int CurrentRound { get; private set; } = 1;
    public float RoundTimer { get; set; } = DefaultRoundDuration;
    public float RoundTransitionTimer { get; private set; } = 0f;
    public string RoundAnnouncement { get; private set; } = "";
    public string MatchWinnerText { get; private set; } = "";
    public float HitstopTimer { get; set; } = 0f;

    public void AddHitstop(float seconds) => HitstopTimer = MathF.Max(HitstopTimer, seconds);

    public bool TickRoundTransition(GameData data, ProjectileManager projectiles, float dt)
    {
        if (RoundTransitionTimer <= 0f) return false;

        Player p1 = data.Player1!;
        Player p2 = data.Player2!;

        p1.SavePreviousState();
        p2.SavePreviousState();
        RoundTransitionTimer -= dt;

        if (RoundTransitionTimer <= 0f)
        {
            if (p1.RoundsWon >= RoundsToWin || p2.RoundsWon >= RoundsToWin)
            {
                if (!Telemetry.HasRecordedCurrentMatch)
                {
                    LeaderboardDatabase.RecordMatch(Telemetry.FinalizeMatch(p1, p2));
                }

                if (IsTrainingMode)
                {
                    string[] roster = data.Services?.AvailableFighters ?? GUIEngine.RosterIds;
                    var rng = Random.Shared;
                    p1.ChangeCharacter(data.LuaEngine.CreateCharacter(roster[rng.Next(roster.Length)], slot: 0));
                    p2.ChangeCharacter(data.LuaEngine.CreateCharacter(roster[rng.Next(roster.Length)], slot: 1));
                    ResetMatch(p1, p2, projectiles);
                }
                else
                {
                    MatchWinnerText = p1.RoundsWon > p2.RoundsWon
                        ? $"{p1.Name} ({p1.CurrentCharacter!.Name}) WINS!"
                        : $"{p2.Name} ({p2.CurrentCharacter!.Name}) WINS!";
                    GameState.SetState(State.GameOver);
                }
            }
            else
            {
                CurrentRound++;
                StartNextRound(p1, p2, projectiles);
            }
        }

        return true;
    }

    public bool TickHitstop(Player p1, Player p2, float dt)
    {
        if (HitstopTimer <= 0f) return false;

        p1.SavePreviousState();
        p2.SavePreviousState();
        HitstopTimer -= dt;
        return true;
    }

    public void TickRoundTimer(float dt) => RoundTimer = MathF.Max(0f, RoundTimer - dt);

    public void CheckRoundEndCondition(GameData data, Player p1, Player p2)
    {
        bool p1Dead = !p1.IsAlive;
        bool p2Dead = !p2.IsAlive;
        bool timeOut = RoundTimer <= 0f;

        if (p1Dead) data.SoundHandler?.PlaySound(p1, SoundType.Death);
        else if (p2Dead) data.SoundHandler?.PlaySound(p2, SoundType.Death);

        if (!p1Dead && !p2Dead && !timeOut)
            return;

        bool isDoubleKO = p1Dead && p2Dead;
        if (isDoubleKO)
        {
            if (p1.RoundsWon < RoundsToWin - 1) p1.RoundsWon++;
            if (p2.RoundsWon < RoundsToWin - 1) p2.RoundsWon++;
            RoundAnnouncement = "DOUBLE K.O.!";
        }
        else if (p1Dead)
        {
            p2.RoundsWon++;
            RoundAnnouncement = $"K.O.! {p2.Name} takes Round {CurrentRound}!";
        }
        else if (p2Dead)
        {
            p1.RoundsWon++;
            RoundAnnouncement = $"K.O.! {p1.Name} takes Round {CurrentRound}!";
        }
        else if (timeOut)
        {
            if (p1.Health > p2.Health)
            {
                p1.RoundsWon++;
                RoundAnnouncement = $"TIME OVER! {p1.Name} takes Round {CurrentRound}!";
            }
            else if (p2.Health > p1.Health)
            {
                p2.RoundsWon++;
                RoundAnnouncement = $"TIME OVER! {p2.Name} takes Round {CurrentRound}!";
            }
            else
            {
                RoundAnnouncement = "TIME OVER! DRAW ROUND!";
            }
        }

        bool p1WonRound = !isDoubleKO && (p2Dead || (timeOut && p1.Health > p2.Health));
        bool p2WonRound = !isDoubleKO && (p1Dead || (timeOut && p2.Health > p1.Health));

        p1.NotifyRoundEnd(p1WonRound);
        p2.NotifyRoundEnd(p2WonRound);

        RoundTransitionTimer = IsTrainingMode ? TrainingTransitionDuration : RoundTransitionDuration;
    }

    public void ResetMatch(Player p1, Player p2, ProjectileManager projectiles, IPlayerIdentityProvider? identity = null)
    {
        CurrentRound = 1;
        p1.RoundsWon = 0;
        p2.RoundsWon = 0;
        MatchWinnerText = "";
        Telemetry.BeginMatch(p1, p2, CurrentMatchMode, ModManager.ActiveModFilter, identity ?? EngineServices.Current?.Identity);
        StartNextRound(p1, p2, projectiles);
    }

    public void StartNextRound(Player p1, Player p2, ProjectileManager projectiles)
    {
        projectiles.Clear();
        HitstopTimer = 0f;
        RoundTimer = DefaultRoundDuration;
        RoundTransitionTimer = 0f;
        RoundAnnouncement = "";
        p1.ResetState(faceRight: true);
        p2.ResetState(faceRight: false);
        if (!PhysicsEngine.IsHeadlessThread)
        {
            PostFXManager.ResetAll();
            VFXManager.Clear();
            CameraManager.SnapTo(p1.Position, p2.Position, SceneManager.CurrentScene?.Bounds);
        }
    }
}
