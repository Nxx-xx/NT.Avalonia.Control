using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace NT.AvaloniaControl.TreeDataGrid;

public enum TreeDataGridRowDragHandle { RowHeader, WholeRow }

/// <summary>A same-control Move gesture. Cancel or Handled ends default handling; Copy and Link are never performed.</summary>
public sealed class TreeDataGridRowDragEventArgs : RoutedEventArgs
{
    internal TreeDataGridRowDragEventArgs(RoutedEvent routedEvent, TreeDataGridSource source,
        IReadOnlyList<TreeDataGridRow> rows, TreeDataGridRow? target, TreeDataGridRowDropPosition position, long version) : base(routedEvent)
    { DataSource = source; Rows = Array.AsReadOnly(rows.ToArray()); Target = target; Position = position; SourceVersion = version; }
    public TreeDataGridSource DataSource { get; }
    public IReadOnlyList<TreeDataGridRow> Rows { get; }
    public TreeDataGridRow? Target { get; }
    public TreeDataGridRowDropPosition Position { get; }
    public long SourceVersion { get; }
    public DragDropEffects AllowedEffects { get; set; } = DragDropEffects.Move;
    public bool Cancel { get; set; }
}

public partial class TreeDataGrid
{
    private bool _raisingRowDragEvent;
    public static readonly StyledProperty<TreeDataGridRowDragHandle> RowDragHandleProperty =
        AvaloniaProperty.Register<TreeDataGrid, TreeDataGridRowDragHandle>(nameof(RowDragHandle));
    public TreeDataGridRowDragHandle RowDragHandle { get => GetValue(RowDragHandleProperty); set => SetValue(RowDragHandleProperty, value); }
    public static readonly RoutedEvent<TreeDataGridRowDragEventArgs> RowDragStartedEvent =
        RoutedEvent.Register<TreeDataGrid, TreeDataGridRowDragEventArgs>(nameof(RowDragStarted), RoutingStrategies.Bubble);
    public static readonly RoutedEvent<TreeDataGridRowDragEventArgs> RowDragOverEvent =
        RoutedEvent.Register<TreeDataGrid, TreeDataGridRowDragEventArgs>(nameof(RowDragOver), RoutingStrategies.Bubble);
    public static readonly RoutedEvent<TreeDataGridRowDragEventArgs> RowDropEvent =
        RoutedEvent.Register<TreeDataGrid, TreeDataGridRowDragEventArgs>(nameof(RowDrop), RoutingStrategies.Bubble);
    public event EventHandler<TreeDataGridRowDragEventArgs>? RowDragStarted { add => AddHandler(RowDragStartedEvent, value); remove => RemoveHandler(RowDragStartedEvent, value); }
    public event EventHandler<TreeDataGridRowDragEventArgs>? RowDragOver { add => AddHandler(RowDragOverEvent, value); remove => RemoveHandler(RowDragOverEvent, value); }
    public event EventHandler<TreeDataGridRowDragEventArgs>? RowDrop { add => AddHandler(RowDropEvent, value); remove => RemoveHandler(RowDropEvent, value); }

    private bool RaiseRowDrag(RowPointerDrag drag, RoutedEvent<TreeDataGridRowDragEventArgs> routedEvent, TreeDataGridRow? target = null,
        TreeDataGridRowDropPosition position = TreeDataGridRowDropPosition.Before)
    {
        var args = new TreeDataGridRowDragEventArgs(routedEvent, drag.Source, drag.Rows, target, position, drag.Source.Version);
        if (routedEvent != RowDragStartedEvent && target is null) args.AllowedEffects = DragDropEffects.None;
        if (_raisingRowDragEvent) return false;
        _raisingRowDragEvent = true;
        try { RaiseEvent(args); }
        catch { CancelRowDrag(); throw; }
        finally { _raisingRowDragEvent = false; }
        return !args.Cancel && !args.Handled && (args.AllowedEffects & DragDropEffects.Move) != 0;
    }
}
