using Raylib_cs;

public static class CombatHUD
{
    private static UIBarStyle P1HealthStyle = new()
    {
        BackgroundColor = Color.GRAY,
        FillColor = Color.RED,
        BorderColor = Color.BLACK,
        BorderThickness = 2f,
        RightToLeft = false
    };

    private static UIBarStyle P2HealthStyle = new()
    {
        BackgroundColor = Color.GRAY,
        FillColor = Color.BLUE,
        BorderColor = Color.BLACK,
        BorderThickness = 2f,
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

        DrawHealthBars(data.Player1, data.Player2);
        DrawRoundPips(data.Player1, data.Player2);
        DrawRoundTimer();
        DrawSkillCooldowns(data.Player1, xPct: 0.10f, rightAlign: false);
        DrawSkillCooldowns(data.Player2, xPct: 0.90f, rightAlign: true);
        DrawComboCounter(data.Player1, xPct: 0.10f, rightAlign: false);
        DrawComboCounter(data.Player2, xPct: 0.90f, rightAlign: true);
        DrawRoundAnnouncement();
    }

    private static void DrawHealthBars(Player p1, Player p2)
    {
        float p1Pct = p1.Health / (float)Player.MAX_HEALTH;
        float p2Pct = p2.Health / (float)Player.MAX_HEALTH;

        Rectangle p1Rect = UIRenderer.GetPctRect(0.10f, 0.05f, 0.32f, 0.045f);
        Rectangle p2Rect = UIRenderer.GetPctRect(0.58f, 0.05f, 0.32f, 0.045f);

        UIRenderer.DrawProgressBar(p1Rect, p1Pct, P1HealthStyle);
        UIRenderer.DrawProgressBar(p2Rect, p2Pct, P2HealthStyle);
    }

    private static void DrawRoundPips(Player p1, Player p2)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        float radius = sh * 0.011f;
        int y = (int)(sh * 0.03f);
        int spacing = (int)(radius * 2.6f);

        for (int i = 0; i < PhysicsEngine.ROUNDS_TO_WIN; i++)
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

    private static void DrawRoundTimer()
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();

        Rectangle box = UIRenderer.GetPctRect(0.46f, 0.04f, 0.08f, 0.065f);
        Raylib.DrawRectangleRec(box, new Color(40, 40, 40, 230));
        Raylib.DrawRectangleLinesEx(box, 2f, Color.GOLD);

        string timeText = ((int)MathF.Ceiling(PhysicsEngine.RoundTimer)).ToString("00");
        int fontSize = (int)(sh * 0.045f);
        int textW = Raylib.MeasureText(timeText, fontSize);
        Raylib.DrawText(timeText, (sw - textW) / 2, (int)(box.y + (box.height - fontSize) / 2f), fontSize, Color.WHITE);

        int roundFont = Math.Max(12, (int)(sh * 0.02f));
        string roundLabel = $"ROUND {PhysicsEngine.CurrentRound}";
        int rw = Raylib.MeasureText(roundLabel, roundFont);
        Raylib.DrawText(roundLabel, (sw - rw) / 2, (int)(box.y + box.height + 6), roundFont, Color.DARKGRAY);
    }

    private static void DrawRoundAnnouncement()
    {
        if (string.IsNullOrEmpty(PhysicsEngine.RoundAnnouncement)) return;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int fontSize = (int)(sh * 0.055f);
        int textW = Raylib.MeasureText(PhysicsEngine.RoundAnnouncement, fontSize);
        int x = (sw - textW) / 2;
        int y = (int)(sh * 0.35f);

        Raylib.DrawText(PhysicsEngine.RoundAnnouncement, x + 3, y + 3, fontSize, Color.BLACK);
        Raylib.DrawText(PhysicsEngine.RoundAnnouncement, x, y, fontSize, Color.GOLD);
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

        int index = 0;
        foreach (var skill in player.CurrentCharacter.Skills.Values)
        {
            if (skill.CooldownTime <= 0f) continue;

            float currentCd = player.CurrentCharacter.SkillCooldowns.TryGetValue(skill.Name, out float cd) ? cd : 0f;
            float readyPct = 1f - Math.Clamp(currentCd / skill.CooldownTime, 0f, 1f);

            float slotXPct = rightAlign
                ? xPct - ((index + 1) * slotW) - (index * spacing)
                : xPct + (index * (slotW + spacing));

            Rectangle slotRect = UIRenderer.GetPctRect(slotXPct, yPct, slotW, slotH);
            UIBarStyle style = currentCd <= 0f ? CooldownReadyStyle : CooldownChargingStyle;
            style.RightToLeft = rightAlign;

            UIRenderer.DrawProgressBar(slotRect, readyPct, style);

            Raylib.DrawText(skill.ShortLabel, (int)slotRect.x + 4, (int)slotRect.y + 2, fontSize, Color.WHITE);

            index++;
        }
    }

    private static void DrawComboCounter(Player player, float xPct, bool rightAlign)
    {
        if (player.ComboCount < 2) return;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        int fontSize = (int)(sh * 0.045f);
        string text = $"{player.ComboCount} HIT COMBO!";
        int textW = Raylib.MeasureText(text, fontSize);

        int drawX = rightAlign ? (int)(sw * xPct) - textW : (int)(sw * xPct);
        int drawY = (int)(sh * 0.16f);

        Raylib.DrawText(text, drawX + 2, drawY + 2, fontSize, Color.BLACK);
        Raylib.DrawText(text, drawX, drawY, fontSize, Color.GOLD);
    }
}