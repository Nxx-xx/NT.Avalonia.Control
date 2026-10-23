using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using NT.AvaloniaControl.TreeDataGrid.Themes;

namespace TreeDataGrid.Usage;

public sealed class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new TreeDataGridTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow { DataContext = new PeopleViewModel() };
            // 宿主最终关闭时解除绑定，使自动适配源不再订阅模型。
            window.Closed += (_, _) => window.DataContext = null;
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
