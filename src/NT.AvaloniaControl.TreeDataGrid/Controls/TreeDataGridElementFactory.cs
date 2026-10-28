using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using System.Runtime.CompilerServices;

namespace NT.AvaloniaControl.TreeDataGrid;

public enum TreeDataGridElementKind { Row, Cell, ColumnHeader }

/// <summary>An immutable snapshot for one container lifetime. Recycling receives this snapshot after the container is cleared.</summary>
public sealed class TreeDataGridElementContext
{
    internal TreeDataGridElementContext(TreeDataGrid owner, Control element, TreeDataGridElementKind kind,
        TreeDataGridSource? source, TreeDataGridRow? row, int rowIndex, TreeDataGridColumn? column, int columnIndex)
    { Owner = owner; Element = element; Kind = kind; Source = source; Row = row; RowIndex = rowIndex; Column = column; ColumnIndex = columnIndex; }
    public TreeDataGrid Owner { get; }
    public Control Element { get; }
    public TreeDataGridElementKind Kind { get; }
    public TreeDataGridSource? Source { get; }
    public TreeDataGridRow? Row { get; }
    public int RowIndex { get; }
    public TreeDataGridColumn? Column { get; }
    public int ColumnIndex { get; }
}

/// <summary>Creates standard, compatible containers and decorates their lifecycle. Pools belong to a grid and a factory session.</summary>
public class TreeDataGridElementFactory
{
    public virtual Control CreateElement(TreeDataGrid owner, TreeDataGridElementKind kind) => kind switch
    {
        TreeDataGridElementKind.Row => new TreeDataGridRowPresenter(),
        TreeDataGridElementKind.Cell => new TreeDataGridCellPresenter(),
        TreeDataGridElementKind.ColumnHeader => new TreeDataGridColumnHeader(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
    public virtual void PrepareElement(TreeDataGridElementContext context) { }
    public virtual void ClearElement(TreeDataGridElementContext context) { }
    /// <summary>Called after built-in cleanup and detachment. Return false to discard instead of pooling.</summary>
    public virtual bool RecycleElement(TreeDataGridElementContext context) => true;
    /// <summary>Called once when a created element leaves the grid's pools permanently; the element is cleared and detached.</summary>
    public virtual void ReleaseElement(TreeDataGrid owner, TreeDataGridElementKind kind, Control element) { }
}

public partial class TreeDataGrid
{
    public static readonly StyledProperty<TreeDataGridElementFactory?> ElementFactoryProperty =
        AvaloniaProperty.Register<TreeDataGrid, TreeDataGridElementFactory?>(nameof(ElementFactory));
    public TreeDataGridElementFactory? ElementFactory { get => GetValue(ElementFactoryProperty); set => SetValue(ElementFactoryProperty, value); }
    private ElementFactorySession? _elementFactorySession;
    private long _elementFactoryGeneration;
    internal ElementFactorySession FactorySession => _elementFactorySession ??= new(this, ElementFactory ?? DefaultElementFactory);
    private static readonly TreeDataGridElementFactory DefaultElementFactory = new();
    internal bool IsFactoryCurrent(ElementFactorySession session) => ReferenceEquals(_elementFactorySession, session);

    private void ChangeElementFactory()
    {
        var generation = ++_elementFactoryGeneration;
        _elementFactorySession?.Retire();
        _elementFactorySession = new(this, ElementFactory ?? DefaultElementFactory);
        ColumnOperationVersion++; CancelColumnReorder(); CancelRowDrag();
        List<Exception>? errors = null;
        try { RowsPresenter?.ResetFactory(); } catch (Exception error) { (errors ??= []).Add(error); }
        if (generation == _elementFactoryGeneration)
            try { _headers?.ResetFactory(); } catch (Exception error) { (errors ??= []).Add(error); }
        NotifyAutomationStateChanged();
        TreeDataGridEventDispatch.Throw(errors);
    }
}

internal sealed class ElementFactorySession(TreeDataGrid owner, TreeDataGridElementFactory factory)
{
    private readonly bool _defaultHooks = factory.GetType() == typeof(TreeDataGridElementFactory);
    private sealed class Registration(ElementFactorySession session, TreeDataGridElementKind kind)
    {
        internal readonly WeakReference<ElementFactorySession> Session = new(session);
        internal readonly TreeDataGridElementKind Kind = kind;
        internal TreeDataGridElementContext? Context;
        internal TreeDataGridElementContext? ClearedContext;
        internal bool Released;
        internal bool Preparing, PendingClear, PendingRecycle;
    }
    private static readonly ConditionalWeakTable<Control, Registration> Registrations = new();
    private bool _retired;
    internal bool IsCurrent => !_retired && owner.IsFactoryCurrent(this);
    // Retained automatic-height cells explicitly invalidate their template/content
    // measurement chain before the next row-height observation.
    internal bool CanRetainHosts => _defaultHooks && IsCurrent;
    internal void Retire() => _retired = true;
    internal T Create<T>(TreeDataGridElementKind kind) where T : Control
    {
        var element = factory.CreateElement(owner, kind);
        if (element is not T typed) throw new InvalidOperationException($"ElementFactory must return {typeof(T).Name} for {kind}; received {element?.GetType().Name ?? "null"}.");
        if (Registrations.TryGetValue(element, out _)) throw new InvalidOperationException("ElementFactory returned an element already issued to a grid/factory session. Return a fresh container; the grid owns recycling.");
        if (element.Parent is not null || element.GetVisualParent() is not null || element.DataContext is not null)
            throw new InvalidOperationException("ElementFactory must return an unparented container with no DataContext.");
        Registrations.Add(element, new(this, kind));
        return typed;
    }
    private Registration Get(Control element)
    {
        if (!Registrations.TryGetValue(element, out var registration) || !registration.Session.TryGetTarget(out var session)
            || !ReferenceEquals(session, this) || registration.Released)
            throw new InvalidOperationException("The element belongs to another grid/factory session or has been released.");
        return registration;
    }
    internal bool Prepare(Control element, TreeDataGridSource? source, TreeDataGridRow? row, int rowIndex, TreeDataGridColumn? column, int columnIndex)
    {
        if (_defaultHooks) return IsCurrent;
        var registration = Get(element);
        if (registration.Context is not null || registration.ClearedContext is not null) throw new InvalidOperationException("An element cannot be prepared before its old lifetime is recycled.");
        var context = new TreeDataGridElementContext(owner, element, registration.Kind, source, row, rowIndex, column, columnIndex);
        registration.Context = context;
        registration.Preparing = true;
        List<Exception>? errors = null;
        try { factory.PrepareElement(context); } catch (Exception error) { (errors ??= []).Add(error); }
        finally { registration.Preparing = false; }
        // A prepare callback may replace the source/factory and continue installing its own subscriptions.
        // Do not reuse or release that container until the callback has returned and its cleanup can finish.
        if (registration.PendingClear)
        {
            registration.PendingClear = false;
            try { factory.ClearElement(context); } catch (Exception error) { (errors ??= []).Add(error); }
        }
        if (registration.PendingRecycle)
        {
            registration.PendingRecycle = false;
            try { Recycle(element, canPool: false); } catch (Exception error) { (errors ??= []).Add(error); }
        }
        TreeDataGridEventDispatch.Throw(errors);
        return IsCurrent && ReferenceEquals(registration.Context, context);
    }
    internal void Clear(Control element)
    {
        if (_defaultHooks) return;
        var registration = Get(element);
        if (registration.Context is not { } context) return;
        registration.Context = null; registration.ClearedContext = context;
        if (registration.Preparing) { registration.PendingClear = true; return; }
        factory.ClearElement(context);
    }
    internal bool Recycle(Control element, bool canPool)
    {
        var registration = Get(element);
        if (registration.Preparing) { registration.PendingRecycle = true; return false; }
        var context = registration.ClearedContext; registration.ClearedContext = null;
        try
        {
            if (context is not null) canPool &= factory.RecycleElement(context);
            if (canPool && IsCurrent) return true;
        }
        catch (Exception error)
        {
            List<Exception>? errors = [error];
            try { Discard(element); } catch (Exception releaseError) { errors.Add(releaseError); }
            TreeDataGridEventDispatch.Throw(errors);
        }
        Discard(element); return false;
    }
    internal void Discard(Control element)
    {
        var registration = Get(element);
        registration.Released = true; registration.Context = registration.ClearedContext = null;
        List<Exception>? errors = null;
        try
        {
            if (element is TreeDataGridRowPresenter row)
                try { row.ReleaseElements(); } catch (Exception error) { (errors ??= []).Add(error); }
            factory.ReleaseElement(owner, registration.Kind, element);
        }
        catch (Exception error) { (errors ??= []).Add(error); }
        finally
        {
            if (element is TreeDataGridRowPresenter row) row.DetachFactory();
            if (element is TreeDataGridCellPresenter cell) cell.DetachFactory();
            if (element is TreeDataGridColumnHeader header) header.DetachFactory();
        }
        TreeDataGridEventDispatch.Throw(errors);
    }
}
