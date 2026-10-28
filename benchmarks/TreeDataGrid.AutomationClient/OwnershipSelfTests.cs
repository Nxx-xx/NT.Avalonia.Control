using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static unsafe class OwnershipSelfTests
{
    internal static void Run()
    {
        var checks = new Dictionary<string, bool>();
        void Require(string name, bool value) { checks[name] = value; if (!value) throw new InvalidOperationException(name); }
        Require("variant-abi", Marshal.SizeOf<NativeUia.Variant>() == 24 && RuntimeInformation.ProcessArchitecture == Architecture.X64);
        using (var handler = NativeUia.Handler(false))
        {
            Guid unknown = new("00000000-0000-0000-c000-000000000046");
            nint alias = 0;
            NativeUia.Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)NativeUia.Slot(handler.P, 0))(handler.P, &unknown, &alias));
            var owned = OwnedUiaReference.Adopt(0, alias);
            owned.Dispose(); owned.Dispose();
            Require("double-dispose-releases-once", OwnedUiaReference.Receipt() is { LiveQueryReferences: 0, Acquired: 1, Released: 1 } && NativeUia.HandlerObjects == 1);
            alias = 0;
            NativeUia.Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)NativeUia.Slot(handler.P, 0))(handler.P, &unknown, &alias));
            try { using var failure = OwnedUiaReference.Adopt(unchecked((int)0x80004005), alias); throw new InvalidOperationException("Expected HRESULT failure."); }
            catch (COMException) { }
            Require("failed-result-releases-returned-pointer", OwnedUiaReference.Receipt() is { LiveQueryReferences: 0, Acquired: 2, Released: 2 } && NativeUia.HandlerObjects == 1);
            alias = 0;
            NativeUia.Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)NativeUia.Slot(handler.P, 0))(handler.P, &unknown, &alias));
            try { using var local = OwnedUiaReference.Adopt(0, alias); throw new InvalidOperationException("unwind"); }
            catch (InvalidOperationException e) when (e.Message == "unwind") { }
            Require("exception-unwind-releases-pointer", OwnedUiaReference.Receipt() is { LiveQueryReferences: 0, Acquired: 3, Released: 3 });
            var structure = NativeUia.StructureEvents;
            NativeUia.Check(((delegate* unmanaged[Stdcall]<nint, nint, int, nint, int>)NativeUia.Slot(handler.P, 3))(handler.P, handler.P, 2, 0));
            Require("structure-callback-borrows-sender", NativeUia.StructureEvents == structure + 1 && OwnedUiaReference.Receipt().LiveQueryReferences == 0);
        }
        using (var handler = NativeUia.Handler(true))
        {
            var properties = NativeUia.PropertyEvents;
            NativeUia.Check(((delegate* unmanaged[Stdcall]<nint, nint, int, NativeUia.Variant, int>)NativeUia.Slot(handler.P, 3))(handler.P, handler.P, 30079, new() { Type = 11, Boolean = -1 }));
            Require("property-callback-borrows-variant-and-sender", NativeUia.PropertyEvents == properties + 1 && OwnedUiaReference.Receipt().LiveQueryReferences == 0);
        }
        Require("complete-drain-without-finalizers", OwnedUiaReference.Receipt() is { LiveQueryReferences: 0, ClientLifetimeReferences: 0, HandlerObjects: 0, FinalizerReleases: 0 });
        Console.WriteLine(JsonSerializer.Serialize(new { Passed = true, Checks = checks, Ownership = OwnedUiaReference.Receipt() }));
    }
}
