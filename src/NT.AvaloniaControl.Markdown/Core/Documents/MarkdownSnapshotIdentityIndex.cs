using System.Runtime.CompilerServices;

namespace NT.AvaloniaControl.Markdown;

// Snapshots are immutable. An ephemeron keeps the identity index available to
// selection and change queries without keeping the snapshot or any UI owner alive.
internal static class MarkdownSnapshotIdentityIndex
{
    private static readonly ConditionalWeakTable<MarkdownDocumentSnapshot, Entry> Indices = new();

    private sealed record Entry(Dictionary<long, MarkdownBlock> ById, MarkdownBlock? ZeroContainingBlock);

    internal static Dictionary<long, MarkdownBlock> Get(MarkdownDocumentSnapshot document) =>
        Indices.GetValue(document, static snapshot =>
        {
            var count = 0;
            foreach (var block in snapshot.Descendants()) count++;
            // Descendants has no cheap Count. Presizing avoids several large
            // abandoned hash tables while constructing the first index.
            var index = new Dictionary<long, MarkdownBlock>(count);
            MarkdownBlock? zero = null;
            foreach (var block in snapshot.Descendants())
            {
                index.Add(block.Id, block);
                // Preserve the original inclusive endpoints, arithmetic and first
                // preorder tie even for overlapping or unordered public snapshots.
                if (block.TextLength > 0 && block.TextStart <= 0 && block.TextStart + block.TextLength >= 0 &&
                    (zero is null || block.TextLength < zero.TextLength)) zero = block;
            }
            return new(index, zero);
        }).ById;

    // A query must not force index construction: duplicate IDs in an old public
    // snapshot are valid inputs to first-preorder anchor lookup and migration.
    internal static bool TryGetZeroContainingBlock(MarkdownDocumentSnapshot document, out MarkdownBlock? block)
    {
        if (Indices.TryGetValue(document, out var entry))
        {
            block = entry.ZeroContainingBlock;
            return true;
        }
        block = null;
        return false;
    }
}
