namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid.Mvvm;

internal static class MvvmDemoLauncher
{
    public static MvvmView CreateView() => new(new MvvmDemoViewModel());
}
