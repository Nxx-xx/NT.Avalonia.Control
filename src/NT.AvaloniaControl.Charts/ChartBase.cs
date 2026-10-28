using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace NT.AvaloniaControl.Charts;

/// <summary>Shared control and command contract for chart projections.</summary>
public abstract partial class ChartBase : TemplatedControl
{
    public static readonly StyledProperty<ChartHighContrastMode> HighContrastModeProperty =
        AvaloniaProperty.Register<ChartBase, ChartHighContrastMode>(nameof(HighContrastMode), validate: Enum.IsDefined);

    protected ChartBase() => ResetViewportCommand = new ChartCommand(ResetViewport);

    // Axis and projection settings define data direction. Framework RTL mirroring
    // would reflect our painted glyphs as well; child text controls retain their
    // inherited FlowDirection and perform their own bidirectional text layout.
    protected override bool BypassFlowDirectionPolicies => true;

    /// <summary>Explicit contrast preset supplied by ChartsTheme or application styles. Local brush values and bindings retain precedence.</summary>
    public ChartHighContrastMode HighContrastMode { get => GetValue(HighContrastModeProperty); set => SetValue(HighContrastModeProperty, value); }

    public ICommand ResetViewportCommand { get; }
    public abstract void ResetViewport();

    private sealed class ChartCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }
}
