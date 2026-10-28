using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Headless.XUnit;
using NT.AvaloniaControl.TreeDataGrid;
using GridControl = NT.AvaloniaControl.TreeDataGrid.TreeDataGrid;

namespace TreeDataGrid.Headless.Tests;

public sealed class ColumnSearchOptionsTests
{
    [AvaloniaFact]
    public void Disabled_and_hidden_columns_are_excluded_without_invoking_their_search_provider()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("alpha"), new("beta")]);
        var calls = 0;
        var text = new TreeDataGridTextColumn { ValueGetter = row => ((SearchOptionRow)row).Name, IsTextSearchEnabled = false };
        var template = new TreeDataGridTemplateColumn { TextSearchValueSelector = _ => { calls++; return "badge"; }, IsTextSearchEnabled = false };
        var grid = A1TestHost.Create(source, text, template);
        var window = A1TestHost.Show(grid);
        try
        {
            Assert.Null(grid.SearchText("alpha")); Assert.Null(grid.SearchText("badge")); Assert.Equal(0, calls);
            text.IsTextSearchEnabled = true;
            Assert.Equal(new CellAddress(source.Rows[0].Id, text.Id), grid.SearchText("ALP"));
            template.IsTextSearchEnabled = true;
            Assert.Equal(template.Id, grid.SearchText("badge")!.Value.Column);
            template.IsVisible = false;
            calls = 0;
            Assert.Null(grid.SearchText("badge")); Assert.Equal(0, calls);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Compiled_search_binding_reads_current_nested_objects_and_releases_each_temporary_subscription()
    {
        var model = new SearchOptionRow("row") { Detail = new("needle") };
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([model]);
        var column = new TreeDataGridTemplateColumn { TextSearchBinding = CompiledBinding.Create<SearchOptionRow, string>(row => row.Detail.Name) };
        var grid = A1TestHost.Create(source, column);
        var window = A1TestHost.Show(grid);
        try
        {
            var baseline = model.Subscribers;
            for (var i = 0; i < 4; i++)
            {
                Assert.NotNull(grid.SearchText("needle"));
                Assert.Equal(baseline, model.Subscribers);
                Assert.Equal(0, model.Detail.Subscribers);
            }
            var old = model.Detail;
            model.Detail = new("replacement");
            Assert.Null(grid.SearchText("needle")); Assert.NotNull(grid.SearchText("replace"));
            model.Detail.Name = "renamed";
            Assert.Null(grid.SearchText("replace")); Assert.NotNull(grid.SearchText("renamed"));
            Assert.Equal(0, old.Subscribers); Assert.Equal(0, model.Detail.Subscribers);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Selector_precedes_template_binding_and_text_null_selectors_retain_the_legacy_fallback()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("text value") { Detail = new("bound") }]);
        var template = new TreeDataGridTemplateColumn
        {
            TextSearchBinding = new ReflectionBinding("Detail.Name") { Converter = new FuncValueConverter<string, string>(value => "converted " + value) },
            TextSearchValueSelector = _ => "delegate",
        };
        var text = new TreeDataGridTextColumn { TextSearchValueSelector = _ => null, ValueGetter = row => ((SearchOptionRow)row).Name };
        var grid = A1TestHost.Create(source, template, text);
        var window = A1TestHost.Show(grid);
        try
        {
            Assert.Equal(template.Id, grid.SearchText("delegate")!.Value.Column);
            Assert.Null(grid.SearchText("converted"));
            template.TextSearchValueSelector = _ => null;
            Assert.Null(grid.SearchText("converted"));
            template.TextSearchValueSelector = null;
            Assert.Equal(template.Id, grid.SearchText("converted bound")!.Value.Column);
            Assert.Equal(text.Id, grid.SearchText("text value")!.Value.Column);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Tree_search_honors_both_outer_and_inner_flags_and_only_searches_visible_nodes()
    {
        var child = new SearchOptionRow("child needle");
        var root = new SearchOptionRow("root") { Children = [child] };
        using var source = new HierarchicalTreeDataGridSource<SearchOptionRow>([root], row => row.Children);
        var inner = new TreeDataGridTemplateColumn { TextSearchBinding = new ReflectionBinding(nameof(SearchOptionRow.Name)) };
        var tree = new TreeDataGridHierarchicalExpanderColumn { InnerColumn = inner, IsTextSearchEnabled = false };
        var grid = A1TestHost.Create(source, tree);
        var window = A1TestHost.Show(grid);
        try
        {
            Assert.Null(grid.SearchText("root"));
            tree.IsTextSearchEnabled = true; inner.IsTextSearchEnabled = false;
            Assert.Null(grid.SearchText("root"));
            inner.IsTextSearchEnabled = true;
            Assert.NotNull(grid.SearchText("root")); Assert.Null(grid.SearchText("child"));
            source.ExpandAll();
            Assert.Equal(child, source.Rows.Single(row => row.Id == grid.SearchText("child")!.Value.Node).Model);
            source.SetFilter(row => !ReferenceEquals(row, child));
            Assert.Null(grid.SearchText("child"));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Provider_source_replacement_or_column_mutation_does_not_select_a_stale_target()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("old")]);
        using var replacement = new FlatTreeDataGridSource<SearchOptionRow>([new("new")]);
        var column = new TreeDataGridTemplateColumn();
        var grid = A1TestHost.Create(source, column);
        var window = A1TestHost.Show(grid);
        try
        {
            column.TextSearchValueSelector = _ => { grid.Source = replacement; return "needle"; };
            Assert.Null(grid.SearchText("needle")); Assert.Null(grid.State!.CurrentCell);
            column.TextSearchValueSelector = _ => { column.IsVisible = false; return "needle"; };
            Assert.Null(grid.SearchText("needle")); Assert.Null(grid.State.CurrentCell);
            Assert.Empty(grid.VisibleColumns);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Matching_search_commits_an_old_editor_once_and_failed_validation_preserves_selection_and_offset()
    {
        var models = Enumerable.Range(0, 150).Select(i => new SearchOptionRow(i == 149 ? "target" : "row " + i)).ToArray();
        using var source = new FlatTreeDataGridSource<SearchOptionRow>(models);
        var valid = false; var writes = 0;
        var column = new TreeDataGridTextColumn
        {
            ValueGetter = row => ((SearchOptionRow)row).Name,
            ValueSetter = (row, value) => { writes++; ((SearchOptionRow)row).Name = (string)value!; },
            ValueValidator = _ => valid ? null : "invalid",
        };
        var grid = A1TestHost.Create(source, column);
        var window = A1TestHost.Show(grid);
        try
        {
            var current = new CellAddress(source.Rows[0].Id, column.Id);
            grid.State!.Select(source.Rows[0].Id); grid.State.SetCurrentCell(current);
            Assert.True(grid.BeginEdit(current));
            var edit = grid.State.EditSession!; edit.Value = "renamed";
            Assert.Null(grid.SearchText("missing")); Assert.Null(edit.ValidationError);
            Assert.Null(grid.SearchText("target"));
            Assert.Equal("invalid", edit.ValidationError); Assert.Equal(0, writes);
            Assert.Equal(current, grid.State.CurrentCell); Assert.Equal(default(Vector), grid.RowsPresenter!.Offset);
            valid = true;
            var found = grid.SearchText("target");
            Assert.Equal(new CellAddress(source.Rows[149].Id, column.Id), found);
            Assert.Equal(1, writes); Assert.Null(grid.State.EditSession); Assert.True(grid.RowsPresenter.Offset.Y > 0);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Search_callback_reentry_is_rejected_and_does_not_poison_the_next_search()
    {
        using var source = new FlatTreeDataGridSource<SearchOptionRow>([new("row")]);
        var column = new TreeDataGridTemplateColumn();
        var grid = A1TestHost.Create(source, column);
        var window = A1TestHost.Show(grid);
        try
        {
            column.TextSearchValueSelector = _ => grid.SearchText("needle")?.ToString();
            Assert.Throws<InvalidOperationException>(() => grid.SearchText("needle"));
            Assert.Null(grid.State!.CurrentCell);
            column.TextSearchValueSelector = _ => "needle";
            Assert.NotNull(grid.SearchText("needle"));
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Compiled_xaml_consumes_search_gestures_sort_overrides_and_header_visibility()
    {
        var view = new A1ColumnOptionsView();
        var grid = view.FindControl<GridControl>("OptionsGrid")!;
        grid.ItemsSource = new[] { new SearchOptionRow("row") { Detail = new("compiled needle") } };
        var window = A1TestHost.Show(view);
        try
        {
            Assert.False(grid.ShowColumnHeaders);
            Assert.Equal(TreeDataGridBeginEditGestures.F2 | TreeDataGridBeginEditGestures.WhenSelected, grid.Columns[0].BeginEditGestures);
            Assert.False(grid.Columns[0].AllowTriStateSorting);
            Assert.False(grid.Columns[0].IsTextSearchEnabled);
            Assert.Equal(grid.Columns[1].Id, grid.SearchText("compiled needle")!.Value.Column);
            grid.ShowColumnHeaders = true; window.UpdateLayout();
            Assert.NotNull(A1TestHost.Header(grid));
        }
        finally { window.Close(); grid.ItemsSource = null; }
    }
}

public sealed class SearchOptionRow(string name) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    private string _name = name;
    private SearchOptionDetail _detail = new("");
    public string Name { get => _name; set { _name = value; _changed?.Invoke(this, new(nameof(Name))); } }
    public SearchOptionDetail Detail { get => _detail; set { _detail = value; _changed?.Invoke(this, new(nameof(Detail))); } }
    public ObservableCollection<SearchOptionRow> Children { get; init; } = [];
    public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}

public sealed class SearchOptionDetail(string name) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    private string _name = name;
    public string Name { get => _name; set { _name = value; _changed?.Invoke(this, new(nameof(Name))); } }
    public int Subscribers => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}
