using System.ComponentModel;
using System.Globalization;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Themes.Tests;

public sealed class BindingWritebackThemeTests
{
    [AvaloniaFact]
    public void Default_theme_preserves_converter_draft_through_variants_and_automation_uses_the_same_staged_writer()
    {
        var row = new Model(); using var source = new FlatTreeDataGridSource<Model>([row]);
        var binding = TreeDataGridBinding.Create<Model, int>(r => r.Number); binding.Converter = new Converter();
        var column = new TreeDataGridTextColumn { Binding = binding, Header = "Count" };
        var grid = new GridControl { Source = source }; grid.Columns.Add(column);
        var window = new Window { Content = grid, Width = 420, Height = 280 }; window.Show(); window.UpdateLayout();
        try
        {
            var address = new CellAddress(source.Rows[0].Id, column.Id); Assert.True(grid.BeginEdit(address));
            var edit = grid.EditSession!; edit.Value = "count 42";
            window.RequestedThemeVariant = ThemeVariant.Dark; window.UpdateLayout();
            grid.ShowColumnHeaders = false; window.UpdateLayout(); Assert.Same(edit, grid.EditSession); Assert.Equal(0, row.Writes);
            window.RequestedThemeVariant = ThemeVariant.Light; grid.ShowColumnHeaders = true; window.UpdateLayout();
            Assert.True(grid.CommitEdit()); Assert.Equal(42, row.Number); Assert.Equal(1, row.Writes);
            var peer = (TreeDataGridAutomationPeer)ControlAutomationPeer.CreatePeerForElement(grid)!;
            var provider = peer.GetCellPeer(address.Node, address.Column)!.GetProvider<IValueProvider>()!;
            provider.SetValue("count 77"); Assert.Equal(77, row.Number); Assert.Equal(2, row.Writes); Assert.Null(grid.EditSession);
        }
        finally { window.Close(); }
    }
    private sealed class Model : INotifyPropertyChanged
    {
        private int _number = 12;
        public int Number { get => _number; set { _number = value; Writes++; PropertyChanged?.Invoke(this, new(nameof(Number))); } }
        public int Writes { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    private sealed class Converter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => "count " + value;
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => int.Parse(value!.ToString()![6..], culture);
    }
}
