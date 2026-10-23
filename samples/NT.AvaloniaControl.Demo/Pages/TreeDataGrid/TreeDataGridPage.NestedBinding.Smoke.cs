using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private async Task RunNestedBindingDemoSmokeAsync(Dictionary<string, bool> checks, List<object> snapshots, string? imagePath, List<string> images)
    {
        await DemoSelectScenarioAsync("template-clock");
        var previousClockRunning = _templateClockTimer?.IsEnabled == true;
        await DemoSelectScenarioAsync("nestedbindings");
        checks["b2NestedScenarioStopsPreviousTemplateClock"] = previousClockRunning
            && _templateClockItems is null && _templateClockTimer?.IsEnabled != true;
        var root = _nestedBindingRoots![0];
        var source = _source!;
        var rootId = source.Rows[0].Id;
        checks["b2NestedItemsSourceOwnsStableSource"] = ReferenceEquals(_grid.ItemsSource, _nestedBindingRoots)
            && ReferenceEquals(source, _grid.Source) && source.Rows.Count == 3;
        var quantity = (TreeDataGridTextColumn)_grid.Columns[1];
        checks["b3CompiledDisplayHasExplicitTypedWriter"] = quantity.Binding is CompiledBinding
            && quantity.ValueType == typeof(int) && quantity.ValueGetter is not null && quantity.ValueSetter is not null
            && ((TreeDataGridHierarchicalExpanderColumn)_grid.Columns[0]).InnerColumn is TreeDataGridTextColumn { Binding: CompiledBinding };
        source.SetExpanded(rootId, false);
        var collapsed = root.State!.Expanded == false && source.Rows.Count == 1;
        source.SetExpanded(rootId, true);
        checks["b2NestedExpansionWritesState"] = collapsed && root.State!.Expanded && source.Rows.Count == 3;

        var originalState = root.State!;
        DemoPressButton(DemoFind<Button>(this, "ReplaceNestedStateButton"));
        await Task.Delay(100);
        UpdateLayout();
        checks["b2StateReplacementChangesTreeAndDisplay"] = ReferenceEquals(source, _source) && source.Rows[0].Id == rootId
            && root.State!.Label == "替换后的状态" && source.Rows.Count == 3 && NestedBindingText(0, 2) == root.State!.Label;
        checks["b2StateReplacementUnsubscribesOldObject"] = originalState.SubscriptionCount == 0;
        var version = source.Version;
        originalState.Label = "旧状态不能影响新界面";
        originalState.Children.Add(new NestedBindingDemoItem("旧集合节点"));
        checks["b2DetachedStateDoesNotRefreshSource"] = source.Version == version && root.State!.Label == "替换后的状态";

        var oldChildren = root.State!.Children;
        var oldChild = oldChildren[0];
        DemoPressButton(DemoFind<Button>(this, "ReplaceNestedChildrenButton"));
        await Task.Delay(100);
        version = source.Version;
        oldChildren.Add(new NestedBindingDemoItem("失效子集合的节点"));
        checks["b2ChildCollectionReplacementDetachesOldModels"] = source.Version == version && source.Rows.Count == 3
            && source.Rows[1].Model is NestedBindingDemoItem { Name: "替换集合的子节点 1" } && oldChild.SubscriptionCount == 0;

        var nulledState = root.State!;
        DemoPressButton(DemoFind<Button>(this, "NullNestedStateButton"));
        await Task.Delay(100);
        checks["b2NullStateIsEmptyHierarchy"] = root.State is null && source.Rows.Count == 1 && source.Rows[0].Id == rootId
            && !source.Rows[0].HasChildren && !source.Rows[0].IsExpanded && nulledState.SubscriptionCount == 0;
        DemoPressButton(DemoFind<Button>(this, "RestoreNestedStateButton"));
        await Task.Delay(100);
        UpdateLayout();
        checks["b2RestoredStateRebindsTree"] = root.State is { Expanded: true, Label: "恢复状态" }
            && source.Rows.Count == 3 && NestedBindingText(0, 2) == "恢复状态";

        DemoPressButton(DemoFind<Button>(this, "BeginNestedQuantityEditButton"));
        var edit = _grid.EditSession ?? throw new InvalidOperationException("The nested scenario did not create its typed edit session.");
        var originalQuantity = root.Quantity;
        edit.Value = "invalid";
        DemoPressButton(DemoFind<Button>(this, "CommitNestedEditButton"));
        checks["b3TypedConversionFailureHasZeroWrites"] = ReferenceEquals(edit, _grid.EditSession)
            && edit.ValidationError is not null && root.Quantity == originalQuantity && root.QuantityWrites == 0;
        edit.Value = "101";
        DemoPressButton(DemoFind<Button>(this, "CommitNestedEditButton"));
        checks["b3TypedValidationFailureHasZeroWrites"] = ReferenceEquals(edit, _grid.EditSession)
            && edit.ValidationError == "数量必须为 0 到 100 的整数。" && root.QuantityWrites == 0;
        edit.Value = "42";
        DemoPressButton(DemoFind<Button>(this, "CommitNestedEditButton"));
        await Task.Delay(100);
        UpdateLayout();
        checks["b3TypedCommitUpdatesCompiledDisplayOnce"] = _grid.EditSession is null && root.Quantity == 42
            && root.QuantityWrites == 1 && NestedBindingText(0, 1) == "42";
        DemoPressButton(DemoFind<Button>(this, "BeginNestedQuantityEditButton"));
        _grid.EditSession!.Value = "77";
        DemoPressButton(DemoFind<Button>(this, "CancelNestedEditButton"));
        checks["b3TypedCancelHasZeroAdditionalWrites"] = _grid.EditSession is null && root.Quantity == 42 && root.QuantityWrites == 1;

        var oldTarget = root.State!;
        var oldLabel = oldTarget.Label;
        var label = _grid.Columns[2];
        var began = _grid.BeginEdit(new CellAddress(rootId, label.Id));
        _grid.EditSession!.Value = "不允许写入新对象的旧草稿";
        DemoPressButton(DemoFind<Button>(this, "ReplaceNestedStateButton"));
        await Task.Delay(100);
        var refused = !_grid.CommitEdit();
        checks["b2ReplacedEditTargetRejectsOldDraftWithZeroWrites"] = began && refused && _grid.EditSession is not null
            && oldTarget.Label == oldLabel && root.State!.Label == "替换后的状态";
        _grid.CancelEdit();
        var restarted = _grid.BeginEdit(new CellAddress(rootId, label.Id));
        _grid.EditSession!.Value = "新对象的新草稿";
        checks["b2CancelAndRestartWritesTheCurrentTarget"] = restarted && _grid.CommitEdit() && root.State!.Label == "新对象的新草稿"
            && oldTarget.Label == oldLabel;
        await Task.Delay(100);
        snapshots.Add(Snapshot("b2-b3-nested-binding"));
        if (imagePath is not null) DemoCapture(imagePath, "b2-b3-nested-binding", images);

        await DemoSelectScenarioAsync("editing");
        checks["b2LeavingScenarioReleasesOwnedNestedSource"] = _grid.ItemsSource is null && _nestedBindingRoots is null
            && root.SubscriptionCount == 0 && root.State!.SubscriptionCount == 0;
    }

    private string? NestedBindingText(int rowIndex, int columnIndex) => _grid.RowsPresenter!.RealizedRows
        .Single(row => row.RowIndex == rowIndex).RealizedCells.Single(cell => cell.ColumnIndex == columnIndex)
        .GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).FirstOrDefault();
}
