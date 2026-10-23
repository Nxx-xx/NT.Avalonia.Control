using System.Windows.Input;
using Avalonia.Controls.Primitives;

namespace NT.AvaloniaControl.Charts;

/// <summary>Shared control and command contract for chart projections.</summary>
public abstract class ChartBase : TemplatedControl
{
    protected ChartBase() => ResetViewportCommand = new ChartCommand(ResetViewport);

    public ICommand ResetViewportCommand { get; }
    public abstract void ResetViewport();

    private sealed class ChartCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }
}
