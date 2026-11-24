using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using Raylib_cs;

public class ProjectileManager
{
    private const int CounterProjectileHealAmount = 10;
    private const float ProjectileBlockKnockbackFactor = 0.65f;

    public List<Projectile> ActiveProjectiles { get; } = new();

    public void Spawn(Projectile p) => ActiveProjectiles.Add(p);

    public void Clear() => ActiveProjectiles.Clear();

    public void Update(float dt, MatchManager matchManager)
    {
        foreach (var proj in ActiveProjectiles)
        {
            proj.SavePreviousState();
            proj.Update(dt);
        }

        ResolveProjectileClashes();
        ResolveProjectileHits(matchManager);

        ActiveProjectiles.RemoveAll(p => !p.IsActive);
    }

    public void Draw(float alpha)
    {
        foreach (var proj in ActiveProjectiles)
        {
            proj.Draw(alpha);
        }
    }

    private void ResolveProjectileClashes()
    {
        for (int i = 0; i < ActiveProjectiles.Count; i++)
        {
            var a = ActiveProjectiles[i];
            if (!a.IsActive || !a.DestroyOnHit) continue;

            for (int j = i + 1; j < ActiveProjectiles.Count; j++)
            {
                var b = ActiveProjectiles[j];
                if (!b.IsActive || !b.DestroyOnHit || a.Owner == b.Owner) continue;

                if (Raylib.CheckCollisionRecs(a.GetHitbox(), b.GetHitbox()))
                {
                    a.Destroy();
                    b.Destroy();
                }
            }
        }
    }

    private void ResolveProjectileHits(MatchManager matchManager)
    {
        foreach (var proj in ActiveProjectiles)
        {
            if (!proj.IsActive || proj.HasHit) continue;

            Player defender = proj.Target;
            if (defender.IsInvulnerable) continue;

            if (Raylib.CheckCollisionRecs(proj.GetHitbox(), defender.GetHurtbox()))
            {
                proj.HasHit = true;

                // Active Counter/Parry absorbs projectile and heals
                Character? defChar = defender.CurrentCharacter;
                if (defChar?.ActiveSkill != null && defChar.ActiveSkill.IsCounter && defChar.IsHitboxActive)
                {
                    proj.Destroy();
                    defender.Health = Math.Min(Player.MAX_HEALTH, defender.Health + CounterProjectileHealAmount);
                    continue;
                }

                bool blocked = CombatResolver.CanBlockHeight(defender, proj.attackHeight, proj.IsBlockBreak);
                if (blocked)
                {
                    int nonLethalChip = CombatResolver.CalculateNonLethalChip(proj.Damage, defender.Health);
                    int prevCombo = proj.Owner.ComboCount;
                    CombatResolver.ApplyHitForce(
                        proj.Owner,
                        defender,
                        nonLethalChip,
                        proj.HitStun * CombatResolver.BlockStunMultiplier,
                        CombatResolver.DefaultBlockKnockbackPct * ProjectileBlockKnockbackFactor,
                        isBlocked: true
                    );
                    proj.Owner.ComboCount = prevCombo;
                }
                else
                {
                    if (!PhysicsEngine.IsHeadlessThread)
                    {
                        matchManager.AddHitstop(CombatResolver.LightHitstopDuration);
                    }
                    if (proj.OnHit != null && proj.OnHit.Type == DataType.Function)
                    {
                        try
                        {
                            proj.OnHit.Function.Call(proj.Owner, defender, proj);
                        }
                        catch (ScriptRuntimeException ex)
                        {
                            Console.WriteLine($"Error executing projectile OnHit function: {ex.DecoratedMessage}");
                        }
                    }
                    else
                    {
                        CombatResolver.ApplyHitForce(proj.Owner, defender, proj.Damage, proj.HitStun, proj.KnockbackPct);
                    }
                }

                VFXManager.SpawnPreset(blocked ? VFXPreset.BlockSpark : VFXPreset.HitSpark, proj.Position, proj.FacingRight);

                if (proj.DestroyOnHit)
                {
                    proj.Destroy();
                }
            }
        }
    }
}
