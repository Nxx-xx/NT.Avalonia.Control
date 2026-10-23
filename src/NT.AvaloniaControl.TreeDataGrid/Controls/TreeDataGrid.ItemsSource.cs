using System.Collections;
using System.ComponentModel;
using Avalonia;

namespace NT.AvaloniaControl.TreeDataGrid;

public partial class TreeDataGrid
{
    public static readonly DirectProperty<TreeDataGrid, IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.RegisterDirect<TreeDataGrid, IEnumerable?>(nameof(ItemsSource), grid => grid.ItemsSource,
            (grid, value) => grid.ItemsSource = value);
    private IEnumerable? _itemsSource;
    private ItemsSourceAdapter? _itemsAdapter;
    private readonly HashSet<TreeDataGridHierarchicalExpanderColumn> _itemsHierarchyColumns = [];
    private bool _changingItemsSource;

    /// <summary>
    /// Model collection adapted into Source automatically. Clear this property to release the adapter.
    /// It is mutually exclusive with an explicitly assigned Source.
    /// </summary>
    public IEnumerable? ItemsSource
    {
        get => _itemsSource;
        set
        {
            VerifyAccess();
            if (_changingItemsSource) throw new InvalidOperationException("ItemsSource cannot be changed from its source-change callbacks.");
            if (ReferenceEquals(value, _itemsSource)) return;
            if (value is not null && Source is not null && !ReferenceEquals(Source, _itemsAdapter?.Source))
                throw new InvalidOperationException("ItemsSource and an explicitly assigned Source are mutually exclusive. Clear Source first.");
            _changingItemsSource = true;
            try
            {
                var candidate = value is null ? null : CreateItemsAdapter(value);
                var previous = _itemsSource;
                _itemsSource = value;
                try { InstallItemsAdapter(candidate); }
                finally
                {
                    UpdateItemsHierarchySubscriptions();
                    RaisePropertyChanged(ItemsSourceProperty, previous, value);
                }
            }
            finally { _changingItemsSource = false; }
        }
    }

    private void ValidateExplicitSource()
    {
        if (_itemsSource is not null)
            throw new InvalidOperationException("Source is managed by ItemsSource. Clear ItemsSource before assigning Source.");
    }

    private static TreeDataGridSource? CoerceSource(AvaloniaObject owner, TreeDataGridSource? value)
    {
        var grid = (TreeDataGrid)owner;
        if (grid._itemsSource is not null)
        {
            // Avalonia re-coerces the default null during style changes. Keep the owned source there;
            // an actual external source is still rejected before it can replace the adapter.
            if (value is null || ReferenceEquals(value, grid._itemsAdapter?.Source)) return grid._itemsAdapter?.Source;
            grid.ValidateExplicitSource();
        }
        return value;
    }

    private ItemsSourceAdapter CreateItemsAdapter(IEnumerable items)
        => new(items, ConfiguredHierarchy());

    private TreeDataGridHierarchicalExpanderColumn? ConfiguredHierarchy()
    {
        var configured = Columns.OfType<TreeDataGridHierarchicalExpanderColumn>()
            .Where(column => column.ChildrenBinding is not null || column.HasChildrenBinding is not null || column.IsExpandedBinding is not null).ToArray();
        if (configured.Length > 1)
            throw new InvalidOperationException("Only one expander column can define ItemsSource hierarchy bindings.");
        return configured.FirstOrDefault();
    }

    private void InstallItemsAdapter(ItemsSourceAdapter? adapter)
    {
        var previous = _itemsAdapter;
        var previousState = previous is not null && ReferenceEquals(_stateSource, previous.Source) ? _state : null;
        _itemsAdapter = adapter;
        try
        {
            // Preserve any existing Avalonia binding expression; this only changes its current value.
            SetCurrentValue(SourceProperty, adapter?.Source);
        }
        finally
        {
            if (previous is not null)
            {
                if (previousState is not null) previousState.DisposeOwnedSourceWhenSafe(previous.Dispose);
                else previous.Dispose();
            }
        }
    }

    private void UpdateItemsHierarchySubscriptions()
    {
        foreach (var column in _itemsHierarchyColumns) column.PropertyChanged -= ItemsHierarchyChanged;
        _itemsHierarchyColumns.Clear();
        if (_itemsSource is null) return;
        foreach (var column in Columns.OfType<TreeDataGridHierarchicalExpanderColumn>())
            if (_itemsHierarchyColumns.Add(column)) column.PropertyChanged += ItemsHierarchyChanged;
    }

    private void ItemsSourceColumnsChanged()
    {
        UpdateItemsHierarchySubscriptions();
        if (_itemsSource is not null) RebuildItemsAdapter();
    }

    private void ItemsHierarchyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(TreeDataGridHierarchicalExpanderColumn.ChildrenBinding)
            or nameof(TreeDataGridHierarchicalExpanderColumn.HasChildrenBinding)
            or nameof(TreeDataGridHierarchicalExpanderColumn.IsExpandedBinding)) RebuildItemsAdapter();
    }

    private void RebuildItemsAdapter()
    {
        if (_itemsSource is null) return;
        if (_itemsAdapter?.Matches(ConfiguredHierarchy()) == true) return;
        if (_changingItemsSource) throw new InvalidOperationException("Hierarchy configuration cannot change during an ItemsSource update.");
        _changingItemsSource = true;
        try { InstallItemsAdapter(CreateItemsAdapter(_itemsSource)); }
        finally { _changingItemsSource = false; }
    }
}
