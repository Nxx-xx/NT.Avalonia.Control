using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;

namespace NT.AvaloniaControl.Demo.Pages.TreeDataGrid;

internal sealed partial class TreeDataGridPage
{
    private object Snapshot(string name) => new
    {
        Name = name,
        ProjectionRows = _source?.Rows.Count,
        Columns = _grid.Columns.Count,
        _grid.FrozenColumnCount,
        RealizedRows = _grid.RowsPresenter?.RealizedRowCount,
        CreatedRows = _grid.RowsPresenter?.CreatedRowCount,
        RecycledRows = _grid.RowsPresenter?.RecycledRowCount,
        RealizedCells = _grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>().Count(),
        CreatedCells = _grid.RowsPresenter?.CreatedCellCount,
        RecycledCells = _grid.RowsPresenter?.RecycledCellCount,
        RealizedHeaders = _grid.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Count(),
        OffsetX = _grid.RowsPresenter?.Offset.X,
        OffsetY = _grid.RowsPresenter?.Offset.Y,
        ViewerOffsetX = _grid.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()?.Offset.X,
        ViewerOffsetY = _grid.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault()?.Offset.Y,
        MaximumRealizedRowHeight = _grid.RowsPresenter?.RealizedRows.Select(row => row.Bounds.Height).DefaultIfEmpty(0).Max(),
        VisualControlCount = _grid.GetVisualDescendants().OfType<Control>().Count(),
    };

    internal async Task RunSmokeAsync(string[] args)
    {
        var outputIndex = Array.IndexOf(args, "--smoke-output");
        var outputPath = outputIndex >= 0 && outputIndex + 1 < args.Length
            ? args[outputIndex + 1]
            : Path.Combine(Environment.CurrentDirectory, "artifacts", "platform", "windows-smoke.json");
        var imageIndex = Array.IndexOf(args, "--smoke-image");
        var imagePath = imageIndex >= 0 && imageIndex + 1 < args.Length ? Path.GetFullPath(args[imageIndex + 1]) : null;
        var checks = new Dictionary<string, bool>();
        var snapshots = new List<object>();
        string? error = null;
        long firstLayoutMilliseconds = 0;
        try
        {
            await Task.Delay(350);
            firstLayoutMilliseconds = _startup.ElapsedMilliseconds;
            checks["nativeWindowCreated"] = _hostWindow.TryGetPlatformHandle() is { Handle: not 0 };
            checks["nonzeroLayout"] = _grid.Bounds.Width > 100 && _grid.Bounds.Height > 100;
            checks["rowsVirtualized"] = _grid.RowsPresenter?.RealizedRowCount is > 0 and < 200;
            _input.Text = ChineseProbe;
            checks["chineseTextRoundTrip"] = _input.Text == ChineseProbe;
            checks["sourcePathMapping"] = _source!.TryGetNodeId(new IndexPath(50_000), out var mappedId) && _source.GetViewIndex(mappedId) == 50_000;
            snapshots.Add(Snapshot("100k-flat-initial"));
            var scrollViewer = _grid.GetVisualDescendants().OfType<ScrollViewer>().First(view => view.Name == "PART_ScrollViewer");
            _grid.FrozenColumnCount = 2;
            scrollViewer.Offset = new Vector(600, 8000);
            await Task.Delay(150);
            checks["scrollAndRecycle"] = scrollViewer.Offset.X > 0 && scrollViewer.Offset.Y > 0 && _grid.RowsPresenter?.RecycledRowCount > 0;
            checks["scrollKeepsBoundedRows"] = _grid.RowsPresenter?.RealizedRowCount is > 0 and < 200;
            snapshots.Add(Snapshot("scrolled-and-frozen"));
            var anchorBeforeHeight = GetTopNode();
            _grid.RowHeightSelector = row => row.Model is DemoItem item && item.Id % 7 == 0 ? 52 : 28;
            await Task.Delay(100);
            checks["variableHeightAnchor"] = GetTopNode() == anchorBeforeHeight;
            snapshots.Add(Snapshot("variable-row-heights"));
            var sourceBeforeTheme = _grid.Source;
            _hostWindow.RequestedThemeVariant = ThemeVariant.Dark;
            await Task.Delay(100);
            checks["themePreservesSource"] = ReferenceEquals(_grid.Source, sourceBeforeTheme) && _source.GetViewIndex(mappedId) == 50_000;
            ShowTree();
            await Task.Delay(100);
            checks["treeProjection"] = _source.Rows.Count == 124 && _source.TryGetNodeId(new IndexPath(0, 0), out var childId) && _source.GetViewIndex(childId) == 1;
            snapshots.Add(Snapshot("tree-projection"));
            await RunP2P3SmokeAsync(checks, snapshots, imagePath);
            if (imagePath is not null) CaptureSmokeImage(imagePath);
            await RunP4P5SmokeAsync(checks, snapshots, imagePath);
        }
        catch (Exception ex)
        {
            error = ex.ToString();
        }
        var passed = error is null && checks.Count == 36 && checks.Values.All(result => result);
        try
        {
            var fullPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, JsonSerializer.Serialize(new
            {
                Passed = passed,
                TimestampUtc = DateTimeOffset.UtcNow,
                OS = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                Runtime = Environment.Version.ToString(),
                AvaloniaVersion = typeof(Application).Assembly.GetName().Version?.ToString(),
                WindowWidthDip = _hostWindow.ClientSize.Width,
                WindowHeightDip = _hostWindow.ClientSize.Height,
                RenderScaling = _hostWindow.RenderScaling,
                GridFontSize = _grid.FontSize,
                GridFontFamily = _grid.FontFamily.Name,
                PlatformDefaultFontFamily = Avalonia.Media.FontManager.Current.DefaultFontFamily.Name,
                FirstLayoutMilliseconds = firstLayoutMilliseconds,
                BuildConfiguration =
#if DEBUG
                    "Debug",
#else
                    "Release",
#endif
                Checks = checks,
                Snapshots = snapshots,
                ImageOutputPath = imagePath,
                ImageOutputs = imagePath is null ? null : new[] { imagePath, ScenarioImagePath(imagePath, "tree"), ScenarioImagePath(imagePath, "columns"), ScenarioImagePath(imagePath, "selection"), ScenarioImagePath(imagePath, "editing"), ScenarioImagePath(imagePath, "sort-filter") },
                ImageCaptureKind = imagePath is null ? null : "Avalonia RenderTargetBitmap of the Window visual; offscreen control rendering, not a desktop screenshot.",
                NativeImeValidated = false,
                KylinValidated = false,
                Notes = "Real native window smoke probe; programmatic Chinese TextBox round-trip does not exercise IME preedit, candidate window, commit or cancellation. FirstLayoutMilliseconds includes the initial 350 ms settle delay; it is not a rendering benchmark.",
                Error = error,
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown(passed ? 0 : 1);
            else _hostWindow.Close();
        }
    }

    private NodeId? GetTopNode()
    {
        var presenter = _grid.RowsPresenter;
        var index = presenter?.Layout?.FindRow(presenter.Offset.Y) ?? -1;
        return index >= 0 && index < _source!.Rows.Count ? _source.Rows[index].Id : null;
    }

    private async Task RunP2P3SmokeAsync(Dictionary<string, bool> checks, List<object> snapshots, string? imagePath)
    {
        var tree = (HierarchicalTreeDataGridSource<DemoItem>)_source!;
        tree.CollapseAll();
        var collapsed = tree.Rows.Count == 100;
        tree.ExpandAll();
        var expanded = tree.Rows.Count == 900;
        ExpandEvenRoots();
        checks["treeBatchOperations"] = collapsed && expanded && tree.Rows.Count == 500;
        AddChild();
        var added = tree.Rows.Count == 501 && tree.TryGetNodeId(new IndexPath(0, 8), out _);
        RemoveChild();
        checks["dynamicChildren"] = added && tree.Rows.Count == 500 && !tree.TryGetNodeId(new IndexPath(0, 8), out _);
        await Task.Delay(100);
        var presenter = _grid.RowsPresenter!;
        presenter.Offset = new Vector(0, 1200);
        await Task.Delay(100);
        var anchor = GetTopNode();
        AddChild();
        await Task.Delay(100);
        checks["treeInsertionKeepsAnchor"] = anchor is not null && GetTopNode() == anchor;
        snapshots.Add(Snapshot("tree-operations-and-anchor"));
        if (imagePath is not null) CaptureSmokeImage(ScenarioImagePath(imagePath, "tree"));

        ShowLazyTree();
        await Task.Delay(100);
        // Route the same Click event as the native expander button; physical pointer/IME remain separate tests.
        var expander = _grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>()
            .Where(cell => cell.Column is TreeDataGridHierarchicalExpanderColumn)
            .SelectMany(cell => cell.GetVisualDescendants().OfType<Button>()).FirstOrDefault();
        expander?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(100);
        checks["lazyExpansion"] = expander is not null && _lazyLoadCount == 1 && _source!.Rows.Count == 4;
        _treeRoots![0].IsExpanded = false;
        var boundCollapsed = _source!.Rows.Count == 1;
        _treeRoots[0].IsExpanded = true;
        checks["modelExpansionBinding"] = boundCollapsed && _source.Rows.Count == 4 && _lazyLoadCount == 1;
        var treeSource = _source;
        var treeIds = _source.Rows.Select(row => row.Id).ToArray();
        ToggleApplicationTemplate();
        await Task.Delay(100);
        checks["customTemplateKeepsTree"] = ReferenceEquals(treeSource, _source) && treeIds.SequenceEqual(_source.Rows.Select(row => row.Id));
        snapshots.Add(Snapshot("lazy-bound-tree-custom-template"));
        ToggleApplicationTemplate();

        ShowColumnTypes();
        await Task.Delay(100);
        var visuals = _grid.GetVisualDescendants().ToArray();
        checks["columnTypes"] = visuals.OfType<CheckBox>().Any(box => box.IsThreeState && box.IsChecked is null)
            && visuals.OfType<TextBlock>().Any(text => text.Text == "待确认")
            && visuals.OfType<TreeDataGridCellPresenter>().Any(cell => cell.Column is TreeDataGridRowHeaderColumn);
        var layout = _grid.RowsPresenter!.Layout!;
        checks["autoAndStarColumns"] = layout.GetColumnWidth(1) >= 120 && layout.GetColumnWidth(1) <= 280
            && layout.GetColumnWidth(3) >= 140 && layout.GetColumnWidth(4) >= 180;
        _grid.Columns[4].Width = new GridLength(220);
        _grid.Columns[4].Header = "换行说明（220 DIP）";
        _grid.AutoRowHeight = true;
        _lastAction.Text = "自动行高：说明列宽为 220 DIP，长中文内容换行；模板列仍以 Star 分配余宽。";
        await Task.Delay(150);
        checks["autoRowHeights"] = _grid.RowsPresenter.RealizedRows.Any(row => row.Bounds.Height > _grid.RowHeight + 1);
        snapshots.Add(Snapshot("column-types-and-wrapped-rows"));
        if (imagePath is not null) CaptureSmokeImage(ScenarioImagePath(imagePath, "columns"));

        ShowFlat(100);
        _grid.FrozenColumnCount = 2;
        await Task.Delay(100);
        var beforeCells = _grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>().Where(cell => cell.ColumnIndex >= 2).ToHashSet();
        _grid.RowsPresenter!.Offset = new Vector(7000, 5000);
        await Task.Delay(150);
        var liveRows = _grid.RowsPresenter.RealizedRows;
        checks["horizontalCellsBounded"] = liveRows.Count > 0 && liveRows.All(row => row.Children.OfType<TreeDataGridCellPresenter>().Count() is > 0 and < 30);
        checks["horizontalCellRecycling"] = _grid.GetVisualDescendants().OfType<TreeDataGridCellPresenter>().Any(beforeCells.Contains);
        snapshots.Add(Snapshot("100k-by-100-scrolled"));
    }

    private static string ScenarioImagePath(string imagePath, string scenario) =>
        Path.Combine(Path.GetDirectoryName(imagePath)!, Path.GetFileNameWithoutExtension(imagePath) + "-" + scenario + ".png");

    private void CaptureSmokeImage(string path)
    {
        UpdateStatus();
        _hostWindow.UpdateLayout();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(_hostWindow.ClientSize.Width * _hostWindow.RenderScaling), (int)Math.Ceiling(_hostWindow.ClientSize.Height * _hostWindow.RenderScaling)), new Vector(96 * _hostWindow.RenderScaling, 96 * _hostWindow.RenderScaling));
        bitmap.Render(_hostWindow);
        bitmap.Save(path);
    }

}
