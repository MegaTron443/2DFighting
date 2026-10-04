using Raylib_cs;
public class SkillData
{
    public string Name { get; set; } = "";
    public string InputKey { get; set; } = "";
    public int Damage { get; set; }
    public int StartupFrames { get; set; }
    public int ActiveFrames { get; set; }
    public int RecoveryFrames { get; set; }
    public Rectangle HitboxOffset { get; set; }
    public float HitStun { get; set; }
    public float CooldownTime { get; set; }
}