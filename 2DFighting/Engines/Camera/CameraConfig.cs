public class CameraConfig
{
    public float MinZoom { get; set; } = 0.85f;
    public float MaxZoom { get; set; } = 1.35f;
    public float ZoomSmoothSpeed { get; set; } = 6.0f;
    public float MoveSmoothSpeed { get; set; } = 8.0f;

    public float PaddingXPercent { get; set; } = 0.25f;
    public float PaddingYPercent { get; set; } = 0.25f;

    public float VerticalBiasPercent { get; set; } = -0.05f;
}