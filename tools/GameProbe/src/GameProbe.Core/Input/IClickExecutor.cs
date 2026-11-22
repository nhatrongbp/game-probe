using GameProbe.Core.Models;
using GameProbe.Core.Windows;

namespace GameProbe.Core.Input;

public interface IClickExecutor
{
    ClickExecutionResult Click(
        GameWindow window,
        ClickPoint point,
        CancellationToken cancellationToken = default);
}
