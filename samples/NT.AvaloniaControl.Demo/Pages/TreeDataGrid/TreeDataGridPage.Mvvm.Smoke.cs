using NT.AvaloniaControl.Demo.Pages.TreeDataGrid.Mvvm;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    internal async Task RunMvvmSmokeAsync(string[] args)
    {
        _modeTabs.SelectedIndex = 1;
        _hostWindow.UpdateLayout();
        await MvvmSmoke.RunAsync(_hostWindow, _mvvmView, (MvvmDemoViewModel)_mvvmView.DataContext!, args);
    }
}
