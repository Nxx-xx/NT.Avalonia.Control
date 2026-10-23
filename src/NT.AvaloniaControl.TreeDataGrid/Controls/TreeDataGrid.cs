using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.Metadata;

namespace NT.AvaloniaControl.TreeDataGrid;

[TemplatePart("PART_ScrollViewer", typeof(ScrollViewer), IsRequired = true)]
[TemplatePart("PART_RowsPresenter", typeof(TreeDataGridRowsPresenter), IsRequired = true)]
[TemplatePart("PART_ColumnHeadersPresenter", typeof(TreeDataGridColumnHeadersPresenter))]
[PseudoClasses(":empty")]
public partial class TreeDataGrid : TemplatedControl
{
    public static readonly StyledProperty<TreeDataGridSource?> SourceProperty =
        AvaloniaProperty.Register<TreeDataGrid, TreeDataGridSource?>(nameof(Source), coerce: CoerceSource);
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<TreeDataGrid, double>(nameof(RowHeight), 28, validate: x => double.IsFinite(x) && x > 0);
    public static readonly StyledProperty<int> FrozenColumnCountProperty =
        AvaloniaProperty.Register<TreeDataGrid, int>(nameof(FrozenColumnCount), 0, validate: x => x >= 0);
    public static readonly StyledProperty<Func<TreeDataGridRow, double>?> RowHeightSelectorProperty =
        AvaloniaProperty.Register<TreeDataGrid, Func<TreeDataGridRow, double>?>(nameof(RowHeightSelector));
    public static readonly StyledProperty<bool> AutoRowHeightProperty = AvaloniaProperty.Register<TreeDataGrid, bool>(nameof(AutoRowHeight));
    public static readonly StyledProperty<bool> CanUserResizeColumnsProperty = AvaloniaProperty.Register<TreeDataGrid, bool>(nameof(CanUserResizeColumns), true);
    public static readonly StyledProperty<ControlTheme?> CellThemeProperty = AvaloniaProperty.Register<TreeDataGrid, ControlTheme?>(nameof(CellTheme));
    public static readonly StyledProperty<ControlTheme?> ColumnHeaderThemeProperty = AvaloniaProperty.Register<TreeDataGrid, ControlTheme?>(nameof(ColumnHeaderTheme));
    private readonly HashSet<TreeDataGridColumn> _subscribedColumns = [];
    private TreeDataGridSource? _subscribedSource;
    private TreeDataGridColumnHeadersPresenter? _headers;
    private bool _attached;

    public TreeDataGrid()
    {
        Columns.CollectionChanged += ColumnsChanged;
        PseudoClasses.Set(":empty", true);
        InitializeInteraction();
        InitializeRowMoving();
    }
    /// <summary>Explicit sources belong to the application; an ItemsSource adapter belongs to this control.</summary>
    public TreeDataGridSource? Source { get => GetValue(SourceProperty); set { ValidateExplicitSource(); SetValue(SourceProperty, value); } }
    [Content]
    public ObservableCollection<TreeDataGridColumn> Columns { get; } = new ColumnCollection();
    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    public int FrozenColumnCount { get => GetValue(FrozenColumnCountProperty); set => SetValue(FrozenColumnCountProperty, value); }
    public Func<TreeDataGridRow, double>? RowHeightSelector { get => GetValue(RowHeightSelectorProperty); set => SetValue(RowHeightSelectorProperty, value); }
    public bool AutoRowHeight { get => GetValue(AutoRowHeightProperty); set => SetValue(AutoRowHeightProperty, value); }
    public bool CanUserResizeColumns { get => GetValue(CanUserResizeColumnsProperty); set => SetValue(CanUserResizeColumnsProperty, value); }
    public ControlTheme? CellTheme { get => GetValue(CellThemeProperty); set => SetValue(CellThemeProperty, value); }
    public ControlTheme? ColumnHeaderTheme { get => GetValue(ColumnHeaderThemeProperty); set => SetValue(ColumnHeaderThemeProperty, value); }
    public TreeDataGridRowsPresenter? RowsPresenter { get; private set; }
    public event EventHandler<TreeDataGridRowEventArgs>? RowPrepared;
    /// <summary>Raised before bindings and DataContext are removed from a recycled row.</summary>
    public event EventHandler<TreeDataGridRowEventArgs>? RowClearing;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        AutomationScrollViewer = null;
        ColumnOperationVersion++;
        CancelColumnReorder();
        CancelRowDrag();
        RowsPresenter?.SetOwner(null, null);
        _headers?.SetOwner(null);
        RowsPresenter = null;
        _headers = null;
        base.OnApplyTemplate(e);
        AutomationScrollViewer = RequirePart<ScrollViewer>(e, "PART_ScrollViewer");
        RowsPresenter = RequirePart<TreeDataGridRowsPresenter>(e, "PART_RowsPresenter");
        _headers = e.NameScope.Find<TreeDataGridColumnHeadersPresenter>("PART_ColumnHeadersPresenter");
        RowsPresenter.SetOwner(this, _attached ? Source : null);
        _headers?.SetOwner(this);
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        RefreshVisibleColumns();
        ResumeInteraction();
        ConnectSource();
        UpdateColumnSubscriptions();
        RowsPresenter?.SetOwner(this, Source);
        _headers?.RefreshColumns();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        ColumnOperationVersion++;
        CancelColumnReorder();
        CancelRowDrag();
        _attached = false;
        SuspendInteraction();
        DisconnectSource();
        UpdateColumnSubscriptions();
        RowsPresenter?.SetOwner(this, null);
        base.OnDetachedFromVisualTree(e);
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty || change.Property == FrozenColumnCountProperty || change.Property == TemplateProperty)
        { ColumnOperationVersion++; CancelColumnReorder(); CancelRowDrag(); }
        OnInteractionPropertyChanged(change);
        OnSortingPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            DisconnectSource();
            ConnectSource();
            RowsPresenter?.SetOwner(this, _attached ? Source : null);
            UpdateEmpty();
            NotifyAutomationStateChanged();
        }
        else if (change.Property == RowHeightProperty || change.Property == RowHeightSelectorProperty || change.Property == FrozenColumnCountProperty || change.Property == AutoRowHeightProperty || change.Property == CellThemeProperty || change.Property == FontSizeProperty || change.Property == FontFamilyProperty || change.Property == FontWeightProperty)
            RowsPresenter?.Refresh(resetMeasurements: true);
        else if (change.Property == CanUserResizeColumnsProperty || change.Property == ColumnHeaderThemeProperty)
        { _headers?.RefreshColumns(); if (change.Property == ColumnHeaderThemeProperty) RowsPresenter?.Refresh(resetMeasurements: true); }
    }
    internal void InvalidateHeaders() { _headers?.InvalidateMeasure(); _headers?.InvalidateArrange(); }
    internal void NotifyRowPrepared(TreeDataGridRowPresenter row) => RowPrepared?.Invoke(this, new TreeDataGridRowEventArgs(row, row.Row!, row.RowIndex));
    internal void NotifyRowClearing(TreeDataGridRowPresenter row) => RowClearing?.Invoke(this, new TreeDataGridRowEventArgs(row, row.Row!, row.RowIndex));
    private static T RequirePart<T>(TemplateAppliedEventArgs e, string name) where T : class =>
        e.NameScope.Find<T>(name) ?? throw new InvalidOperationException($"TreeDataGrid template requires {name} of type {typeof(T).Name}.");
    private void ConnectSource()
    {
        if (_attached && Source is { } source)
        {
            _subscribedSource = source;
            source.ProjectionChanged += SourceChanged;
            source.RefreshResumed += SourceRefreshResumed;
        }
        UpdateEmpty();
    }
    private void DisconnectSource()
    {
        if (_subscribedSource is { } source)
        {
            source.ProjectionChanged -= SourceChanged;
            source.RefreshResumed -= SourceRefreshResumed;
        }
        _subscribedSource = null;
    }
    private void SourceChanged(object? sender, ProjectionChangedEventArgs e) { UpdateEmpty(); RowsPresenter?.Refresh(); NotifyAutomationStateChanged(); }
    private void SourceRefreshResumed(object? sender, EventArgs e)
    {
        if (_attached && ReferenceEquals(sender, _subscribedSource) && ReferenceEquals(sender, Source)) UpdateEditCommands();
    }
    private void UpdateEmpty() => PseudoClasses.Set(":empty", Source is null || Source.Rows.Count == 0);
    private void ColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ColumnOperationVersion++;
        CancelColumnReorder();
        CancelRowDrag();
        RefreshVisibleColumns();
        ItemsSourceColumnsChanged();
        SynchronizeStateColumns();
        UpdateColumnSubscriptions();
        ColumnsInvalidated();
    }
    private void ColumnChanged(object? sender, PropertyChangedEventArgs e)
    {
        ColumnOperationVersion++;
        CancelColumnReorder();
        CancelRowDrag();
        RefreshVisibleColumns();
        if (_state?.EditSession is { } edit && Columns.FirstOrDefault(column => column.Id == edit.Cell.Column) is { } active && (active.IsReadOnly || active.EffectiveColumn.IsReadOnly)) CancelEdit();
        UpdateColumnSubscriptions(); ColumnsInvalidated(e.PropertyName != nameof(TreeDataGridColumn.IsVisible)); UpdateEditCommands();
    }
    private void ColumnsInvalidated(bool resetMeasurements = true) { _headers?.RefreshColumns(); RowsPresenter?.Refresh(resetMeasurements: resetMeasurements); NotifyAutomationStateChanged(); }
    private void UpdateColumnSubscriptions()
    {
        foreach (var column in _subscribedColumns) column.PropertyChanged -= ColumnChanged;
        _subscribedColumns.Clear();
        if (_attached)
            foreach (var column in Columns)
            {
                if (_subscribedColumns.Add(column)) column.PropertyChanged += ColumnChanged;
                if (column is TreeDataGridHierarchicalExpanderColumn tree && _subscribedColumns.Add(tree.InnerColumn)) tree.InnerColumn.PropertyChanged += ColumnChanged;
            }
    }

    private sealed class ColumnCollection : ObservableCollection<TreeDataGridColumn>
    {
        protected override void InsertItem(int index, TreeDataGridColumn item) { Validate(item, -1); base.InsertItem(index, item); }
        protected override void SetItem(int index, TreeDataGridColumn item) { Validate(item, index); base.SetItem(index, item); }
        private void Validate(TreeDataGridColumn item, int replacing)
        {
            ArgumentNullException.ThrowIfNull(item);
            for (var i = 0; i < Count; ++i)
                if (i != replacing && this[i].Id == item.Id) throw new ArgumentException("A TreeDataGrid column instance can occur only once in Columns.", nameof(item));
        }
    }
}
