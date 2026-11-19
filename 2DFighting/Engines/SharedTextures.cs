using Raylib_cs;

public static class SharedTextures
{
    private static Texture2D _whitePixel;

    public static Texture2D WhitePixel
    {
        get
        {
            if (_whitePixel.id == 0)
            {
                Image img = Raylib.GenImageColor(1, 1, Color.WHITE);
                _whitePixel = Raylib.LoadTextureFromImage(img);
                Raylib.UnloadImage(img);
            }
            return _whitePixel;
        }
    }

    public static void Dispose()
    {
        if (_whitePixel.id > 0)
        {
            Raylib.UnloadTexture(_whitePixel);
            _whitePixel = default;
        }
    }
}
