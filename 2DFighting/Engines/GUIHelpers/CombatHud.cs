using System.Numerics;
using Raylib_cs;

public static class CombatHUD
{
    private static readonly RenderMaterial TimerPanelMaterial = new() { ShaderId = BuiltInShaders.UiButtonId };
    private static readonly Vector3 TimerAccentColor = new(1.0f, 0.82f, 0.28f);

    private static UIBarStyle P1HealthStyle = new()
    {
        BackgroundColor = new Color(22, 24, 34, 225),
        FillColor = new Color(235, 45, 60, 255),
        BorderColor = new Color(220, 225, 240, 255),
        BorderThickness = 2.2f,
        RightToLeft = false
    };

    private static UIBarStyle P2HealthStyle = new()
    {
        BackgroundColor = new Color(22, 24, 34, 225),
        FillColor = new Color(50, 135, 255, 255),
        BorderColor = new Color(220, 225, 240, 255),
        BorderThickness = 2.2f,
        RightToLeft = true
    };

    private static UIBarStyle CooldownReadyStyle = new()
    {
        BackgroundColor = new Color(30, 30, 30, 200),
        FillColor = Color.DARKGREEN,
        BorderColor = Color.WHITE,
        BorderThickness = 1f
    };

    private static UIBarStyle CooldownChargingStyle = new()
    {
        BackgroundColor = new Color(30, 30, 30, 200),
        FillColor = Color.ORANGE,
        BorderColor = Color.GRAY,
        BorderThickness = 1f
    };

    public static void Draw(GameData data)
    {
        if (data.Player1 == null || data.Player2 == null) return;

        MatchManager? match = data.Services?.Match;

        DrawHealthBars(data.Player1, data.Player2);
        DrawRoundPips(data.Player1, data.Player2);
        DrawRoundTimer(match);
        DrawSkillCooldowns(data.Player1, xPct: 0.10f, rightAlign: false);
        DrawSkillCooldowns(data.Player2, xPct: 0.90f, rightAlign: true);
        DrawComboCounter(data.Player1, xPct: 0.10f, rightAlign: false);
        DrawComboCounter(data.Player2, xPct: 0.90f, rightAlign: true);
        DrawRoundAnnouncement(match);
    }

    private static float _p1TrailPct = 1.0f;
    private static float _p2TrailPct = 1.0f;
    private static int _lastTimerSec = -1;
    private static string _cachedTimerText = "00";
    private static int _lastRound = -1;
    private static string _cachedRoundLabel = "ROUND 1";
    private static int _lastP1Combo = -1;
    private static string _cachedP1ComboText = "";
    private static int _lastP2Combo = -1;
    private static string _cachedP2ComboText = "";

    private static void DrawHealthBars(Player p1, Player p2)
    {
        float p1Pct = Math.Clamp(p1.Health / (float)Player.MAX_HEALTH, 0f, 1f);
        float p2Pct = Math.Clamp(p2.Health / (float)Player.MAX_HEALTH, 0f, 1f);

        float dt = Raylib.GetFrameTime();

        if (p1Pct > _p1TrailPct) _p1TrailPct = p1Pct;
        else if (!p1.IsInHitstun && _p1TrailPct > p1Pct)
            _p1TrailPct = MathF.Max(p1Pct, _p1TrailPct - (dt * 0.45f));

        if (p2Pct > _p2TrailPct) _p2TrailPct = p2Pct;
        else if (!p2.IsInHitstun && _p2TrailPct > p2Pct)
            _p2TrailPct = MathF.Max(p2Pct, _p2TrailPct - (dt * 0.45f));

        Rectangle p1Rect = UIRenderer.GetPctRect(0.10f, 0.05f, 0.32f, 0.045f);
        Rectangle p2Rect = UIRenderer.GetPctRect(0.58f, 0.05f, 0.32f, 0.045f);

        UIRenderer.DrawProgressBar(p1Rect, p1Pct, _p1TrailPct, P1HealthStyle);
        UIRenderer.DrawProgressBar(p2Rect, p2Pct, _p2TrailPct, P2HealthStyle);
    }

    private static void DrawRoundPips(Player p1, Player p2)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        float radius = sh * 0.011f;
        int y = (int)(sh * 0.03f);
        int spacing = (int)(radius * 2.6f);

        for (int i = 0; i < MatchManager.RoundsToWin; i++)
        {
            int p1PipX = (int)(sw * 0.41f) - (i * spacing);
            Color p1Col = i < p1.RoundsWon ? Color.GOLD : Color.DARKGRAY;
            Raylib.DrawCircle(p1PipX, y, radius, p1Col);
            Raylib.DrawCircleLines(p1PipX, y, radius, Color.BLACK);

            int p2PipX = (int)(sw * 0.59f) + (i * spacing);
            Color p2Col = i < p2.RoundsWon ? Color.GOLD : Color.DARKGRAY;
            Raylib.DrawCircle(p2PipX, y, radius, p2Col);
            Raylib.DrawCircleLines(p2PipX, y, radius, Color.BLACK);
        }
    }

    private static void DrawRoundTimer(MatchManager? match)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();

        Rectangle box = UIRenderer.GetPctRect(0.46f, 0.04f, 0.08f, 0.065f);
        UIRenderer.DrawStyledPanel(
            box,
            TimerPanelMaterial,
            texture: null,
            hover: 0f,
            isSelected: false,
            TimerAccentColor,
            new Color(28, 30, 42, 235),
            Color.GOLD
        );

        float roundTimer = match?.RoundTimer ?? PhysicsEngine.RoundTimer;
        int currentRound = match?.CurrentRound ?? PhysicsEngine.CurrentRound;

        int timerSec = (int)MathF.Ceiling(roundTimer);
        if (timerSec != _lastTimerSec)
        {
            _lastTimerSec = timerSec;
            _cachedTimerText = timerSec.ToString("00");
        }
        string timeText = _cachedTimerText;
        int fontSize = (int)(sh * 0.045f);
        int textW = UIRenderer.MeasureText(timeText, fontSize);
        UIRenderer.DrawTextShadowed(timeText, (sw - textW) * 0.5f, box.y + (box.height - fontSize) * 0.5f, fontSize, Color.WHITE);

        int roundFont = Math.Max(12, (int)(sh * 0.02f));
        if (currentRound != _lastRound)
        {
            _lastRound = currentRound;
            _cachedRoundLabel = $"ROUND {currentRound}";
        }
        string roundLabel = _cachedRoundLabel;
        int rw = UIRenderer.MeasureText(roundLabel, roundFont);
        UIRenderer.DrawTextShadowed(roundLabel, (sw - rw) * 0.5f, box.y + box.height + 6f, roundFont, new Color(215, 220, 235, 255));
    }

    private static void DrawRoundAnnouncement(MatchManager? match)
    {
        string announcement = match?.RoundAnnouncement ?? PhysicsEngine.RoundAnnouncement;
        if (string.IsNullOrEmpty(announcement)) return;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int fontSize = (int)(sh * 0.055f);
        int textW = UIRenderer.MeasureText(announcement, fontSize);
        float x = (sw - textW) * 0.5f;
        float y = sh * 0.35f;

        UIRenderer.DrawTextShadowed(announcement, x, y, fontSize, Color.GOLD, shadowOffset: 3f);
    }

    private static void DrawSkillCooldowns(Player player, float xPct, bool rightAlign)
    {
        if (player.CurrentCharacter == null) return;

        int sh = Raylib.GetScreenHeight();
        int fontSize = Math.Max(10, (int)(sh * 0.016f));
        float slotW = 0.042f;
        float slotH = 0.022f;
        float spacing = 0.006f;
        float yPct = 0.105f;

        var skills = player.CurrentCharacter.SkillController.SkillList;
        int index = 0;
        for (int i = 0; i < skills.Count; i++)
        {
            var skill = skills[i];
            if (skill.CooldownTime <= 0f) continue;

            float currentCd = skill.CurrentCooldown;
            float readyPct = 1f - Math.Clamp(currentCd / skill.CooldownTime, 0f, 1f);

            float slotXPct = rightAlign
                ? xPct - ((index + 1) * slotW) - (index * spacing)
                : xPct + (index * (slotW + spacing));

            Rectangle slotRect = UIRenderer.GetPctRect(slotXPct, yPct, slotW, slotH);
            UIBarStyle style = currentCd <= 0f ? CooldownReadyStyle : CooldownChargingStyle;
            style.RightToLeft = rightAlign;

            UIRenderer.DrawProgressBar(slotRect, readyPct, style);

            UIRenderer.DrawTextShadowed(skill.ShortLabel, slotRect.x + 6f, slotRect.y + 2f, fontSize, Color.WHITE, shadowOffset: 1f);

            index++;
        }
    }

    private static void DrawComboCounter(Player player, float xPct, bool rightAlign)
    {
        if (player.ComboCount < 2) return;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int fontSize = (int)(sh * 0.045f);
        string text;
        if (!rightAlign)
        {
            if (player.ComboCount != _lastP1Combo)
            {
                _lastP1Combo = player.ComboCount;
                _cachedP1ComboText = $"{player.ComboCount} HIT COMBO!";
            }
            text = _cachedP1ComboText;
        }
        else
        {
            if (player.ComboCount != _lastP2Combo)
            {
                _lastP2Combo = player.ComboCount;
                _cachedP2ComboText = $"{player.ComboCount} HIT COMBO!";
            }
            text = _cachedP2ComboText;
        }
        int textW = UIRenderer.MeasureText(text, fontSize);

        float drawX = rightAlign ? (sw * xPct) - textW : (sw * xPct);
        float drawY = sh * 0.16f;

        UIRenderer.DrawTextShadowed(text, drawX, drawY, fontSize, Color.GOLD, shadowOffset: 2f);
    }
}