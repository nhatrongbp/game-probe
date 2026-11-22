using System.ComponentModel;

namespace GameProbe.Core.Diagnostics;

public static class Win32ErrorText
{
    public static string Describe(int error)
    {
        return error switch
        {
            0 => "không có mã lỗi Win32",
            5 => "truy cập bị từ chối (cần chạy Administrator hoặc game/anti-cheat đang chặn)",
            6 => "handle không hợp lệ",
            87 => "tham số không hợp lệ (process có thể đã thoát)",
            1400 => "HWND không hợp lệ",
            1460 => "hết thời gian chờ xử lý message",
            _ => TryDescribeSystemError(error)
        };
    }

    public static string Format(int error) =>
        error == 0 ? Describe(0) : $"{Describe(error)} (Win32 {error})";

    private static string TryDescribeSystemError(int error)
    {
        try
        {
            return new Win32Exception(error).Message;
        }
        catch (Exception)
        {
            return "lỗi Win32 không xác định";
        }
    }
}
