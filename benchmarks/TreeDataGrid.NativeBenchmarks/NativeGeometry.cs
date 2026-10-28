using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NT.AvaloniaControl.TreeDataGrid;

namespace TreeDataGridNativeBenchmarks;

// A native geometry oracle, deliberately outside the timed performance protocol.
internal static class NativeGeometry
{
    internal static async Task<int> RunAsync(Window window, string output)
    {
        Directory.CreateDirectory(output);
        using var stream = new StreamWriter(new FileStream(Path.Combine(output, "geometry.jsonl"), FileMode.CreateNew));
        var root = new Grid(); var witness = new FrameWitness { ZIndex = 1000, IsHitTestVisible = false };
        root.Children.Add(witness); window.Content = root;
        var expected = new Dictionary<string, string>(); var differences = new List<string>(); var frames = 0; var snapshots = 0;
        string? error = null;
        var environment = new { protocol = "treedatagrid-native-geometry-v1", functionSha256 = NativeReport.Hash(typeof(TreeDataGridRowsPresenter).Assembly.Location),
            hostSha256 = NativeReport.Hash(typeof(NativeGeometry).Assembly.Location), themeSha256 = NativeReport.Hash(typeof(NT.AvaloniaControl.TreeDataGrid.Themes.TreeDataGridTheme).Assembly.Location),
            window.ClientSize, window.RenderScaling, native = OperatingSystem.IsWindows() && window.TryGetPlatformHandle() is { Handle: not 0 } };
        File.WriteAllText(Path.Combine(output, "initial.json"), JsonSerializer.Serialize(environment, NativeReport.Json));
        try
        {
            NativeFixture.Require(environment.native && window.IsVisible, "A visible Windows native window is required.");
            foreach (var plan in new[] { "scroll", "mutations", "lifecycle" })
            foreach (var scenario in new[] { "auto-default", "auto-factory" })
            for (var round = 0; round < (plan == "scroll" ? 5 : 1); round++)
            {
                window.RequestedThemeVariant = ThemeVariant.Light;
                using var fixture = new NativeFixture(scenario, observeEvents: true);
                root.Children.Insert(0, fixture.Grid); window.UpdateLayout();
                try
                {
                    for (var step = 0; step < (plan == "lifecycle" ? 24 : 90); step++)
                    {
                        if (plan == "mutations") Mutate(fixture, step);
                        if (plan == "lifecycle") Lifecycle(fixture, step);
                        fixture.Scroll(step); window.UpdateLayout();
                        Write("layout", plan, round, scenario, step, fixture);
                        await witness.ObserveAsync(() => { Write("render", plan, round, scenario, step, fixture); return fixture.Snapshot(); });
                        NativeFixture.Require(window.ClientSize == environment.ClientSize && window.RenderScaling == environment.RenderScaling, "Native geometry viewport/DPI changed.");
                        frames++;
                    }
                }
                finally { root.Children.Remove(fixture.Grid); window.UpdateLayout(); }
            }
        }
        catch (Exception exception) { error = exception.ToString(); }
        var passed = error is null && differences.Count == 0 && frames == 1128 && snapshots == 2256 && expected.Count == 1128;
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new { environment.protocol, passed, frames, snapshots,
            expectedSnapshots = expected.Count, differences, error, boundary = "Synchronous layout and correlated native UI render geometry; no physical scan-out or timing claims." }, NativeReport.Json));
        return passed ? 0 : 1;

        void Mutate(NativeFixture fixture, int step)
        {
            var grid = fixture.Grid;
            if (step % 9 == 0) fixture.Source!.Refresh();
            if (step % 13 == 0) fixture.NavigateAndEdit(step);
            if (step == 15 || step == 30) foreach (var column in grid.Columns) column.Width = new GridLength(step == 15 ? 90 : 140);
            if (step == 20 || step == 25) grid.FrozenColumnCount = step == 20 ? 0 : 3;
            if (step == 35 || step == 55) grid.MaxWidth = step == 35 ? 800 : double.PositiveInfinity;
            if (step == 45 || step == 52) grid.FontSize = step == 45 ? 18 : 14;
            if (step == 60) window.RequestedThemeVariant = ThemeVariant.Dark;
        }
        static void Lifecycle(NativeFixture fixture, int step)
        {
            var grid = fixture.Grid; var model = fixture.Models[0];
            var address = new CellAddress(fixture.Source!.Rows[0].Id, grid.Columns[0].Id);
            if (step is 2 or 6) { NativeFixture.Require(grid.BeginEdit(address), "Geometry draft failed."); grid.EditSession!.Value = "draft"; }
            if (step == 3) { var edit = grid.EditSession; fixture.Source.Refresh(); NativeFixture.Require(ReferenceEquals(edit, grid.EditSession) && model.Writes == 0, "Refresh lost or wrote a draft."); }
            if (step == 4) { grid.CancelEdit(); NativeFixture.Require(model.Writes == 0, "Cancelled geometry draft wrote a value."); }
            if (step == 7) { var edit = grid.EditSession; fixture.RebuildTemplate(); NativeFixture.Require(ReferenceEquals(edit, grid.EditSession), "Template switch lost a draft."); }
            if (step == 8) NativeFixture.Require(grid.CommitEdit() && model.Writes == 1, "Geometry draft must write its original model once.");
            if (step == 10) { fixture.Source.Refresh(); fixture.ReplaceFactory((_, _) => { }); }
            if (step == 11) fixture.UseDefaultFactory((_, _) => { });
            if (step == 12) { fixture.Source.Refresh(); fixture.ReplaceSource((_, _) => { }); }
            if (step == 15) { fixture.Source.Refresh(); fixture.RebuildTemplate(); }
        }
        void Write(string phase, string plan, int round, string scenario, int step, NativeFixture fixture)
        {
            fixture.Snapshot(); // Independently checks source/model/column/text and A2/factory lifetimes.
            var presenter = fixture.Grid.RowsPresenter!;
            var geometry = new { offset = new[] { presenter.Offset.X, presenter.Offset.Y }, extent = S(presenter.Extent),
                rows = presenter.RealizedRows.OrderBy(r => r.RowIndex).Select(row => new { row.RowIndex, height = presenter.Layout!.GetRowHeight(row.RowIndex), desired = S(row.DesiredSize), bounds = B(row.Bounds),
                    cells = row.RealizedCells.Select(cell => new { cell.ColumnIndex, desired = S(cell.DesiredSize), bounds = B(cell.Bounds), cell.IsMeasureValid,
                        text = cell.Content is TextBlock text ? new { text.Text, text.FontSize, desired = S(text.DesiredSize), text.IsMeasureValid, bounds = B(text.Bounds) } : null,
                        template = cell.GetVisualDescendants().OfType<ContentPresenter>().Select(cp => new { desired = S(cp.DesiredSize), cp.IsMeasureValid, bounds = B(cp.Bounds), same = ReferenceEquals(cp.Child, cell.Content) }).ToArray() }).ToArray() }).ToArray() };
            var key = $"{plan}/{round}/{step}/{phase}"; var serialized = JsonSerializer.Serialize(geometry);
            if (scenario == "auto-default") expected.Add(key, serialized);
            else if (expected[key] != serialized) differences.Add(key);
            stream.WriteLine(JsonSerializer.Serialize(new { phase, plan, round, scenario, step, geometry }));
            stream.Flush(); snapshots++;
        }
        static double[] S(Size size) => [size.Width, size.Height];
        static double[] B(Rect rect) => [rect.X, rect.Y, rect.Width, rect.Height];
    }
}
