using System.Text.Json;
using GameProbe.Core.Windows;

namespace GameProbe.Cli.Commands;

internal static class WindowJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static WindowListItem ToListItem(WindowInfo window) => new(
        window.HandleHex,
        window.ProcessId,
        window.ProcessName,
        window.ClassName,
        window.Title,
        window.ClientWidth,
        window.ClientHeight,
        window.Dpi,
        window.IsVisible,
        window.IsMinimized,
        window.IsHung);

    public static WindowDetail ToDetail(WindowInfo window) => new(
        window.HandleHex,
        window.ProcessId,
        window.ProcessName,
        window.ProcessPath,
        window.ClassName,
        window.Title,
        window.IsVisible,
        window.IsMinimized,
        window.IsHung,
        new WindowRectDto(window.WindowLeft, window.WindowTop, window.WindowWidth, window.WindowHeight),
        window.ClientWidth,
        window.ClientHeight,
        window.Dpi,
        window.DpiScale);
}

internal sealed record WindowListItem(
    string Hwnd,
    uint Pid,
    string Process,
    string ClassName,
    string Title,
    int ClientWidth,
    int ClientHeight,
    uint Dpi,
    bool Visible,
    bool Minimized,
    bool Hung);

internal sealed record WindowRectDto(int Left, int Top, int Width, int Height);

internal sealed record WindowDetail(
    string Hwnd,
    uint Pid,
    string Process,
    string ProcessPath,
    string ClassName,
    string Title,
    bool Visible,
    bool Minimized,
    bool Hung,
    WindowRectDto WindowRect,
    int ClientWidth,
    int ClientHeight,
    uint Dpi,
    double DpiScale);
