using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using NT.AvaloniaControl.VirtualKeyboard.Themes;

namespace VirtualKeyboard.Usage;

public sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new VirtualKeyboardTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (Program.Arguments.Any(argument => argument is "--keyboard-smoke" or "--keyboard-benchmark"))
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = new MainWindow { DataContext = new InputViewModel() };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
