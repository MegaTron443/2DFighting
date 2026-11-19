using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

public struct AfterimageSnapshot
{
    public Vector2 Position;
    public bool FacingRight;
    public int AtlasFrame;
    public float Alpha;
    public Vector3 TintColor;
    public float WeaponAngleDeg;
}

public class AfterimageSystem
{
    private const int InitialCapacity = 8;
    private const int MaxSnapshots = 6;
    private const float FadeSpeed = 3.8f;
    private const float MinAlphaThreshold = 0.02f;
    private const float InitialAlpha = 0.65f;

    private readonly List<AfterimageSnapshot> _snapshots = new(InitialCapacity);

    public bool EmitAfterimages { get; set; } = false;
    public Vector3 AfterimageTint { get; set; } = new Vector3(0.65f, 0.20f, 1.0f);

    public void RequestAfterimage(float r = 165f, float g = 50f, float b = 255f)
    {
        AfterimageTint = (r > 1f || g > 1f || b > 1f)
            ? new Vector3(r / 255f, g / 255f, b / 255f)
            : new Vector3(r, g, b);
        EmitAfterimages = true;
    }

    public void StopEmission() => EmitAfterimages = false;

    public void Clear()
    {
        _snapshots.Clear();
        EmitAfterimages = false;
    }

    public void Update(Player p, int atlasFrame, KeyframeStep? currentKf, float dt)
    {
        bool shouldSnapshot = EmitAfterimages || (currentKf.HasValue && currentKf.Value.SpawnAfterimage);

        for (int i = _snapshots.Count - 1; i >= 0; i--)
        {
            var snap = _snapshots[i];
            snap.Alpha -= dt * FadeSpeed;
            if (snap.Alpha <= MinAlphaThreshold)
                _snapshots.RemoveAt(i);
            else
                _snapshots[i] = snap;
        }

        if (shouldSnapshot && _snapshots.Count < MaxSnapshots)
        {
            float wepAngle = currentKf?.WeaponAngleDeg ?? 0f;
            _snapshots.Add(new AfterimageSnapshot
            {
                Position = p.Position,
                FacingRight = p.FacingRight,
                AtlasFrame = atlasFrame,
                Alpha = InitialAlpha,
                TintColor = AfterimageTint,
                WeaponAngleDeg = wepAngle
            });
            EmitAfterimages = false;
        }
    }

    public void Draw(CharacterRenderer renderer, Character character)
    {
        bool hasSprite = renderer.HasSpriteSheet;
        for (int i = 0; i < _snapshots.Count; i++)
        {
            var snap = _snapshots[i];
            Color ghostCol = new Color(
                (int)(snap.TintColor.X * 255),
                (int)(snap.TintColor.Y * 255),
                (int)(snap.TintColor.Z * 255),
                (byte)Math.Clamp(snap.Alpha * 255f, 0f, 255f)
            );
            Rectangle ghostBox = new Rectangle(
                snap.Position.X - (character.Width / 2f),
                snap.Position.Y - (character.Height / 2f),
                character.Width,
                character.Height
            );

            if (hasSprite)
            {
                renderer.DrawAtlasSprite(character, renderer.SpriteSheet!.Value, snap.AtlasFrame, snap.FacingRight, ghostBox, ghostCol);
            }
            else
            {
                Raylib.DrawRectangleRec(ghostBox, ghostCol);
            }
        }
    }
}
