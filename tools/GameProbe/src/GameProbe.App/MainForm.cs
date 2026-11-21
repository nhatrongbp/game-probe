using GameProbe.Core.Diagnostics;

namespace GameProbe.App;

public sealed class MainForm : Form
{
    private readonly IActionLogger _logger;

    public MainForm(IActionLogger logger)
    {
        _logger = logger;
        InitializeLayout();
    }

    private void InitializeLayout()
    {
        Text = "GameProbe — P1 skeleton";
        ClientSize = new Size(980, 640);
        MinimumSize = new Size(760, 480);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(30, 34, 42);
        ForeColor = Color.White;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        root.Controls.Add(CreateHeader(), 0, 0);
        root.Controls.Add(CreateTabs(), 0, 1);
        root.Controls.Add(CreateStatusBar(), 0, 2);

        Controls.Add(root);
    }

    private Control CreateHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 23, 29),
            Padding = new Padding(20, 12, 20, 10)
        };

        var title = new Label
        {
            Text = "GameProbe",
            Font = new Font("Segoe UI Semibold", 20f, FontStyle.Regular),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(20, 12)
        };

        var subtitle = new Label
        {
            Text = "P1 — WindowLocator/CLI đã có. P2 thêm click/pixel; P4 hoàn thiện GUI target bar.",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(166, 176, 191),
            AutoSize = true,
            Location = new Point(22, 52)
        };

        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        return header;
    }

    private static TabControl CreateTabs()
    {
        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(14, 6)
        };

        tabs.TabPages.Add(CreatePlaceholderTab(
            "Click",
            "P2 — cấu hình point, click bằng PostMessage, capture tọa độ bằng F8."));

        tabs.TabPages.Add(CreatePlaceholderTab(
            "Pixel",
            "P2 — đọc màu 1 pixel bằng BitBlt, phân loại màu và watch."));

        tabs.TabPages.Add(CreatePlaceholderTab(
            "Text",
            "P3 — đọc window title, scan chuỗi trong RAM, snapshot và diff."));

        tabs.TabPages.Add(CreatePlaceholderTab(
            "Log",
            "P4 — xem log hành động và lỗi Win32."));

        return tabs;
    }

    private static TabPage CreatePlaceholderTab(string title, string description)
    {
        var page = new TabPage(title)
        {
            BackColor = Color.FromArgb(30, 34, 42),
            ForeColor = Color.White,
            Padding = new Padding(24)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 1
        };

        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = description,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 11f, FontStyle.Regular),
            ForeColor = Color.FromArgb(180, 188, 200)
        };

        layout.Controls.Add(label, 0, 0);
        page.Controls.Add(layout);
        return page;
    }

    private static Control CreateStatusBar()
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            Text = "P1 — core/CLI window info đã có. Chạy bằng Administrator. Chỉ dùng cho nghiên cứu/account test.",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 12, 0),
            BackColor = Color.FromArgb(20, 23, 29),
            ForeColor = Color.FromArgb(150, 160, 175),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
        };
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _logger.Info("ui.ready", "Main window shown");
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _logger.Info("ui.closed", "Main window closed");
        base.OnFormClosed(e);
    }
}
