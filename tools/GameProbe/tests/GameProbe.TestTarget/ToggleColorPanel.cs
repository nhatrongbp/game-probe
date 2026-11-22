namespace GameProbe.TestTarget;

internal sealed class ToggleColorPanel : Panel
{
    private static readonly Color[] Palette =
    {
        Color.FromArgb(0x00, 0xA0, 0xFF),
        Color.FromArgb(0xFF, 0xD7, 0x00)
    };

    private readonly Font _titleFont = new("Segoe UI Semibold", 10f, FontStyle.Regular);
    private readonly Font _smallFont = new("Consolas", 8f, FontStyle.Regular);

    private int _index;

    public ToggleColorPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(24, 27, 33);
    }

    public event EventHandler? Toggled;

    public Color CurrentColor => Palette[_index];

    public string CurrentHex => $"#{CurrentColor.R:X2}{CurrentColor.G:X2}{CurrentColor.B:X2}";

    public void Toggle()
    {
        _index = (_index + 1) % Palette.Length;
        Invalidate();
        Toggled?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            Toggle();
        }

        base.OnMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(BackColor);

        var rect = new Rectangle(4, 4, Width - 9, Height - 9);
        using (var brush = new SolidBrush(CurrentColor))
        {
            e.Graphics.FillRectangle(brush, rect);
        }

        using (var border = new Pen(Color.FromArgb(90, 255, 255, 255)))
        {
            e.Graphics.DrawRectangle(border, rect);
        }

        var textColor = GetTextColor(CurrentColor);
        var titleRect = new Rectangle(rect.X + 6, rect.Y + 8, rect.Width - 12, 22);
        var hexRect = new Rectangle(rect.X + 6, rect.Y + 34, rect.Width - 12, 20);
        var hintRect = new Rectangle(rect.X + 6, rect.Bottom - 28, rect.Width - 12, 20);

        TextRenderer.DrawText(
            e.Graphics,
            "TOGGLE",
            _titleFont,
            titleRect,
            textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);

        TextRenderer.DrawText(
            e.Graphics,
            CurrentHex,
            _smallFont,
            hexRect,
            textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);

        TextRenderer.DrawText(
            e.Graphics,
            "click to change",
            _smallFont,
            hintRect,
            textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _smallFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Color GetTextColor(Color background)
    {
        var luminance = 0.299 * background.R + 0.587 * background.G + 0.114 * background.B;
        return luminance > 140 ? Color.FromArgb(20, 20, 20) : Color.White;
    }
}
