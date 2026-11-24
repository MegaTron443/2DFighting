using System.Numerics;
using Raylib_cs;

public enum LobbyRoom
{
    Center,
    LeftOffline,
    RightDev,
    BottomTraining
}

public static class CameraManager
{
    public static CameraConfig Config { get; } = new();
    public static Camera2D RawCamera { get; private set; }
    public static Vector2 Target => _currentTarget;
    public static float Zoom => _currentZoom;

    private static Vector2 _currentTarget;
    private static float _currentZoom = 1f;

    private static Vector2? _overrideTarget;
    private static float? _overrideZoom;
    private static float _overrideTimer;

    public static LobbyRoom CurrentLobbyRoom { get; private set; } = LobbyRoom.Center;
    public static bool IsInLobbyAlcove => CurrentLobbyRoom == LobbyRoom.RightDev;

    public static void Initialize() => Reset();

    public static void Reset()
    {
        float sw = Raylib.GetScreenWidth();
        float sh = Raylib.GetScreenHeight();

        _currentTarget = new Vector2(sw * 0.5f, sh * 0.5f);
        _currentZoom = 1f;
        CurrentLobbyRoom = LobbyRoom.Center;
        ClearOverride();

        RawCamera = new Camera2D
        {
            offset = new Vector2(sw * 0.5f, sh * 0.5f),
            target = _currentTarget,
            rotation = 0f,
            zoom = 1f
        };
    }

    public static void UpdateLobby(float dt, Vector2 playerPos, Scene? scene)
    {
        float sw = Raylib.GetScreenWidth();
        float sh = Raylib.GetScreenHeight();

        if (scene?.HasTrainingLab == true && playerPos.Y > sh)
        {
            CurrentLobbyRoom = LobbyRoom.BottomTraining;
        }
        else if (scene?.HasOfflineWing == true && playerPos.X < 0f)
        {
            CurrentLobbyRoom = LobbyRoom.LeftOffline;
        }
        else if (scene?.HasDeveloperAlcove == true && playerPos.X > sw)
        {
            CurrentLobbyRoom = LobbyRoom.RightDev;
        }
        else
        {
            CurrentLobbyRoom = LobbyRoom.Center;
        }

        Vector2 desiredTarget;
        float desiredZoom;

        if (_overrideTimer > 0f && _overrideTarget.HasValue)
        {
            _overrideTimer -= dt;
            desiredTarget = _overrideTarget.Value;
            desiredZoom = _overrideZoom ?? _currentZoom;
            if (_overrideTimer <= 0f) ClearOverride();
        }
        else
        {
            desiredTarget = CurrentLobbyRoom switch
            {
                LobbyRoom.LeftOffline    => new Vector2(sw * -0.50f, sh * 0.50f),
                LobbyRoom.RightDev       => new Vector2(sw * 1.50f, sh * 0.50f),
                LobbyRoom.BottomTraining => new Vector2(sw * 0.50f, sh * 1.50f),
                _                        => new Vector2(sw * 0.50f, sh * 0.50f)
            };
            desiredZoom = 1.0f;
        }

        float moveT = 1f - MathF.Exp(-Config.MoveSmoothSpeed * dt);
        float zoomT = 1f - MathF.Exp(-Config.ZoomSmoothSpeed * dt);

        _currentTarget = Vector2.Lerp(_currentTarget, desiredTarget, moveT);
        _currentZoom = float.Lerp(_currentZoom, desiredZoom, zoomT);

        ApplyToRaylibCamera(scene?.Bounds);
    }

    public static void Update(float dt, Vector2 p1Pos, Vector2 p2Pos, Rectangle? arenaBounds = null)
    {
        Vector2 desiredTarget;
        float desiredZoom;

        if (_overrideTimer > 0f && _overrideTarget.HasValue)
        {
            _overrideTimer -= dt;
            desiredTarget = _overrideTarget.Value;
            desiredZoom = _overrideZoom ?? _currentZoom;
            if (_overrideTimer <= 0f) ClearOverride();
        }
        else
        {
            (desiredTarget, desiredZoom) = ComputeTrackingState(p1Pos, p2Pos);
        }

        float moveT = 1f - MathF.Exp(-Config.MoveSmoothSpeed * dt);
        float zoomT = 1f - MathF.Exp(-Config.ZoomSmoothSpeed * dt);

        _currentTarget = Vector2.Lerp(_currentTarget, desiredTarget, moveT);
        _currentZoom = float.Lerp(_currentZoom, desiredZoom, zoomT);

        ApplyToRaylibCamera(arenaBounds);
    }

    private static (Vector2 target, float zoom) ComputeTrackingState(Vector2 p1, Vector2 p2)
    {
        float sw = Raylib.GetScreenWidth();
        float sh = Raylib.GetScreenHeight();

        float usableW = sw * Math.Clamp(1f - Config.PaddingXPercent, 0.1f, 1f);
        float usableH = sh * Math.Clamp(1f - Config.PaddingYPercent, 0.1f, 1f);

        float distX = MathF.Max(MathF.Abs(p1.X - p2.X), 0.001f);
        float distY = MathF.Max(MathF.Abs(p1.Y - p2.Y), 0.001f);

        float zoomX = usableW / distX;
        float zoomY = usableH / distY;

        float desiredZoom = Math.Clamp(MathF.Min(zoomX, zoomY), Config.MinZoom, Config.MaxZoom);

        Vector2 midpoint = (p1 + p2) * 0.5f;
        midpoint.Y += (sh / desiredZoom) * Config.VerticalBiasPercent;

        return (midpoint, desiredZoom);
    }

    public static void SnapTo(Vector2 p1Pos, Vector2 p2Pos, Rectangle? arenaBounds = null)
    {
        ClearOverride();
        (_currentTarget, _currentZoom) = ComputeTrackingState(p1Pos, p2Pos);
        ApplyToRaylibCamera(arenaBounds);
    }

    private static void ApplyToRaylibCamera(Rectangle? arenaBounds)
    {
        float sw = Raylib.GetScreenWidth();
        float sh = Raylib.GetScreenHeight();

        Vector2 clamped = _currentTarget;

        if (arenaBounds.HasValue)
        {
            Rectangle b = arenaBounds.Value;
            float halfW = (sw * 0.5f) / _currentZoom;
            float halfH = (sh * 0.5f) / _currentZoom;

            clamped.X = b.width <= halfW * 2f
                ? b.x + b.width * 0.5f
                : Math.Clamp(clamped.X, b.x + halfW, b.x + b.width - halfW);

            clamped.Y = b.height <= halfH * 2f
                ? b.y + b.height * 0.5f
                : Math.Clamp(clamped.Y, b.y + halfH, b.y + b.height - halfH);
        }

        RawCamera = new Camera2D
        {
            offset = new Vector2(sw * 0.5f, sh * 0.5f),
            target = clamped,
            rotation = 0f,
            zoom = _currentZoom
        };
    }

    public static void BeginMode() => Raylib.BeginMode2D(RawCamera);
    public static void EndMode() => Raylib.EndMode2D();

    public static void ClearOverride()
    {
        _overrideTimer = 0f;
        _overrideTarget = null;
        _overrideZoom = null;
    }


    public static void TriggerFocusFromLua(float x, float y, float zoom, float durationSec)
    {
        _overrideTarget = new Vector2(x, y);
        _overrideZoom = Math.Clamp(zoom, Config.MinZoom, Config.MaxZoom);
        _overrideTimer = durationSec;
    }

    public static void ClearFocusFromLua() => ClearOverride();

    public static void SetZoomLimitsFromLua(float minZoom, float maxZoom)
    {
        Config.MinZoom = minZoom;
        Config.MaxZoom = maxZoom;
    }

    public static float GetZoomFromLua() => _currentZoom;
}