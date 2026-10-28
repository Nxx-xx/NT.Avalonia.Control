using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Themes.Tests;

public sealed class ElementFactoryThemeTests
{
    [AvaloniaFact]
    public void Factory_cells_keep_default_theme_selection_editing_and_identity_automation_across_factory_and_variant_changes()
    {
        var model = new Model(); using var source = new FlatTreeDataGridSource<Model>([model]);
        var first = new Factory(); var next = new Factory(); var grid = new GridControl { Source = source, ElementFactory = first };
        var column = new TreeDataGridTextColumn { Binding = TreeDataGridBinding.Create<Model, string>(row => row.Name), Width = new GridLength(220) }; grid.Columns.Add(column);
        var window = new Window { Content = grid, Width = 420, Height = 260 }; window.Show(); window.UpdateLayout();
        try
        {
            var address = new CellAddress(source.Rows[0].Id, column.Id); Assert.True(grid.BeginEdit(address));
            var edit = grid.EditSession!; edit.Value = "retained";
            window.RequestedThemeVariant = ThemeVariant.Dark; grid.ShowColumnHeaders = false; window.UpdateLayout();
            grid.ElementFactory = next; window.RequestedThemeVariant = ThemeVariant.Light; grid.ShowColumnHeaders = true; window.UpdateLayout();
            Assert.Same(edit, grid.EditSession); Assert.True(grid.CommitEdit()); Assert.Equal("retained", model.Name);
            Assert.Equal(first.Created, first.Released); Assert.Equal(0, first.Active);
            var peer = (TreeDataGridAutomationPeer)ControlAutomationPeer.CreatePeerForElement(grid)!;
            var provider = peer.GetCellPeer(address.Node, address.Column)!.GetProvider<IValueProvider>()!;
            provider.SetValue("automation"); Assert.Equal("automation", model.Name);
            Assert.All(grid.RowsPresenter!.RealizedRows.SelectMany(row => row.RealizedCells), cell => Assert.Contains("factory-cell", cell.Classes));
        }
        finally { window.Close(); }
        Assert.Equal(next.Created, next.Released); Assert.Equal(0, next.Active);
    }

    [AvaloniaFact]
    public void Factory_can_supply_a_content_template_without_replacing_column_content_or_header_sort_semantics()
    {
        using var source = new FlatTreeDataGridSource<Model>([new() { Name = "b" }, new() { Name = "a" }]);
        var factory = new TemplateFactory(); var grid = new GridControl { Source = source, ElementFactory = factory };
        var column = new TreeDataGridTextColumn { Binding = new ReflectionBinding(nameof(Model.Name)), Header = "Name", Width = new GridLength(220) }; grid.Columns.Add(column);
        var window = new Window { Content = grid, Width = 420, Height = 260 }; window.Show(); window.UpdateLayout();
        try
        {
            Assert.Equal(2, grid.GetVisualDescendants().OfType<Border>().Count(border => border.Name == "FactoryCellBorder"));
            var peer = (TreeDataGridAutomationPeer)ControlAutomationPeer.CreatePeerForElement(grid)!;
            peer.GetColumnHeaderPeer(column.Id)!.GetProvider<IInvokeProvider>()!.Invoke(); window.UpdateLayout();
            Assert.Equal("a", ((Model)source.Rows[0].Model).Name); Assert.Equal(TreeDataGridSortDirection.Ascending, source.SortDirection);
            grid.ElementFactory = null; window.UpdateLayout(); Assert.DoesNotContain(grid.GetVisualDescendants().OfType<Border>(), border => border.Name == "FactoryCellBorder");
        }
        finally { window.Close(); }
    }

    private sealed class Model { public string Name { get; set; } = "initial"; }
    private sealed class Factory : TreeDataGridElementFactory
    {
        public int Created, Released, Active;
        public override Control CreateElement(GridControl owner, TreeDataGridElementKind kind) { Created++; return base.CreateElement(owner, kind); }
        public override void PrepareElement(TreeDataGridElementContext context) { Active++; if (context.Kind == TreeDataGridElementKind.Cell) context.Element.Classes.Add("factory-cell"); }
        public override void ClearElement(TreeDataGridElementContext context) => Active--;
        public override void ReleaseElement(GridControl owner, TreeDataGridElementKind kind, Control element) => Released++;
    }
    private sealed class TemplateFactory : TreeDataGridElementFactory
    {
        public override Control CreateElement(GridControl owner, TreeDataGridElementKind kind)
        {
            var element = base.CreateElement(owner, kind);
            if (element is TreeDataGridCellPresenter cell)
                cell.Template = new FuncControlTemplate<TreeDataGridCellPresenter>((_, _) => new Border
                {
                    Name = "FactoryCellBorder", Padding = new Thickness(5),
                    Child = new ContentPresenter { [!ContentPresenter.ContentProperty] = new TemplateBinding(ContentControl.ContentProperty),
                        [!ContentPresenter.BackgroundProperty] = new TemplateBinding(ContentControl.BackgroundProperty) },
                });
            return element;
        }
    }
}
