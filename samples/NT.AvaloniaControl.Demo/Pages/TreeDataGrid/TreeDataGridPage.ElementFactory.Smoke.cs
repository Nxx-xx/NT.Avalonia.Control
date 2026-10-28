using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private async Task RunElementFactoryDemoSmokeAsync(Dictionary<string, bool> checks, List<object> snapshots, string? imagePath, List<string> images)
    {
        await DemoSelectScenarioAsync("element-factory");
        var a = _elementFactoryA!; var b = _elementFactoryB!; var source = _source!;
        checks["a5FactoryCreatedThreeRoles"] = a.Kinds.Count == 3 && a.Active > 0 && a.Subscriptions > 0;
        DemoPressButton(DemoFind<Button>(this, "BeginFactoryEditButton")); UpdateLayout();
        var edit = _grid.EditSession!; var item = (EditingItem)source.Rows[0].Model; var originalName = item.Name; var address = edit.Cell;
        edit.Value = "工厂切换后提交";
        checks["a5DraftStagedBeforeFactorySwitch"] = item.Name == originalName && _grid.CurrentCell == address;
        DemoPressButton(DemoFind<Button>(this, "UseFactoryBButton")); UpdateLayout();
        checks["a5FactorySwitchPreservesDraftAndIdentity"] = ReferenceEquals(edit, _grid.EditSession) && edit.Cell == address && item.Name == originalName
            && _grid.RowsPresenter!.RealizedRows.SelectMany(row => row.RealizedCells).All(cell => Equals(cell.Tag, "factory-B"));
        checks["a5RetiredFactoryReleasedAllSubscriptions"] = a.Active == 0 && a.Subscriptions == 0 && a.Created == a.Released;
        var variant = _hostWindow.RequestedThemeVariant;
        _hostWindow.RequestedThemeVariant = ThemeVariant.Dark; _grid.ShowColumnHeaders = false; UpdateLayout();
        _hostWindow.RequestedThemeVariant = variant; _grid.ShowColumnHeaders = true; UpdateLayout();
        checks["a5HeaderAndThemeChangesPreserveDraft"] = ReferenceEquals(edit, _grid.EditSession) && Equals(edit.Value, "工厂切换后提交") && item.Name == originalName;
        DemoPressButton(DemoFind<Button>(this, "UseDefaultFactoryButton")); UpdateLayout();
        checks["a5DefaultFactoryRestoresStandardContainersWithoutCommit"] = _grid.ElementFactory is null && ReferenceEquals(edit, _grid.EditSession)
            && b.Active == 0 && b.Subscriptions == 0 && b.Created == b.Released && item.Name == originalName;
        var writes = 0;
        EventHandler<TreeDataGridCellValueChangedEventArgs> committed = (_, args) => { if (args.Reason == TreeDataGridCellValueChangeReason.EditCommit && args.Cell == address) writes++; };
        _grid.CellValueChanged += committed;
        DemoPressButton(DemoFind<Button>(this, "CommitFactoryEditButton"));
        _grid.CellValueChanged -= committed;
        checks["a5DraftCommitsOriginalModelOnce"] = _grid.EditSession is null && item.Name == "工厂切换后提交" && writes == 1;
        DemoPressButton(DemoFind<Button>(this, "UseFactoryAButton"));
        _grid.RowsPresenter!.Offset = new(160, 4_000); UpdateLayout();
        checks["a5ViewportBoundedFactoryAfterScroll"] = a.Active > 0 && a.Active <= 200 && a.Created - a.Released <= 220
            && a.Active == _grid.RowsPresenter.RealizedRowCount + _grid.RowsPresenter.RealizedCellCount + _grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Count();
        snapshots.Add(Snapshot("a5-element-factory"));
        if (imagePath is not null) DemoCapture(imagePath, "a5-element-factory", images);
    }
}
