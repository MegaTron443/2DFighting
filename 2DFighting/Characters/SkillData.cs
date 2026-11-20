using Raylib_cs;
using MoonSharp.Interpreter;

[MoonSharpUserData]
public class SkillData
{
    public string Name { get; set; } = "";
    public string? AnimationClipName { get; set; } = null;
    public int TriggerMask { get; set; }
    public int Damage { get; set; }
    public AttackHeight Height { get; set; } = AttackHeight.Mid;
    public int StartupFrames { get; set; }
    public int ActiveFrames { get; set; }
    public int RecoveryFrames { get; set; }
    public bool IsHyperFrames { get; set; }
    public int HyperStartFrame { get; set; }
    public int HyperFrames { get; set; }
    public Rectangle HitboxOffsetPct { get; set; }
    public float HitStun { get; set; }
    public float CooldownTime { get; set; }
    public bool IsBlockBreak { get; set; }
    public DynValue? OnBlockBreak { get; set; }
    public bool IsCounter { get; set; }
    public DynValue? OnCounter { get; set; }
    public bool CustomLogic { get; set; }
    public DynValue? OnCustomLogic { get; set; }
    public DynValue? OnHit { get; set; }
    public string ShortLabel { get; set; } = "";
    public void SetHitbox(float xOffsetPct, float yOffsetPct, float widthPct, float heightPct)
    {
        HitboxOffsetPct = new Rectangle(xOffsetPct, yOffsetPct, widthPct, heightPct);
    }
}

public enum AttackHeight
{
    Mid,
    Low,
    Overhead
}