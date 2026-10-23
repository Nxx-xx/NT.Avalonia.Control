using Avalonia.Controls;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid.Mvvm;

public partial class MvvmView : UserControl, IDisposable
{
    private readonly MvvmDemoViewModel? _ownedViewModel;
    private bool _disposed;

    public MvvmView() => InitializeComponent();

    internal MvvmView(MvvmDemoViewModel viewModel) : this()
    {
        _ownedViewModel = viewModel;
        DataContext = viewModel;
    }

    // A tab can detach this view temporarily. Only its host's final close releases the model.
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DataContext = null;
        _ownedViewModel?.Dispose();
    }
}
