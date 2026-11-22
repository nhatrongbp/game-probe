namespace GameProbe.Cli;

internal static class HelpText
{
    public static void PrintGeneral()
    {
        Console.WriteLine(
            """
            GameProbe — công cụ nghiên cứu automation cửa sổ game.

            CÁCH DÙNG:
              gameprobe <command> [options]

            COMMAND:
              window list [--all] [--json]     Liệt kê cửa sổ top-level
              window info --hwnd <hex>         Thông tin một cửa sổ
              window info --class <name>       Tìm và hiển thị cửa sổ đầu tiên
              profile validate --file <path>   Kiểm tra profile JSON
              click --profile <path> --point <id> [--dry-run] [--json]
              capture [target options] [--timeout <sec>] [--json]
              pixel --profile <path> --probe <id> [--json]
              pixel --x <x> --y <y> [target options] [--json]
              pixel watch ...                 Theo dõi màu pixel
              help                             Hiển thị trợ giúp
              --version                        Hiển thị phiên bản

            TARGET OPTIONS (dùng cho click/pixel/capture):
              --profile <path>     Dùng target trong profile JSON
              --hwnd <hex>         Chỉ định HWND
              --pid <pid>          Lọc theo process id
              --class <name>       Lọc theo window class
              --process <name>     Lọc theo process name
              --title <text>       Lọc theo chuỗi con trong title
              --include-hidden     Cho phép khớp cửa sổ hidden

            VÍ DỤ:
              gameprobe window list
              gameprobe profile validate --file profiles/fc-online.json
              gameprobe click --profile profiles/fc-online.json --point play --dry-run
              gameprobe pixel --profile profiles/fc-online.json --probe play-color
              gameprobe capture --class FIFAKC --timeout 30 --json

            LƯU Ý:
              - Chỉ dùng cho nghiên cứu trên account test.
              - Tool chỉ đọc memory, không ghi, không inject.
              - Click mặc định dùng PostMessage, không di chuyển con trỏ thật.
              - Click vào game nên chạy PowerShell Administrator (UIPI có thể trả Win32 5).
              - Tuân thủ ToS của game; không dùng để gian lận.
            """);
    }

    public static void PrintProfileValidate()
    {
        Console.WriteLine(
            """
            CÁCH DÙNG:
              gameprobe profile validate --file <path>
              gameprobe profile validate <path>

            OPTIONS:
              -f, --file <path>   Đường dẫn tới profile JSON
              -h, --help          Hiển thị trợ giúp
            """);
    }

    public static void PrintWindowCommands()
    {
        Console.WriteLine(
            """
            CÁCH DÙNG:
              gameprobe window list [--all] [--json]
              gameprobe window info --hwnd <hex|decimal> [--json]
              gameprobe window info [--pid <pid>] [--class <name>] [--process <name>] [--title <text>] [--include-hidden] [--json]

            COMMAND:
              list   Liệt kê cửa sổ top-level (mặc định: visible, client >= 16x16)
              info   Xem thông tin chi tiết một cửa sổ hoặc tìm theo tiêu chí

            OPTIONS:
              -a, --all            list: gồm cả cửa sổ hidden/kích thước nhỏ
              --include-hidden     info: cho phép khớp cửa sổ hidden
              --json               Xuất JSON
              -h, --help           Hiển thị trợ giúp
            """);
    }

    public static void PrintWindowList()
    {
        Console.WriteLine(
            """
            CÁCH DÙNG:
              gameprobe window list [--all] [--json]

            OPTIONS:
              -a, --all   Gồm cả cửa sổ hidden/kích thước nhỏ
              --json      Xuất JSON
              -h, --help  Hiển thị trợ giúp
            """);
    }

    public static void PrintWindowInfo()
    {
        Console.WriteLine(
            """
            CÁCH DÙNG:
              gameprobe window info --hwnd <hex|decimal> [--json]
              gameprobe window info --class <name> [--process <name>] [--title <text>] [--pid <pid>] [--include-hidden] [--json]

            OPTIONS:
              --hwnd <value>       HWND dạng 0x... hoặc số thập phân
              --pid <pid>          Lọc theo process id
              --class <name>       Lọc theo window class
              --process <name>     Lọc theo tên process (có hoặc không có .exe)
              --title <text>       Lọc theo chuỗi con trong title
              --include-hidden     Cho phép khớp cửa sổ hidden
              --json               Xuất JSON
              -h, --help           Hiển thị trợ giúp
            """);
    }

    public static void PrintClick()
    {
        Console.WriteLine(
            """
            CÁCH DÙNG:
              gameprobe click --profile <path> --point <id> [options]

            OPTIONS:
              --profile <path>   Profile JSON (bắt buộc)
              --point, -p <id>   ID của click point (bắt buộc)
              --hwnd <hex>       Ghi đè target bằng HWND cụ thể
              --pid <pid>        Ghi đè target theo PID
              --class <name>     Ghi đè target theo window class
              --process <name>   Ghi đè target theo process name
              --title <text>     Ghi đè target theo title
              --include-hidden   Cho phép khớp cửa sổ hidden
              --dry-run          Chỉ resolve tọa độ, không gửi message
              --no-hover         Không gửi WM_MOUSEMOVE trước khi click
              --json             Xuất JSON
              -h, --help         Hiển thị trợ giúp

            GHI CHÚ:
              - Chỉ hỗ trợ syncMode post/send trong P2; sendInput bị từ chối.
              - Mỗi sequence cách nhau tối thiểu 20 ms; count/interval lấy từ profile.
              - Click vào game cần PowerShell Administrator; process thường bị UIPI chặn (Win32 5).
            """);
    }

    public static void PrintCapture()
    {
        Console.WriteLine(
            """
            CÁCH DÙNG:
              gameprobe capture [target options] [--timeout <sec>] [--json]

            MÔ TẢ:
              Chờ nhấn F8, đọc vị trí con trỏ và quy đổi thành client coordinates.
              Không gửi input, không di chuyển con trỏ.

            TARGET OPTIONS:
              --profile <path>   Dùng target trong profile JSON
              --hwnd <hex>       Chỉ định HWND
              --pid <pid>        Lọc theo PID
              --class <name>     Lọc theo window class
              --process <name>   Lọc theo process name
              --title <text>     Lọc theo title
              --include-hidden   Cho phép khớp cửa sổ hidden

            OPTIONS:
              --timeout <sec>    Thời gian chờ F8, 1..600 giây (mặc định 30)
              --json             Xuất JSON kèm snippet tọa độ
              -h, --help         Hiển thị trợ giúp
            """);
    }

    public static void PrintPixel()
    {
        Console.WriteLine(
            """
            CÁCH DÙNG:
              gameprobe pixel --profile <path> --probe <id> [options]
              gameprobe pixel --x <x> --y <y> [target options] [options]

            OPTIONS:
              --profile <path>     Profile JSON (bắt buộc khi dùng --probe)
              --probe <id>         ID của pixel probe
              --x <x>              Client X (khi không dùng probe)
              --y <y>              Client Y (khi không dùng probe)
              --tolerance <0-128>  Tolerance phân loại màu (mặc định 8 hoặc theo probe)
              --expect-class <0-3> Kỳ vọng class màu: 0 trắng/xám, 1 xanh, 2 vàng, 3 khác/đen
              --expect-rgb <hex>   Kỳ vọng màu #RRGGBB
              --json               Xuất JSON
              -h, --help           Hiển thị trợ giúp

            TARGET OPTIONS:
              --hwnd <hex>, --pid <pid>, --class <name>, --process <name>, --title <text>,
              --include-hidden
            """);
    }

    public static void PrintPixelWatch()
    {
        Console.WriteLine(
            """
            CÁCH DÙNG:
              gameprobe pixel watch --profile <path> --probe <id> [options]
              gameprobe pixel watch --x <x> --y <y> [target options] [options]

            MÔ TẢ:
              Poll màu pixel theo interval, chỉ in khi RGB hoặc class thay đổi.
              Ctrl+C để dừng.

            OPTIONS:
              --profile <path>     Profile JSON (bắt buộc khi dùng --probe)
              --probe <id>         ID của pixel probe
              --x <x>              Client X (khi không dùng probe)
              --y <y>              Client Y (khi không dùng probe)
              --tolerance <0-128>  Tolerance phân loại màu
              --expect-class <0-3> Kỳ vọng class màu
              --expect-rgb <hex>   Kỳ vọng màu #RRGGBB
              --interval <ms>      Interval poll, tối thiểu 50 ms (mặc định 200 hoặc theo probe)
              --duration <sec>     Tự dừng sau N giây (mặc định chạy đến Ctrl+C)
              --json               Xuất JSON Lines (mỗi thay đổi một dòng)
              -h, --help           Hiển thị trợ giúp

            TARGET OPTIONS:
              --hwnd <hex>, --pid <pid>, --class <name>, --process <name>, --title <text>,
              --include-hidden
            """);
    }
}
