using Raylib_cs;

public class UIGrid
{
    public float XPct { get; set; }
    public float YPct { get; set; }
    public float WidthPct { get; set; }
    public float HeightPct { get; set; }
    public int Columns { get; }
    public int Rows { get; }
    public float SpacingPct { get; set; }

    public TextAlignment DefaultAlignment { get; set; } = TextAlignment.Center;

    private readonly List<(string text, Action<ButtonContext> onClick, Texture2D? icon, TextAlignment? alignment)> _items = new();

    public UIGrid(float xPct, float yPct, float widthPct, float heightPct, int columns, int rows, float spacingPct = 0.015f)
    {
        XPct = xPct;
        YPct = yPct;
        WidthPct = widthPct;
        HeightPct = heightPct;
        Columns = columns;
        Rows = rows;
        SpacingPct = spacingPct;
    }

    public UIGrid WithAlignment(TextAlignment alignment)
    {
        DefaultAlignment = alignment;
        return this;
    }

    public UIGrid Add(string text, Action<ButtonContext> onClick, Texture2D? icon = null, TextAlignment? alignment = null)
    {
        if (_items.Count >= Columns * Rows)
            throw new InvalidOperationException($"Grid limit of {Columns * Rows} cells reached.");

        _items.Add((text, onClick, icon, alignment));
        return this;
    }

    public List<Button> Build()
    {
        var buttons = new List<Button>();
        int totalCells = _items.Count;
        if (totalCells == 0) return buttons;

        float totalSpacingX = SpacingPct * (Columns - 1);
        float totalSpacingY = SpacingPct * (Rows - 1);

        float cellWidth = (WidthPct - totalSpacingX) / Columns;
        float cellHeight = (HeightPct - totalSpacingY) / Rows;

        for (int i = 0; i < totalCells; i++)
        {
            int col = i % Columns;
            int row = i / Columns;

            float cellX = XPct + col * (cellWidth + SpacingPct);
            float cellY = YPct + row * (cellHeight + SpacingPct);

            var (text, onClick, icon, alignment) = _items[i];

            TextAlignment resolvedAlignment = alignment ?? DefaultAlignment;

            var button = new Button(
                cellX, 
                cellY, 
                cellWidth, 
                cellHeight, 
                text, 
                onClick, 
                icon, 
                isCentered: false
            ).WithAlignment(resolvedAlignment);

            buttons.Add(button);
        }

        return buttons;
    }
}