using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using TreeDataGridNativeBenchmarks;

var backendIndex = Array.IndexOf(args, "--client-backend");
var backend = backendIndex < 0 ? "explicit-com" : args[backendIndex + 1];
if (backend == "managed-legacy") { ManagedLegacyClient.Run(args); return; }
if (backend != "explicit-com") throw new ArgumentException("Invalid UIA client backend.");
if (args.Contains("--self-test-ownership")) { OwnershipSelfTests.Run(); return; }
if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64) throw new PlatformNotSupportedException("Windows x64 only.");
var index = Array.IndexOf(args, "--workload");
var workload = index < 0 ? "both" : args[index + 1];
if (workload is not ("query" or "events" or "both")) throw new ArgumentException("Invalid workload.");
NativeUia.Check(NativeUia.CoInitializeEx(0, 0));
try
{
using (var client = new ExplicitUiaClient(workload))
{
    while (Console.ReadLine() is { } line)
    {
        var request = JsonSerializer.Deserialize<AutomationRequest>(line)!;
        var start = Stopwatch.GetTimestamp();
        AutomationReply reply;
        try
        {
            var items = client.Query(request, out var found);
            reply = new(request.Id, Environment.ProcessId, request.HostPid, request.GridId, request.Retired, found,
                AutomationProtocol.Hash(items), items, start, Stopwatch.GetTimestamp(), null, NativeUia.StructureEvents, NativeUia.PropertyEvents, client.Attached,
                workload, GC.GetTotalMemory(false), GC.CollectionCount(2));
        }
        catch (Exception error)
        {
            reply = new(request.Id, Environment.ProcessId, request.HostPid, request.GridId, request.Retired, false, "", [], start, Stopwatch.GetTimestamp(), error.ToString(),
                NativeUia.StructureEvents, NativeUia.PropertyEvents, client.Attached, workload, GC.GetTotalMemory(false), GC.CollectionCount(2));
        }
        Console.WriteLine(JsonSerializer.Serialize(reply with { Ownership = OwnedUiaReference.Receipt() }));
    }
}
}
finally { NativeUia.CoUninitialize(); }
Console.WriteLine(JsonSerializer.Serialize(new { Shutdown = true, ClientPid = Environment.ProcessId, EventHandlersRemoved = true,
    ManagedHandleDrain = false, ExplicitComReferenceDrain = true, NativeUia.StructureEvents, NativeUia.PropertyEvents, Ownership = OwnedUiaReference.Receipt() }));

