using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using TreeDataGridNativeBenchmarks;


// Vtable slots/IIDs follow Windows SDK 10.0.26100.0 UIAutomationClient.idl.
// No RCWs or AutomationElement SafeHandles are created. Every returned native
// interface has exactly one owned reference, disposed at its lexical boundary.
sealed unsafe class ExplicitUiaClient : IDisposable
{
    private readonly string _workload;
    private readonly OwnedUiaReference _automation;
    private readonly OwnedUiaReference _structure, _property;
    internal string? Attached { get; private set; }
    internal ExplicitUiaClient(string workload)
    {
        _workload = workload;
        Guid clsid = new("ff48dba4-60ef-4201-aa87-54103eef594e"), iid = new("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee");
        var hr = NativeUia.CoCreateInstance(ref clsid, 0, 1, ref iid, out var p);
        _automation = OwnedUiaReference.Adopt(hr, p, lifetime: true);
        try { _structure = NativeUia.Handler(false); _property = NativeUia.Handler(true); }
        catch { _structure?.Dispose(); _automation.Dispose(); throw; }
    }
    internal void End()
    {
        NativeUia.Check(((delegate* unmanaged[Stdcall]<nint, int>)NativeUia.Slot(_automation.P, 41))(_automation.P)); Attached = null;
    }
    private void Subscribe(nint grid, string id)
    {
        if (_workload == "query" || Attached == id) return;
        End();
        NativeUia.Check(((delegate* unmanaged[Stdcall]<nint, nint, int, nint, nint, int>)NativeUia.Slot(_automation.P, 37))(_automation.P, grid, 7, 0, _structure.P));
        int* properties = stackalloc int[] { 30005, 30026, 30079, 30045 };
        NativeUia.Check(((delegate* unmanaged[Stdcall]<nint, nint, int, nint, nint, int*, int, int>)NativeUia.Slot(_automation.P, 34))(_automation.P, grid, 7, 0, _property.P, properties, 4));
        Attached = id;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal AutomationItem[] Query(AutomationRequest request, out bool found)
    {
        nint windowPointer = 0;
        var hr = ((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)NativeUia.Slot(_automation.P, 6))(_automation.P, (nint)request.Handle, &windowPointer);
        using var window = OwnedUiaReference.Adopt(hr, windowPointer);
        if ((int)NativeUia.Property(window.P, 30002, false)! != request.HostPid) throw new InvalidOperationException("Wrong window process.");
        if (request.Retired) End();
        else if (!request.ReadTree) { found = Attached == request.GridId; return []; }
        using var cache = NativeUia.GetInterface(_automation.P, 20);
        foreach (var property in new[] { 30011, 30005, 30002, 30003 })
            NativeUia.Check(((delegate* unmanaged[Stdcall]<nint, int, int>)NativeUia.Slot(cache.P, 3))(cache.P, property));
        NativeUia.Check(((delegate* unmanaged[Stdcall]<nint, int, int>)NativeUia.Slot(cache.P, 7))(cache.P, 5));
        NativeUia.Check(((delegate* unmanaged[Stdcall]<nint, int, int>)NativeUia.Slot(cache.P, 11))(cache.P, 1)); // SDK AutomationElementMode_Full: live patterns remain available.
        using var filter = NativeUia.GetInterface(_automation.P, 18);
        NativeUia.Check(((delegate* unmanaged[Stdcall]<nint, nint, int>)NativeUia.Slot(cache.P, 9))(cache.P, filter.P));
        var value = new NativeUia.Variant { Type = 8, Pointer = Marshal.StringToBSTR(request.GridId) };
        nint conditionPointer = 0;
        try { hr = ((delegate* unmanaged[Stdcall]<nint, int, NativeUia.Variant, nint*, int>)NativeUia.Slot(_automation.P, 23))(_automation.P, 30011, value, &conditionPointer); }
        finally { NativeUia.VariantClear(&value); }
        using var condition = OwnedUiaReference.Adopt(hr, conditionPointer);
        nint gridPointer = 0;
        hr = ((delegate* unmanaged[Stdcall]<nint, int, nint, nint*, int>)NativeUia.Slot(window.P, 5))(window.P, 4, condition.P, &gridPointer);
        using var grid = OwnedUiaReference.Adopt(hr, gridPointer);
        found = grid.P != 0;
        if (!found) return [];
        Subscribe(grid.P, request.GridId);
        nint cachedPointer = 0;
        hr = ((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)NativeUia.Slot(grid.P, 9))(grid.P, cache.P, &cachedPointer);
        using var cached = OwnedUiaReference.Adopt(hr, cachedPointer);
        if ((int)NativeUia.Property(cached.P, 30002, true)! != request.HostPid || (int)NativeUia.Property(cached.P, 30003, true)! != 50028)
            throw new InvalidOperationException("Wrong cached grid.");
        var items = new List<AutomationItem>(); Visit(cached.P, items);
        return items.OrderBy(i => i.Id, StringComparer.Ordinal).ToArray();
    }
    private static void Visit(nint element, List<AutomationItem> result)
    {
        var id = (string?)NativeUia.Property(element, 30011, true) ?? "";
        if (id.StartsWith("TreeDataGrid.Row.", StringComparison.Ordinal) || id.StartsWith("TreeDataGrid.Cell.", StringComparison.Ordinal) || id.StartsWith("TreeDataGrid.Column.", StringComparison.Ordinal))
        {
            using var valuePattern = NativeUia.Pattern(element, 10002, new("a94cd8b1-0844-4cd6-9d2d-640537ab39e9"));
            using var selection = NativeUia.Pattern(element, 10010, new("a8efa66a-0fda-421a-9194-38021f3578ea"));
            string? value = null; bool? selected = null;
            if (valuePattern.P != 0)
            {
                nint text = 0;
                try { NativeUia.Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)NativeUia.Slot(valuePattern.P, 4))(valuePattern.P, &text)); value = text == 0 ? "" : Marshal.PtrToStringBSTR(text); }
                finally { if (text != 0) Marshal.FreeBSTR(text); }
            }
            if (selection.P != 0) { int state = 0; NativeUia.Check(((delegate* unmanaged[Stdcall]<nint, int*, int>)NativeUia.Slot(selection.P, 6))(selection.P, &state)); selected = state != 0; }
            result.Add(new(id, (string?)NativeUia.Property(element, 30005, true) ?? "", value, selected));
        }
        using var children = NativeUia.GetInterface(element, 19);
        if (children.P == 0) return;
        int length = 0; NativeUia.Check(((delegate* unmanaged[Stdcall]<nint, int*, int>)NativeUia.Slot(children.P, 3))(children.P, &length));
        for (var i = 0; i < length; i++)
        {
            nint childPointer = 0;
            var hr = ((delegate* unmanaged[Stdcall]<nint, int, nint*, int>)NativeUia.Slot(children.P, 4))(children.P, i, &childPointer);
            using var child = OwnedUiaReference.Adopt(hr, childPointer); Visit(child.P, result);
        }
    }
    public void Dispose() { try { End(); } finally { _automation.Dispose(); _property.Dispose(); _structure.Dispose(); } }
}

sealed unsafe class OwnedUiaReference : IDisposable
{
    private nint _pointer;
    private readonly bool _lifetime;
    private static long _acquired, _released, _live, _finalizers, _clientLive;
    internal nint P => _pointer;
    internal OwnedUiaReference(nint pointer, bool lifetime = false)
    {
        _pointer = pointer; _lifetime = lifetime;
        if (pointer == 0) return;
        if (lifetime) Interlocked.Increment(ref _clientLive);
        else { Interlocked.Increment(ref _acquired); Interlocked.Increment(ref _live); }
    }
    internal static OwnedUiaReference Adopt(int hr, nint pointer, bool lifetime = false)
    {
        var owned = new OwnedUiaReference(pointer, lifetime);
        if (hr >= 0) return owned;
        owned.Dispose(); NativeUia.Check(hr); return owned;
    }
    public void Dispose()
    {
        var p = Interlocked.Exchange(ref _pointer, 0);
        if (p != 0)
        {
            ((delegate* unmanaged[Stdcall]<nint, uint>)NativeUia.Slot(p, 2))(p);
            if (_lifetime) Interlocked.Decrement(ref _clientLive);
            else { Interlocked.Increment(ref _released); Interlocked.Decrement(ref _live); }
        }
        GC.SuppressFinalize(this);
    }
    ~OwnedUiaReference() { if (_pointer == 0) return; Interlocked.Increment(ref _finalizers); Dispose(); }
    internal static AutomationOwnershipReceipt Receipt() => new(1, Interlocked.Read(ref _acquired), Interlocked.Read(ref _released), Interlocked.Read(ref _live),
        Interlocked.Read(ref _clientLive), Interlocked.Read(ref _finalizers), Interlocked.Read(ref NativeUia.HandlerObjects));
}

static unsafe class NativeUia
{
    internal static long StructureEvents, PropertyEvents, HandlerObjects;
    private static readonly Guid Unknown = new("00000000-0000-0000-c000-000000000046"), PropertyHandler = new("40cd37d4-c756-4b0c-8c6f-bddfeeb13b50"), StructureHandler = new("e81d1b4e-11c5-42f8-9754-e7036c79f054");
    [StructLayout(LayoutKind.Explicit, Size = 24)] internal struct Variant { [FieldOffset(0)] internal ushort Type; [FieldOffset(8)] internal nint Pointer; [FieldOffset(8)] internal int Integer; [FieldOffset(8)] internal short Boolean; }
    [DllImport("ole32.dll")] internal static extern int CoInitializeEx(nint reserved, uint mode);
    [DllImport("ole32.dll")] internal static extern void CoUninitialize();
    [DllImport("ole32.dll")] internal static extern int CoCreateInstance(ref Guid clsid, nint outer, uint context, ref Guid iid, out nint pointer);
    [DllImport("oleaut32.dll")] internal static extern int VariantClear(Variant* variant);
    internal static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
    internal static nint Slot(nint p, int slot) => (*(nint**)p)[slot];
    internal static OwnedUiaReference GetInterface(nint p, int slot)
    {
        nint result = 0; var hr = ((delegate* unmanaged[Stdcall]<nint, nint*, int>)Slot(p, slot))(p, &result);
        return OwnedUiaReference.Adopt(hr, result);
    }
    internal static object? Property(nint p, int id, bool cached)
    {
        Variant value = default;
        try
        {
            Check(((delegate* unmanaged[Stdcall]<nint, int, Variant*, int>)Slot(p, cached ? 12 : 10))(p, id, &value));
            return value.Type switch { 0 => null, 3 => value.Integer, 8 => value.Pointer == 0 ? "" : Marshal.PtrToStringBSTR(value.Pointer), 11 => value.Boolean != 0,
                _ => throw new InvalidOperationException($"Unsupported property variant {value.Type} for {id}.") };
        }
        finally { VariantClear(&value); }
    }
    internal static OwnedUiaReference Pattern(nint p, int id, Guid iid)
    {
        nint result = 0;
        var hr = ((delegate* unmanaged[Stdcall]<nint, int, Guid*, nint*, int>)Slot(p, 14))(p, id, &iid, &result);
        if (result == 0 && hr is unchecked((int)0x80040204) or unchecked((int)0x80004002)) return new(0);
        return OwnedUiaReference.Adopt(hr, result);
    }
    internal static OwnedUiaReference Handler(bool property)
    {
        var table = (nint*)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(NativeUia), 4 * sizeof(nint));
        table[0] = (nint)(delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)&QueryInterface;
        table[1] = (nint)(delegate* unmanaged[Stdcall]<nint, uint>)&AddRef;
        table[2] = (nint)(delegate* unmanaged[Stdcall]<nint, uint>)&Release;
        table[3] = property ? (nint)(delegate* unmanaged[Stdcall]<nint, nint, int, Variant, int>)&OnProperty
            : (nint)(delegate* unmanaged[Stdcall]<nint, nint, int, nint, int>)&OnStructure;
        var p = (nint)NativeMemory.AllocZeroed(16); *(nint*)p = (nint)table; *(int*)(p + 8) = 1; *(int*)(p + 12) = property ? 1 : 0;
        Interlocked.Increment(ref HandlerObjects); return new(p, lifetime: true);
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface(nint self, Guid* iid, nint* result)
    {
        if (result == null) return unchecked((int)0x80004003);
        *result = 0;
        if (*iid != Unknown && *iid != (*(int*)(self + 12) == 1 ? PropertyHandler : StructureHandler)) return unchecked((int)0x80004002);
        Interlocked.Increment(ref *(int*)(self + 8)); *result = self; return 0;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])] private static uint AddRef(nint self) => (uint)Interlocked.Increment(ref *(int*)(self + 8));
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(nint self)
    {
        var count = Interlocked.Decrement(ref *(int*)(self + 8));
        if (count == 0) { NativeMemory.Free((void*)self); Interlocked.Decrement(ref HandlerObjects); }
        return (uint)count;
    }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])] private static int OnStructure(nint self, nint sender, int kind, nint runtimeId) { Interlocked.Increment(ref StructureEvents); return 0; }
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])] private static int OnProperty(nint self, nint sender, int id, Variant value) { Interlocked.Increment(ref PropertyEvents); return 0; }
}
