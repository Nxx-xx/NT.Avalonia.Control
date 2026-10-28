using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Buffers.Binary;

namespace TreeDataGridNativeBenchmarks;

internal sealed record AutomationItem(string Id, string Name, string? Value, bool? Selected);
internal sealed record AutomationRequest(int Id, long Handle, int HostPid, string GridId, bool Retired, bool ReadTree = true);
internal sealed record AutomationReply(int Id, int ClientPid, int HostPid, string GridId, bool Retired,
    bool GridFound, string Hash, AutomationItem[] Items, long StartTicks, long EndTicks, string? Error, long StructureEvents, long PropertyEvents, string? SubscriptionGridId,
    string Workload = "both", long ClientManagedBytes = 0, int ClientGen2Collections = 0, AutomationOwnershipReceipt? Ownership = null);
internal sealed record AutomationOwnershipReceipt(int Version, long Acquired, long Released, long LiveQueryReferences,
    long ClientLifetimeReferences, long FinalizerReleases, long HandlerObjects);
internal static class AutomationProtocol
{
    internal static string Hash(IEnumerable<AutomationItem> items)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var item in items.OrderBy(i => i.Id, StringComparer.Ordinal))
        {
            Append(item.Id); Append(item.Name); Append(item.Value);
            hash.AppendData([(byte)(item.Selected is null ? 2 : item.Selected.Value ? 1 : 0)]);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
        void Append(string? value)
        {
            var bytes = value is null ? [] : Encoding.UTF8.GetBytes(value);
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(length, value is null ? -1 : bytes.Length);
            hash.AppendData(length); hash.AppendData(bytes);
        }
    }
}
