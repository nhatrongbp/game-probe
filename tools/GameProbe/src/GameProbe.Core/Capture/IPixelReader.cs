using GameProbe.Core.Models;
using GameProbe.Core.Windows;

namespace GameProbe.Core.Capture;

public interface IPixelReader
{
    PixelReadResult Read(GameWindow window, PixelProbeSpec probe);
}
