using System.Drawing.Drawing2D;

namespace GameProbe.TestTarget;

internal sealed class ColorGridPanel : Panel
{
    public const int Columns = 6;
    public const int Rows = 4;
    public const int CellWidth = 100;
    public const int CellHeight = 120;

    public static readonly ColorCell[] Cells =
    {
        new("White", "#FFFFFF", Color.FromArgb(0xFF, 0xFF, 0xFF)),
        new("Silver", "#C0C0C0", Color.FromArgb(0xC0, 0xC0, 0xC0)),
        new("Black", "#000000", Color.FromArgb(0x00, 0x00, 0x00)),
        new("Red", "#FF0000", Color.FromArgb(0xFF, 0x00, 0x00)),
        new("Green", "#00FF00", Color.FromArgb(0x00, 0xFF, 0x00)),
        new("Blue", "#0000FF", Color.FromArgb(0x00, 0x00, 0xFF)),
        new("Yellow", "#FFFF00", Color.FromArgb(0xFF, 0xFF, 0x00)),
        new("Cyan", "#00FFFF", Color.FromArgb(0x00, 0xFF, 0xFF)),
        new("Magenta", "#FF00FF", Color.FromArgb(0xFF, 0x00, 0xFF)),
        new("Gray", "#808080", Color.FromArgb(0x80, 0x80, 0x80)),
        new("Orange", "#FFA500", Color.FromArgb(0xFF, 0xA5, 0x00)),
        new("Purple", "#800080", Color.FromArgb(0x80, 0x00, 0x80)),
        new("FC blue", "#00A0FF", Color.FromArgb(0x00, 0xA0, 0xFF)),
        new("Gold", "#FFD700", Color.FromArgb(0xFF, 0xD7, 0x00)),
        new("Dark", "#202020", Color.FromArgb(0x20, 0x20, 0x20)),
        new("Off-white", "#F0F0F0", Color.FromArgb(0xF0, 0xF0, 0xF0)),
        new("Charcoal", "#404040", Color.FromArgb(0x40, 0x40, 0x40)),
        new("Near-black", "#101010", Color.FromArgb(0x10, 0x10, 0x10)),
        new("Dodger", "#1E90FF", Color.FromArgb(0x1E, 0x90, 0xFF)),
        new("Green 2", "#22C55E", Color.FromArgb(0x22, 0xC5, 0x5E)),
        new("Amber", "#F59E0B", Color.FromArgb(0xF5, 0x9E, 0x0B)),
        new("Red 2", "#EF4444", Color.FromArgb(0xEF, 0x44, 0x44)),
        new("Blue 2", "#3B82F6", Color.FromArgb(0x3B, 0x82, 0xF6)),
        new("Violet 2", "#A855F7", Color.FromArgb(0xA8, 0x55, 0xF7))
    };

    private readonly Font _nameFont = new("Segoe UI Semibold", 9f, FontStyle.Regular);
    private readonly Font _smallFont = new("Consolas", 8f, FontStyle.Regular);
    private Point? _lastPoint;

    public ColorGridPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(18, 20, 25);
    }

    public static Point GetCellCenter(int index)
    {
        var column = index % Columns;
        var row = index / Columns;
        return new Point(
            column * CellWidth + CellWidth / 2,
            row * CellHeight + CellHeight / 2);
    }

    public void SetLastPoint(int x, int y)
    {
        var inside = x >= 0 && y >= 0 && x < ClientSize.Width && y < ClientSize.Height;
        _lastPoint = inside ? new Point(x, y) : null;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.None;
        e.Graphics.Clear(BackColor);

        using var borderPen = new Pen(Color.FromArgb(70, 255, 255, 255));

        for (var index = 0; index < Cells.Length; index++)
        {
            var cell = Cells[index];
            var column = index % Columns;
            var row = index / Columns;
            var rect = new Rectangle(
                column * CellWidth,
                row * CellHeight,
                CellWidth,
                CellHeight);

            using (var brush = new SolidBrush(cell.Color))
            {
                e.Graphics.FillRectangle(brush, rect);
            }

            e.Graphics.DrawRectangle(borderPen, rect.Left, rect.Top, rect.Width - 1, rect.Height - 1);

            var textColor = GetTextColor(cell.Color);
            var nameRect = new Rectangle(rect.X + 4, rect.Y + 8, rect.Width - 8, 20);
            var hexRect = new Rectangle(rect.X + 4, rect.Y + 30, rect.Width - 8, 20);
            var center = GetCellCenter(index);
            var centerRect = new Rectangle(rect.X + 4, rect.Bottom - 28, rect.Width - 8, 20);

            TextRenderer.DrawText(
                e.Graphics,
                cell.Name,
                _nameFont,
                nameRect,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(
                e.Graphics,
                cell.Hex,
                _smallFont,
                hexRect,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);

            TextRenderer.DrawText(
                e.Graphics,
                $"center ({center.X},{center.Y})",
                _smallFont,
                centerRect,
                textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
        }

        if (_lastPoint is { } point)
        {
            // Draw an outline around the point, leaving the exact center pixel
            // untouched so a click followed by a pixel probe still reads the
            // original cell color.
            using var markerPen = new Pen(Color.FromArgb(255, 60, 60), 2f);
            e.Graphics.DrawRectangle(markerPen, point.X - 8, point.Y - 8, 16, 16);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _nameFont.Dispose();
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

internal sealed record ColorCell(string Name, string Hex, Color Color);
