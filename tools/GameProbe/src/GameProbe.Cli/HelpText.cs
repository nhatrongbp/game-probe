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
              help                             Hiển thị trợ giúp
              --version                        Hiển thị phiên bản

            VÍ DỤ:
              gameprobe window list
              gameprobe window info --class FIFAKC
              gameprobe profile validate --file profiles/fc-online.json

            LƯU Ý:
              - Chỉ dùng cho nghiên cứu trên account test.
              - Tool chỉ đọc memory, không ghi, không inject.
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
}
