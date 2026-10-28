using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class AutomationNotificationTests
{
    private sealed class Item(string name) { public string Name { get; set; } = name; }
    private static GridControl Create(TreeDataGridSource source) => A1TestHost.Create(source,
        new TreeDataGridTextColumn { Header = "Name", ValueGetter = m => ((Item)m).Name,
            ValueSetter = (m, v) => ((Item)m).Name = (string)v!, ValueType = typeof(string),
            ValueValidator = v => string.IsNullOrWhiteSpace(v?.ToString()) ? "Required" : null });

    private static void Notify(GridControl grid) => typeof(GridControl).GetMethod("NotifyAutomationStateChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(grid, null);

    private static TreeDataGridAutomationPeer Prime(GridControl grid)
    {
        var peer = Assert.IsType<TreeDataGridAutomationPeer>(ControlAutomationPeer.CreatePeerForElement(grid));
        foreach (var row in grid.RowsPresenter!.RealizedRows)
        {
            ControlAutomationPeer.CreatePeerForElement(row);
            foreach (var cell in row.RealizedCells) ControlAutomationPeer.CreatePeerForElement(cell);
        }
        Notify(grid);
        return peer;
    }

    [AvaloniaFact]
    public void UnchangedStateDoesNotRebroadcastStructureOrProperties()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("one"), new("two")]);
        var grid = Create(source); var window = A1TestHost.Show(grid);
        try
        {
            var peer = Prime(grid); var changes = 0;
            peer.ChildrenChanged += (_, _) => changes++;
            peer.PropertyChanged += (_, _) => changes++;
            foreach (var row in grid.RowsPresenter!.RealizedRows)
            {
                ControlAutomationPeer.FromElement(row)!.PropertyChanged += (_, _) => changes++;
                foreach (var cell in row.RealizedCells) ControlAutomationPeer.FromElement(cell)!.PropertyChanged += (_, _) => changes++;
            }
            for (var i = 0; i < 5; i++) Notify(grid);
            Assert.Equal(0, changes);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SelectionNotifiesChangedItemsWithoutInvalidatingUnchangedChildren()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("one"), new("two")]);
        var grid = Create(source); var window = A1TestHost.Show(grid);
        try
        {
            grid.SelectionUnit = TreeDataGridSelectionUnit.Row;
            var peer = Prime(grid); var structure = 0; var selected = new List<int>();
            peer.ChildrenChanged += (_, _) => structure++;
            for (var i = 0; i < 2; i++)
            {
                var index = i;
                ControlAutomationPeer.FromElement(A1TestHost.Cell(grid, i))!.PropertyChanged += (_, e) =>
                { if (e.Property == SelectionItemPatternIdentifiers.IsSelectedProperty) selected.Add(index); };
            }
            grid.State!.Select(source.Rows[0].Id);
            Assert.Equal(new[] { 0 }, selected);
            selected.Clear(); grid.State.Select(source.Rows[1].Id);
            Assert.Equal(new[] { 0, 1 }, selected.Order());
            Assert.Equal(0, structure);
            grid.ShowColumnHeaders = false;
            Assert.True(structure > 0);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ValidationHelpChangesAreDeliveredAndUnchangedErrorsAreSuppressed()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("one")]);
        var grid = Create(source); var window = A1TestHost.Show(grid);
        try
        {
            Prime(grid); var changes = new List<object?>();
            ControlAutomationPeer.FromElement(A1TestHost.Cell(grid))!.PropertyChanged += (_, e) =>
            { if (e.Property == AutomationElementIdentifiers.HelpTextProperty) changes.Add(e.NewValue); };
            Assert.True(grid.BeginEdit(new(source.Rows[0].Id, grid.Columns[0].Id)));
            grid.EditSession!.Value = ""; Assert.False(grid.CommitEdit());
            Assert.Contains(changes, v => v?.ToString()?.Contains("Required") == true);
            var count = changes.Count; Notify(grid);
            Assert.Equal(count, changes.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PlainTextIsRepresentedByCellAndInteractiveContentKeepsItsTree()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("one")]);
        var grid = Create(source); var window = A1TestHost.Show(grid);
        try
        {
            var cell = A1TestHost.Cell(grid);
            var peer = ControlAutomationPeer.CreatePeerForElement(cell)!;
            Assert.Empty(peer.GetChildren());
            Assert.Contains("one", peer.GetName());
            var structure = 0; peer.ChildrenChanged += (_, _) => structure++;
            var display = cell.Content;
            cell.Content = null; cell.Content = display;
            Assert.Equal(0, structure);
            var button = new Button { Content = "custom action" }; cell.Content = button; window.UpdateLayout();
            Assert.Contains(Descendants(peer), p => ReferenceEquals(p, ControlAutomationPeer.CreatePeerForElement(button)));
            grid.Source = null; window.UpdateLayout();
            Assert.Empty(peer.GetChildren());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LiveEditorKeepsItsAutomationTreeAndCancelRestoresCellText()
    {
        using var source = new FlatTreeDataGridSource<Item>([new("one")]);
        var grid = Create(source); var window = A1TestHost.Show(grid);
        try
        {
            var cell = A1TestHost.Cell(grid);
            var peer = ControlAutomationPeer.CreatePeerForElement(cell)!;
            Assert.Empty(peer.GetChildren());
            Assert.True(grid.BeginEdit(new(source.Rows[0].Id, grid.Columns[0].Id)));
            window.UpdateLayout();
            Assert.Contains(Descendants(peer), p => p.GetAutomationControlType() == AutomationControlType.Edit);
            AssertParentLinks(peer);
            grid.CancelEdit(); window.UpdateLayout();
            Assert.Empty(peer.GetChildren());
            Assert.Contains("one", peer.GetName());
        }
        finally { window.Close(); }
    }

    private static void AssertParentLinks(AutomationPeer peer)
    {
        foreach (var child in peer.GetChildren())
        {
            Assert.Same(peer, child.GetParent());
            AssertParentLinks(child);
        }
    }
    private static IEnumerable<AutomationPeer> Descendants(AutomationPeer peer)
    {
        foreach (var child in peer.GetChildren())
        {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
