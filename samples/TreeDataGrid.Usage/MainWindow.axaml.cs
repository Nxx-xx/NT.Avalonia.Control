using Avalonia.Controls;
using NT.AvaloniaControl.TreeDataGrid;

namespace TreeDataGrid.Usage;

public partial class MainWindow : Window
{
    // For compiled display bindings, configure staged writes explicitly on the column:
    // column.SetValueAccessors<Person, string>(person => person.Name, (person, value) => person.Name = value);
    public MainWindow() => InitializeComponent();

    private void OnSelectionChanging(object? sender, TreeDataGridSelectionChangingEventArgs e) => e.Cancel = BlockSelection.IsChecked == true;

    private void OnCellValueChanged(object? sender, TreeDataGridCellValueChangedEventArgs e) =>
        EventStatus.Text = $"{e.Reason}：{e.OldValue} → {e.NewValue}（节点 {e.Cell.Node.Value}，列 {e.Cell.Column.Value}）";
}
