using System.Diagnostics;

namespace GameProbe.TestTarget;

public sealed class TestTargetForm : Form
{
    private const int WmMouseMove = 0x0200;
    private const int WmLeftButtonDown = 0x0201;
    private const int WmLeftButtonUp = 0x0202;
    private const int WmRightButtonDown = 0x0204;
    private const int WmRightButtonUp = 0x0205;
    private const int WmMiddleButtonDown = 0x0207;
    private const int WmMiddleButtonUp = 0x0208;

    private const int MaxLogEntries = 300;

    private readonly ColorGridPanel _grid;
    private readonly ToggleColorPanel _toggle;
    private readonly ListBox _log;
    private readonly StreamWriter? _mouseLog;
    private int _lastMoveX = int.MinValue;
    private int _lastMoveY = int.MinValue;

    public TestTargetForm()
    {
        Text = "GameProbe TestTarget — P2 fixture";
        ClientSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(18, 20, 25);

        _mouseLog = TryOpenMouseLog();

        _grid = new ColorGridPanel
        {
            Location = new Point(0, 0),
            Size = new Size(
                ColorGridPanel.Columns * ColorGridPanel.CellWidth,
                ColorGridPanel.Rows * ColorGridPanel.CellHeight)
        };

        _grid.MouseDown += (_, e) =>
        {
            _grid.SetLastPoint(e.X, e.Y);
            LogMouse($"MouseDown({e.Button})", e.X, e.Y, wParam: 0);
        };

        _toggle = new ToggleColorPanel
        {
            Location = new Point(0, 480),
            Size = new Size(120, 120)
        };

        _toggle.Toggled += (_, _) =>
            AppendLog($"{DateTime.Now:HH:mm:ss.fff} {"TOGGLE",-16} color={_toggle.CurrentHex}");

        _log = new ListBox
        {
            Location = new Point(600, 0),
            Size = new Size(300, 600),
            IntegralHeight = false,
            HorizontalScrollbar = true,
            BackColor = Color.FromArgb(12, 14, 18),
            ForeColor = Color.FromArgb(190, 200, 215),
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 8.25f)
        };

        var hint = new Label
        {
            Location = new Point(120, 480),
            Size = new Size(480, 120),
            BackColor = Color.FromArgb(24, 27, 33),
            ForeColor = Color.FromArgb(180, 190, 205),
            Font = new Font("Segoe UI", 8.75f),
            Padding = new Padding(10, 6, 10, 6),
            Text =
                "GameProbe P2 fixture\r\n" +
                "• Lưới 6x4, ô 100x120; tâm ô = (cột*100 + 50, hàng*120 + 60).\r\n" +
                "• Ô TOGGLE (60,540) đổi màu mỗi click — dùng test pixel watch.\r\n" +
                "• Mouse message gửi tới HWND form được log bên phải và trong file\r\n" +
                "  %LocalAppData%\\GameProbe\\logs\\testtarget-mouse.log."
        };

        Controls.Add(_grid);
        Controls.Add(_toggle);
        Controls.Add(_log);
        Controls.Add(hint);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg is WmMouseMove or WmLeftButtonDown or WmLeftButtonUp
            or WmRightButtonDown or WmRightButtonUp
            or WmMiddleButtonDown or WmMiddleButtonUp)
        {
            var lParam = m.LParam.ToInt64();
            var x = (int)(lParam & 0xFFFF);
            var y = (int)((lParam >> 16) & 0xFFFF);

            if (m.Msg != WmMouseMove || x != _lastMoveX || y != _lastMoveY)
            {
                if (m.Msg == WmMouseMove)
                {
                    _lastMoveX = x;
                    _lastMoveY = y;
                }

                LogMouse(GetMessageName(m.Msg), x, y, m.WParam.ToInt64());
                _grid.SetLastPoint(x, y);

                if (m.Msg == WmLeftButtonDown && _toggle.Bounds.Contains(x, y))
                {
                    _toggle.Toggle();
                }
            }
        }

        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _mouseLog?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void LogMouse(string messageName, int x, int y, long wParam)
    {
        AppendLog(
            $"{DateTime.Now:HH:mm:ss.fff} {messageName,-16} " +
            $"x={x,4} y={y,4} wParam=0x{wParam:X}");
    }

    private void AppendLog(string line)
    {
        _log.BeginUpdate();
        try
        {
            _log.Items.Add(line);
            while (_log.Items.Count > MaxLogEntries)
            {
                _log.Items.RemoveAt(0);
            }

            _log.TopIndex = Math.Max(0, _log.Items.Count - 1);
        }
        finally
        {
            _log.EndUpdate();
        }

        try
        {
            _mouseLog?.WriteLine(line);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"TestTarget mouse log write failed: {ex.Message}");
        }
    }

    private static StreamWriter? TryOpenMouseLog()
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GameProbe",
                "logs");

            Directory.CreateDirectory(directory);

            var writer = new StreamWriter(
                Path.Combine(directory, "testtarget-mouse.log"),
                append: true)
            {
                AutoFlush = true
            };

            writer.WriteLine(
                $"--- TestTarget session {DateTimeOffset.Now:O} " +
                $"pid={Environment.ProcessId} client=900x600 ---");
            return writer;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"TestTarget mouse log unavailable: {ex.Message}");
            return null;
        }
    }

    private static string GetMessageName(int message)
    {
        return message switch
        {
            WmMouseMove => "WM_MOUSEMOVE",
            WmLeftButtonDown => "WM_LBUTTONDOWN",
            WmLeftButtonUp => "WM_LBUTTONUP",
            WmRightButtonDown => "WM_RBUTTONDOWN",
            WmRightButtonUp => "WM_RBUTTONUP",
            WmMiddleButtonDown => "WM_MBUTTONDOWN",
            WmMiddleButtonUp => "WM_MBUTTONUP",
            _ => $"0x{message:X4}"
        };
    }
}
