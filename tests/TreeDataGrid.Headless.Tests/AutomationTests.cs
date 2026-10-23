using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class AutomationTests
{
    [AvaloniaFact]
    public void AutomationTreeUsesVisibleColumnIndicesAndStableHeaderIdentities()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("row", 1)]);
        var grid = Create(source);
        AutomationProperties.SetName(grid, "订单明细");
        grid.Columns[1].IsVisible = false;
        var window = Show(grid);
        try
        {
            var peer = Peer(grid);
            Assert.Equal(AutomationControlType.DataGrid, peer.GetAutomationControlType());
            Assert.Equal("订单明细", peer.GetName());
            Assert.Equal(1, peer.RowCount);
            Assert.Equal(2, peer.ColumnCount);
            var header = peer.GetColumnHeaderPeer(grid.Columns[2].Id)!;
            var id = header.GetAutomationId();
            Assert.Equal("Detail", header.GetName());
            Assert.Contains("columnIndex=1", header.GetItemStatus());
            Assert.True(grid.TryReorderColumn(grid.Columns[2].Id, 0));
            window.UpdateLayout();
            Assert.Equal(id, header.GetAutomationId());
            Assert.Contains("columnIndex=0", header.GetItemStatus());
            var children = peer.GetChildren()!;
            Assert.Contains(children, child => child.GetAutomationControlType() == AutomationControlType.HeaderItem);
            Assert.Equal(new[] { "Detail", "Name" }, children.Where(child => child.GetAutomationControlType() == AutomationControlType.HeaderItem).Select(child => child.GetName()));
            var row = Assert.Single(children, child => child.GetClassName() == nameof(TreeDataGridRowPresenter));
            Assert.Same(peer, row.GetParent());
            Assert.Equal(2, row.GetChildren()!.Count);
            Assert.All(row.GetChildren()!, cell => Assert.Equal(AutomationControlType.DataItem, cell.GetAutomationControlType()));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LogicalCellPeerBringsVirtualizedIdentityIntoViewWithoutSelectingIt()
    {
        using var source = new FlatTreeDataGridSource<Item>(Enumerable.Range(0, 500).Select(index => new Item($"Item {index}", index)).ToArray());
        var grid = Create(source);
        var window = Show(grid);
        try
        {
            var peer = Peer(grid);
            var address = new CellAddress(source.Rows[450].Id, grid.Columns[0].Id);
            var cell = peer.GetCellPeer(address.Node, address.Column)!;
            var identity = cell.GetAutomationId();
            Assert.True(cell.IsOffscreen());
            Assert.Contains("Item 450", cell.GetName());
            cell.BringIntoView();
            window.UpdateLayout();
            Assert.False(cell.IsOffscreen());
            Assert.Null(grid.State!.CurrentCell);
            Assert.Empty(peer.GetSelection());
            Assert.Contains(grid.RowsPresenter!.RealizedRows.SelectMany(row => row.RealizedCells), realized => realized.Row?.Id == address.Node && realized.Column?.Id == address.Column);
            cell.SetFocus();
            Assert.Equal(address, grid.State.CurrentCell);
            Assert.True(cell.HasKeyboardFocus());
            grid.RowsPresenter.Offset = default;
            window.UpdateLayout();
            Assert.True(cell.IsOffscreen());
            Assert.Equal(identity, cell.GetAutomationId());
            Assert.Equal("Item 450", cell.GetProvider<IValueProvider>()!.Value);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ScrollProviderMakesUnrealizedRowsDiscoverableThroughTheAutomationTree()
    {
        using var source = new FlatTreeDataGridSource<Item>(Enumerable.Range(0, 100).Select(index => new Item($"Item {index}", index)).ToArray());
        var grid = Create(source);
        var window = Show(grid);
        try
        {
            var peer = Peer(grid);
            var scroll = peer.GetProvider<IScrollProvider>()!;
            Assert.NotNull(scroll);
            Assert.True(scroll.VerticallyScrollable);
            scroll.SetScrollPercent(-1, 50);
            Assert.True(grid.RowsPresenter!.Offset.Y > 0);
            Assert.Contains(peer.GetChildren()!, child => child.GetAutomationId() == $"TreeDataGrid.Row.{grid.RowsPresenter.RealizedRows[0].Row!.Id.Value}");
            scroll.SetScrollPercent(-1, 0);
            Assert.Equal(0, grid.RowsPresenter.Offset.Y);
            Assert.Null(peer.GetRowPeer(source.Rows[0].Id)!.GetProvider<ISelectionItemProvider>());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ValueProviderUsesConversionValidationAndStagedEditing()
    {
        var item = new Item("row", 10);
        using var source = new FlatTreeDataGridSource<Item>([item]);
        var grid = Create(source);
        var window = Show(grid);
        try
        {
            var peer = Peer(grid).GetCellPeer(source.Rows[0].Id, grid.Columns[1].Id)!;
            var value = peer.GetProvider<IValueProvider>()!;
            Assert.False(value.IsReadOnly);
            Assert.Throws<InvalidOperationException>(() => value.SetValue("200"));
            Assert.Equal(10, item.Quantity);
            Assert.NotNull(grid.State!.EditSession);
            Assert.Contains("Quantity must be 0–100", peer.GetHelpText());
            value.SetValue("35");
            Assert.Equal(35, item.Quantity);
            Assert.Null(grid.State.EditSession);
            grid.Columns[1].IsReadOnly = true;
            Assert.True(value.IsReadOnly);
            Assert.Throws<InvalidOperationException>(() => value.SetValue("40"));
            Assert.Equal(35, item.Quantity);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RowSelectionAndExpansionProvidersTrackProjectionAndDepth()
    {
        var root = new Item("root", 0) { Children = [new("child", 1)] };
        using var source = new HierarchicalTreeDataGridSource<Item>([root], item => item.Children);
        var grid = Create(source);
        grid.SelectionUnit = TreeDataGridSelectionUnit.Row;
        var window = Show(grid);
        try
        {
            var peer = Peer(grid);
            var row = peer.GetRowPeer(source.Rows[0].Id)!;
            var selection = row.GetProvider<ISelectionItemProvider>()!;
            var expansion = row.GetProvider<IExpandCollapseProvider>()!;
            Assert.Equal(ExpandCollapseState.Collapsed, expansion.ExpandCollapseState);
            selection.Select();
            Assert.True(selection.IsSelected);
            Assert.Single(peer.GetSelection());
            expansion.Expand();
            window.UpdateLayout();
            Assert.Equal(2, peer.RowCount);
            Assert.Equal(ExpandCollapseState.Expanded, expansion.ExpandCollapseState);
            var child = peer.GetRowPeer(source.Rows[1].Id)!;
            Assert.Contains("depth=1", child.GetItemStatus());
            Assert.Equal(ExpandCollapseState.LeafNode, child.GetProvider<IExpandCollapseProvider>()!.ExpandCollapseState);
            child.GetProvider<ISelectionItemProvider>()!.AddToSelection();
            Assert.Equal(2, peer.GetSelection().Count);
            selection.RemoveFromSelection();
            Assert.False(selection.IsSelected);
            expansion.Collapse();
            window.UpdateLayout();
            Assert.Equal(1, peer.RowCount);
            Assert.True(child.GetProvider<ISelectionItemProvider>()!.IsSelected);
            Assert.True(child.IsOffscreen());
            Assert.Throws<InvalidOperationException>(() => child.SetFocus());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void OldLogicalPeerCannotWriteOrSelectAfterSourceReplacement()
    {
        var item = new Item("original", 1);
        using var source = new FlatTreeDataGridSource<Item>([item]);
        using var replacement = new FlatTreeDataGridSource<Item>([new("replacement", 2)]);
        var grid = Create(source);
        var window = Show(grid);
        try
        {
            var cell = Peer(grid).GetCellPeer(source.Rows[0].Id, grid.Columns[0].Id)!;
            var identity = cell.GetAutomationId();
            grid.Source = replacement;
            window.UpdateLayout();
            Assert.True(cell.IsOffscreen());
            Assert.Equal(identity, cell.GetAutomationId());
            Assert.Throws<InvalidOperationException>(() => cell.GetProvider<ISelectionItemProvider>()!.Select());
            Assert.Throws<InvalidOperationException>(() => cell.GetProvider<IValueProvider>()!.SetValue("wrong"));
            Assert.Equal("original", item.Name);
            Assert.Equal("replacement", ((Item)replacement.Rows[0].Model).Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AutomationWriteStopsIfReadCallbackReplacesItsSource()
    {
        var item = new Item("original", 1);
        using var source = new FlatTreeDataGridSource<Item>([item]);
        using var replacement = new FlatTreeDataGridSource<Item>([new("replacement", 2)]);
        var grid = Create(source);
        var window = Show(grid);
        try
        {
            var cell = Peer(grid).GetCellPeer(source.Rows[0].Id, grid.Columns[0].Id)!;
            grid.Columns[0].ValueGetter = model => { grid.Source = replacement; return ((Item)model).Name; };
            Assert.Throws<InvalidOperationException>(() => cell.GetProvider<IValueProvider>()!.SetValue("wrong"));
            Assert.Same(replacement, grid.Source);
            Assert.Equal("original", item.Name);
            Assert.Equal("replacement", ((Item)replacement.Rows[0].Model).Name);
        }
        finally { window.Close(); }
    }

    private static TreeDataGridAutomationPeer Peer(GridControl grid) => Assert.IsType<TreeDataGridAutomationPeer>(ControlAutomationPeer.CreatePeerForElement(grid));
    private static GridControl Create(TreeDataGridSource source)
    {
        var grid = new GridControl { Source = source, CanUserReorderColumns = true, SelectionUnit = TreeDataGridSelectionUnit.Cell, SelectionMode = TreeDataGridSelectionMode.Multiple,
            Template = new FuncControlTemplate<GridControl>((_, scope) =>
            {
                var rows = new TreeDataGridRowsPresenter();
                var headers = new TreeDataGridColumnHeadersPresenter();
                var scroll = new ScrollViewer { Content = rows };
                scope.Register("PART_RowsPresenter", rows);
                scope.Register("PART_ColumnHeadersPresenter", headers);
                scope.Register("PART_ScrollViewer", scroll);
                Grid.SetRow(scroll, 1);
                return new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { headers, scroll } };
            }) };
        grid.Columns.Add(new TreeDataGridTextColumn { Header = "Name", Width = new GridLength(140), ValueType = typeof(string), ValueGetter = model => ((Item)model).Name, ValueSetter = (model, value) => ((Item)model).Name = (string)value! });
        grid.Columns.Add(new TreeDataGridTextColumn { Header = "Quantity", Width = new GridLength(100), ValueType = typeof(int), ValueGetter = model => ((Item)model).Quantity, ValueSetter = (model, value) => ((Item)model).Quantity = (int)value!, ValueValidator = value => value is int quantity && quantity is >= 0 and <= 100 ? null : "Quantity must be 0–100" });
        grid.Columns.Add(new TreeDataGridTextColumn { Header = "Detail", Width = new GridLength(160), ValueGetter = model => ((Item)model).Name });
        return grid;
    }
    private static Window Show(Control content)
    {
        var window = new Window { Width = 480, Height = 280, Content = content };
        window.Show();
        window.UpdateLayout();
        return window;
    }
    private sealed class Item(string name, int quantity)
    {
        public string Name { get; set; } = name;
        public int Quantity { get; set; } = quantity;
        public IReadOnlyList<Item> Children { get; init; } = [];
    }
}
