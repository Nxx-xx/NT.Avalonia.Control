using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Themes.Tests;

public sealed class P4P5IntegrationTests
{
    [AvaloniaFact]
    public void KeyboardEditingValidatesConvertsCommitsAndCancelsWithVisibleError()
    {
        var item = new Item("alpha", 10);
        using var source = new FlatTreeDataGridSource<Item>([item]);
        var grid = CreateGrid(source);
        var window = Show(grid);
        try
        {
            var address = new CellAddress(source.Rows[0].Id, grid.Columns[1].Id);
            grid.State!.SelectCell(address);
            grid.Focus();
            Key(window, Avalonia.Input.Key.F2);
            var editor = Editor(grid);
            Assert.Contains(":editing", Cell(grid, address).Classes);
            editor.Text = "200";
            Key(window, Avalonia.Input.Key.Enter);
            Assert.Equal(10, item.Quantity);
            Assert.Equal("Quantity must be 0–100.", grid.State.EditSession!.ValidationError);
            var cell = Cell(grid, address);
            Assert.Contains(":invalid", cell.Classes);
            var border = cell.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "PART_StateIndicator");
            Assert.Equal(new Thickness(2), border.BorderThickness);
            editor = Editor(grid);
            editor.Text = "42";
            Key(window, Avalonia.Input.Key.Enter);
            Assert.Equal(42, item.Quantity);
            Assert.Null(grid.State.EditSession);
            grid.Focus();
            Key(window, Avalonia.Input.Key.F2);
            Editor(grid).Text = "9";
            Key(window, Avalonia.Input.Key.Escape);
            Assert.Equal(42, item.Quantity);
            Assert.Null(grid.State.EditSession);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RectangleAndHeaderSortShareIdentityAndShowThemedIndicator()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("alpha", 1), new("beta", 2), new("gamma", 3)]);
        var grid = CreateGrid(source);
        var window = Show(grid);
        try
        {
            grid.State!.SelectCellRange(new CellAddress(source.Rows[0].Id, grid.Columns[0].Id),
                new CellAddress(source.Rows[1].Id, grid.Columns[1].Id), grid.Columns.Select(column => column.Id).ToArray());
            window.UpdateLayout();
            var selected = grid.State.SelectedCells.ToHashSet();
            Assert.Equal(4, selected.Count);
            Assert.Equal(4, grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>().Count(cell => cell.Classes.Contains(":selected")));
            var header = grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Single(control => control.Column == grid.Columns[0]);
            var point = header.TranslatePoint(new Point(40, 12), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            window.UpdateLayout();
            Assert.Equal(TreeDataGridSortDirection.Ascending, source.SortDirection);
            Assert.Equal("▲", header.SortIndicator);
            Assert.Contains(header.GetVisualDescendants().OfType<TextBlock>(), text => text.Name == "PART_SortIndicator" && text.Text == "▲");
            Assert.True(grid.SortByColumn(grid.Columns[0].Id, TreeDataGridSortDirection.Descending));
            window.UpdateLayout();
            Assert.Equal("gamma", ((Item)source.Rows[0].Model).Name);
            Assert.True(selected.SetEquals(grid.State.SelectedCells));
            Assert.All(selected, address => Assert.Contains(":selected", Cell(grid, address).Classes));
            var current = Cell(grid, grid.State.CurrentCell!.Value);
            Assert.Equal(new Thickness(2), current.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "PART_StateIndicator").BorderThickness);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RecycledEditorKeepsDraftThroughSortAndCancelsWhenFilteredOut()
    {
        var items = Enumerable.Range(0, 1000).Select(index => new Item($"item-{index:D4}", 10)).ToArray();
        using var source = new FlatTreeDataGridSource<Item>(items);
        var grid = CreateGrid(source);
        var window = Show(grid);
        try
        {
            var address = new CellAddress(source.Rows[0].Id, grid.Columns[0].Id);
            Assert.True(grid.BeginEdit(address));
            window.UpdateLayout();
            Editor(grid).Text = "draft after recycling";
            var session = grid.State!.EditSession;
            grid.RowsPresenter!.Offset = new Vector(0, 5000);
            window.UpdateLayout();
            Assert.DoesNotContain(grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>(), cell => cell.Row?.Id == address.Node);
            source.Sort(grid.Columns[0].Id, (a, b) => StringComparer.Ordinal.Compare(((Item)a).Name, ((Item)b).Name), TreeDataGridSortDirection.Descending);
            Assert.Same(session, grid.State.EditSession);
            Assert.Equal("draft after recycling", session!.Value);
            grid.RowsPresenter.ScrollIntoView(address.Node);
            window.UpdateLayout();
            Assert.Equal("draft after recycling", Editor(grid).Text);
            Assert.True(grid.CommitEdit());
            window.UpdateLayout();
            Assert.Equal("draft after recycling", items[0].Name);
            Assert.Equal("item-0001", items[1].Name);
            Assert.True(grid.BeginEdit(address));
            window.UpdateLayout();
            Editor(grid).Text = "discarded";
            source.SetFilter(model => !ReferenceEquals(model, items[0]));
            window.UpdateLayout();
            Assert.Null(grid.State.EditSession);
            Assert.Equal("draft after recycling", items[0].Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ThemeAndTemplateReplacementKeepDraftButSourceReplacementDisposesIt()
    {
        var item = new Item("original", 10);
        using var source = new FlatTreeDataGridSource<Item>([item]);
        using var replacement = new FlatTreeDataGridSource<Item>([new("replacement", 20)]);
        var grid = CreateGrid(source);
        var window = Show(grid);
        try
        {
            var address = new CellAddress(source.Rows[0].Id, grid.Columns[0].Id);
            grid.State!.SelectCell(address);
            window.UpdateLayout();
            var lightSelection = Assert.IsAssignableFrom<ISolidColorBrush>(Cell(grid, address).Background).Color;
            window.RequestedThemeVariant = ThemeVariant.Dark;
            window.UpdateLayout();
            var darkSelection = Assert.IsAssignableFrom<ISolidColorBrush>(Cell(grid, address).Background).Color;
            Assert.NotEqual(lightSelection, darkSelection);
            Assert.True(grid.BeginEdit(address));
            window.UpdateLayout();
            Editor(grid).Text = "uncommitted";
            var state = grid.State;
            var session = state.EditSession;
            window.RequestedThemeVariant = ThemeVariant.Dark;
            grid.Template = new FuncControlTemplate<GridControl>((_, scope) =>
            {
                var rows = new TreeDataGridRowsPresenter();
                var scroll = new ScrollViewer { Content = rows };
                scope.Register("PART_RowsPresenter", rows);
                scope.Register("PART_ScrollViewer", scroll);
                return scroll;
            });
            window.UpdateLayout();
            Assert.Same(state, grid.State);
            Assert.Same(session, grid.State.EditSession);
            Assert.Equal("uncommitted", Editor(grid).Text);
            Assert.Contains(":selected", Cell(grid, address).Classes);
            grid.Source = replacement;
            window.UpdateLayout();
            Assert.NotSame(state, grid.State);
            Assert.Null(grid.State!.EditSession);
            Assert.Throws<ObjectDisposedException>(() => state.ClearSelection());
            Assert.Equal("original", item.Name);
            Assert.Empty(grid.GetVisualDescendants().OfType<TextBox>());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NullableBooleanAndCustomEditorUseStagedSessionValues()
    {
        var item = new Item("initial", 10);
        using var source = new FlatTreeDataGridSource<Item>([item]);
        var grid = CreateGrid(source);
        var window = Show(grid);
        try
        {
            var node = source.Rows[0].Id;
            var displayCell = Cell(grid, new CellAddress(node, grid.Columns[2].Id));
            var displayCheck = displayCell.GetVisualDescendants().OfType<CheckBox>().Single();
            Assert.True(displayCheck.Bounds.Height <= displayCell.Bounds.Height - displayCell.Padding.Top - displayCell.Padding.Bottom);
            var indicator = displayCheck.GetVisualDescendants().OfType<Border>().Single(control => control.Name == "PART_CheckIndicator");
            var indicatorTop = indicator.TranslatePoint(default, displayCheck)!.Value.Y;
            Assert.True(indicatorTop >= 0 && indicatorTop + indicator.Bounds.Height <= displayCheck.Bounds.Height);
            Assert.True(grid.BeginEdit(new CellAddress(node, grid.Columns[2].Id)));
            window.UpdateLayout();
            var check = Cell(grid, new CellAddress(node, grid.Columns[2].Id)).GetVisualDescendants().OfType<CheckBox>().Single();
            Assert.Null(check.IsChecked);
            check.IsChecked = true;
            Assert.Null(item.Enabled);
            Assert.True(grid.CommitEdit());
            Assert.True(item.Enabled);
            Assert.True(grid.BeginEdit(new CellAddress(node, grid.Columns[2].Id)));
            window.UpdateLayout();
            Cell(grid, new CellAddress(node, grid.Columns[2].Id)).GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = null;
            Assert.True(grid.CommitEdit());
            Assert.Null(item.Enabled);
            Assert.True(grid.BeginEdit(new CellAddress(node, grid.Columns[3].Id)));
            window.UpdateLayout();
            var custom = grid.GetVisualDescendants().OfType<TextBox>().Single(text => text.Name == "CustomEditor");
            custom.Text = "custom draft";
            Assert.Equal("initial", item.Name);
            Assert.Equal("custom draft", grid.State!.EditSession!.Value);
            Assert.True(grid.CommitEdit());
            Assert.Equal("custom draft", item.Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void InvalidEditorPreventsHeaderSortAndValidCommitCanHideItsOwnRow()
    {
        var first = new Item("beta", 20);
        var second = new Item("alpha", 30);
        using var source = new FlatTreeDataGridSource<Item>([first, second]);
        var grid = CreateGrid(source);
        var window = Show(grid);
        try
        {
            source.SetFilter(model => ((Item)model).Quantity >= 10);
            var address = new CellAddress(source.Rows[0].Id, grid.Columns[1].Id);
            Assert.True(grid.BeginEdit(address));
            window.UpdateLayout();
            Editor(grid).Text = "200";
            Assert.False(grid.SortByColumn(grid.Columns[0].Id, TreeDataGridSortDirection.Ascending));
            Assert.Equal(TreeDataGridSortDirection.None, source.SortDirection);
            Assert.NotNull(grid.State!.EditSession!.ValidationError);
            Assert.Same(first, source.Rows[0].Model);
            Editor(grid).Text = "5";
            Assert.True(grid.CommitEdit());
            window.UpdateLayout();
            Assert.Equal(5, first.Quantity);
            Assert.Equal(30, second.Quantity);
            Assert.Single(source.Rows);
            Assert.Same(second, source.Rows[0].Model);
            Assert.Null(grid.State.EditSession);
            Assert.DoesNotContain(grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>(), cell => cell.Classes.Contains(":editing"));
        }
        finally { window.Close(); }
    }

    private static GridControl CreateGrid(TreeDataGridSource source)
    {
        var grid = new GridControl { Source = source, SelectionUnit = TreeDataGridSelectionUnit.Cell, SelectionMode = TreeDataGridSelectionMode.Multiple };
        grid.Columns.Add(new TreeDataGridTextColumn { Header = "Name", Width = new GridLength(180), ValueType = typeof(string), ValueGetter = model => ((Item)model).Name, ValueSetter = (model, value) => ((Item)model).Name = (string)value! });
        grid.Columns.Add(new TreeDataGridTextColumn
        {
            Header = "Quantity", Width = new GridLength(140), ValueType = typeof(int), ValueGetter = model => ((Item)model).Quantity,
            ValueSetter = (model, value) => ((Item)model).Quantity = (int)value!,
            ValueValidator = value => value is int number && number is >= 0 and <= 100 ? null : "Quantity must be 0–100.",
        });
        grid.Columns.Add(new TreeDataGridCheckBoxColumn { Header = "Nullable", Width = new GridLength(110), IsThreeState = true, ValueType = typeof(bool?), ValueGetter = model => ((Item)model).Enabled, ValueSetter = (model, value) => ((Item)model).Enabled = (bool?)value });
        grid.Columns.Add(new TreeDataGridTemplateColumn
        {
            Header = "Template", Width = new GridLength(200), ValueType = typeof(string), ValueGetter = model => ((Item)model).Name,
            ValueSetter = (model, value) => ((Item)model).Name = (string)value!,
            CellTemplate = new FuncDataTemplate<string>((value, _) => new TextBlock { Text = value }),
            CellEditingTemplate = new FuncDataTemplate<TreeDataGridEditSession>((session, _) =>
            {
                var editor = new TextBox { Name = "CustomEditor", DataContext = session };
                editor.Bind(TextBox.TextProperty, new ReflectionBinding(nameof(TreeDataGridEditSession.Value)) { Mode = BindingMode.TwoWay });
                return editor;
            }),
        });
        return grid;
    }
    private static TextBox Editor(GridControl grid) => grid.GetVisualDescendants().OfType<TextBox>().Single(text => text.Name == "PART_Editor");
    private static TreeDataGridCellPresenter Cell(GridControl grid, CellAddress address) => grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>().Single(cell => cell.Row?.Id == address.Node && cell.Column?.Id == address.Column);
    private static Window Show(Control content) { var window = new Window { Width = 760, Height = 380, Content = content }; window.Show(); window.UpdateLayout(); return window; }
    private static void Key(Window window, Key key)
    {
        var physicalKey = key == Avalonia.Input.Key.Enter ? PhysicalKey.Enter : Enum.Parse<PhysicalKey>(key.ToString());
        window.KeyPress(key, RawInputModifiers.None, physicalKey, null);
        window.KeyRelease(key, RawInputModifiers.None, physicalKey, null);
        window.UpdateLayout();
    }

    public sealed class Item(string name, int quantity) : INotifyPropertyChanged
    {
        private string _name = name;
        private int _quantity = quantity;
        private bool? _enabled;
        public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); } }
        public int Quantity { get => _quantity; set { _quantity = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Quantity))); } }
        public bool? Enabled { get => _enabled; set { _enabled = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
