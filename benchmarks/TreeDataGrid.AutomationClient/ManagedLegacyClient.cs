using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Automation;
using TreeDataGridNativeBenchmarks;


internal static class ManagedLegacyClient
{
    internal static void Run(string[] args)
    {
// The client is a separate MTA process. Only the Windows UI Automation bridge
// accesses the host; no Avalonia or product assemblies are loaded here.
var workloadIndex = Array.IndexOf(args, "--workload");
ManagedListener.Workload = workloadIndex < 0 ? "both" : args[workloadIndex + 1];
if (ManagedListener.Workload is not ("query" or "events" or "both")) throw new ArgumentException("Invalid UIA workload.");
while (Console.ReadLine() is { } line)
{
    var request = JsonSerializer.Deserialize<AutomationRequest>(line)!;
    var start = Stopwatch.GetTimestamp();
    AutomationReply reply;
    try
    {
        var items = Query(request, out var found);
        reply = new(request.Id, Environment.ProcessId, request.HostPid, request.GridId, request.Retired,
            found, AutomationProtocol.Hash(items), items, start, Stopwatch.GetTimestamp(), null, ManagedListener.StructureEvents, ManagedListener.PropertyEvents, ManagedListener.AttachedGridId,
            ManagedListener.Workload, GC.GetTotalMemory(false), GC.CollectionCount(2));
    }
    catch (Exception error)
    {
        reply = new(request.Id, Environment.ProcessId, request.HostPid, request.GridId, request.Retired,
            false, "", [], start, Stopwatch.GetTimestamp(), error.ToString(), ManagedListener.StructureEvents, ManagedListener.PropertyEvents, ManagedListener.AttachedGridId,
            ManagedListener.Workload, GC.GetTotalMemory(false), GC.CollectionCount(2));
    }
    Console.WriteLine(JsonSerializer.Serialize(reply));
}
// AutomationElement exposes no Dispose. Release this client's abandoned UIA
// SafeHandles before process exit; .NET does not run finalizers at shutdown.
// The host closes stdin only after the measured workload has ended.
Automation.RemoveAllEventHandlers();
GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
Console.WriteLine(JsonSerializer.Serialize(new { Shutdown = true, ClientPid = Environment.ProcessId, ManagedHandleDrain = true,
    EventHandlersRemoved = true, ManagedListener.StructureEvents, ManagedListener.PropertyEvents }));

[MethodImpl(MethodImplOptions.NoInlining)]
static AutomationItem[] Query(AutomationRequest request, out bool found)
{
    var window = AutomationElement.FromHandle((nint)request.Handle);
    if (window.Current.ProcessId != request.HostPid) throw new InvalidOperationException("Wrong UIA window process.");
    if (request.Retired) ManagedListener.End();
    else if (!request.ReadTree)
    {
        found = ManagedListener.AttachedGridId == request.GridId;
        return [];
    }
    var cache = new CacheRequest { TreeScope = TreeScope.Element | TreeScope.Descendants, TreeFilter = Automation.ControlViewCondition };
    cache.Add(AutomationElement.AutomationIdProperty); cache.Add(AutomationElement.NameProperty);
    cache.Add(AutomationElement.ProcessIdProperty); cache.Add(AutomationElement.ControlTypeProperty);
    AutomationElement? grid;
    grid = window.FindFirst(TreeScope.Descendants,
        new PropertyCondition(AutomationElement.AutomationIdProperty, request.GridId));
    found = grid is not null;
    if (grid is null) return [];
    if (ManagedListener.Workload != "query") ManagedListener.Ensure(grid, request.GridId);
    grid = grid.GetUpdatedCache(cache);
    if (grid.Cached.ProcessId != request.HostPid || grid.Cached.ControlType != ControlType.DataGrid)
        throw new InvalidOperationException("UIA target is not the requested product grid.");
    var result = new List<AutomationItem>();
    Visit(grid);
    return result.OrderBy(i => i.Id, StringComparer.Ordinal).ToArray();

    void Visit(AutomationElement element)
    {
        var id = element.Cached.AutomationId;
        if (id.StartsWith("TreeDataGrid.Row.", StringComparison.Ordinal) || id.StartsWith("TreeDataGrid.Cell.", StringComparison.Ordinal)
            || id.StartsWith("TreeDataGrid.Column.", StringComparison.Ordinal))
        {
            // Avalonia exposes these through pattern providers, rather than the
            // element GetPropertyValue path used by a Windows cache request.
            var value = element.TryGetCurrentPattern(ValuePattern.Pattern, out var v) ? ((ValuePattern)v).Current.Value : null;
            bool? selected = element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var s) ? ((SelectionItemPattern)s).Current.IsSelected : null;
            result.Add(new(id, element.Cached.Name, value, selected));
        }
        foreach (AutomationElement child in element.CachedChildren) Visit(child);
    }
}


    }
}
internal static class ManagedListener
{
    internal static string Workload = "both";
    private static string? _attached;
    private static long _structure, _properties;
    internal static string? AttachedGridId => _attached;
    internal static void End() { Automation.RemoveAllEventHandlers(); _attached = null; }
    internal static long StructureEvents => Interlocked.Read(ref _structure);
    internal static long PropertyEvents => Interlocked.Read(ref _properties);
    internal static void Ensure(AutomationElement grid, string gridId)
    {
        if (_attached == gridId) return;
        Automation.RemoveAllEventHandlers();
        Automation.AddStructureChangedEventHandler(grid, TreeScope.Subtree, (_, _) => Interlocked.Increment(ref _structure));
        Automation.AddAutomationPropertyChangedEventHandler(grid, TreeScope.Subtree, (_, _) => Interlocked.Increment(ref _properties),
            AutomationElement.NameProperty, AutomationElement.ItemStatusProperty, SelectionItemPattern.IsSelectedProperty, ValuePattern.ValueProperty);
        _attached = gridId;
    }
}
