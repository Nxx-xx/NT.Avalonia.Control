using System.Collections.Immutable;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Core.Tests;

public sealed class MarkdownTraversalTests
{
    [Fact]
    public void TraversalPreservesPreorderAndIndependentEnumerators()
    {
        var document = Snapshot([Block(1, [Block(2), Block(3, [Block(4)])]), Block(5)]);
        using var first = document.Descendants().GetEnumerator();
        Assert.True(first.MoveNext());
        Assert.Equal(1, first.Current.Id);
        Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, document.Descendants().Select(block => block.Id));
        Assert.True(first.MoveNext());
        Assert.Equal(2, first.Current.Id);
        Assert.Equal(4, document.FindBlock(4)!.Id);
        Assert.Null(document.FindBlock(99));
        Assert.Empty(MarkdownDocumentSnapshot.Empty.Descendants());
    }

    [Fact]
    public void TraversalHandlesDeepImmutableDataWithoutRecursiveMoveNextCalls()
    {
        const int depth = 4096;
        var root = Block(depth);
        for (var id = depth - 1; id >= 1; id--) root = Block(id, [root]);
        var document = Snapshot([root]);
        Assert.Equal(Enumerable.Range(1, depth).Select(id => (long)id), document.Descendants().Select(block => block.Id));
        Assert.Equal(depth, document.FindBlock(depth)!.Id);
    }

    [Fact]
    public void TraversingManyLeafBlocksDoesNotAllocateAnIteratorPerBlock()
    {
        const int count = 4096;
        var document = Snapshot(Enumerable.Range(1, count).Select(id => Block(id)).ToImmutableArray());
        long sum = 0;
        foreach (var block in document.Descendants()) sum += block.Id; // Warm iterator code outside the measurement.
        var before = GC.GetAllocatedBytesForCurrentThread();
        sum = 0;
        foreach (var block in document.Descendants()) sum += block.Id;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal((long)count * (count + 1) / 2, sum);
        // A generous guard for iterator/empty stack overhead, far below 4096 per-node iterators.
        // This is a managed traversal allocation guard, not a UI or timing budget.
        Assert.InRange(allocated, 0, 8 * 1024);
    }

    [Fact]
    public void ReconcilingAnEmptySiblingTreeKeepsFreshParsedNodesAndHandlesRemoval()
    {
        var parser = new MarkdownParser();
        var parsed = parser.Parse("paragraph\n\n- item\n  - nested");
        var inserted = MarkdownDocumentMatcher.Reconcile(MarkdownDocumentSnapshot.Empty, parsed);
        Assert.Equal(parsed.Blocks, inserted.Blocks);
        Assert.Same(parsed.Blocks[1], inserted.Blocks[1]);
        Assert.Equal(parsed.Descendants().Select(block => block.Id), inserted.Descendants().Select(block => block.Id));
        var removed = MarkdownDocumentMatcher.Reconcile(parsed, MarkdownDocumentSnapshot.Empty);
        Assert.Empty(removed.Blocks);
        Assert.Empty(removed.Text);
    }

    [Theory]
    [InlineData("[r]: https://example.test\n\n[s]: target.md \"title\"")]
    [InlineData("[^unused]: unreferenced note\n\n    second paragraph")]
    public void ReferenceOnlyDocumentsHaveNoPresentationOrCopyText(string source)
    {
        var document = new MarkdownParser().Parse(source);
        Assert.Equal(source, document.Source);
        Assert.Empty(document.Text);
        Assert.Empty(document.Blocks);
        Assert.Empty(document.Diagnostics);
    }

    private static MarkdownBlock Block(long id, ImmutableArray<MarkdownBlock> children = default) => new(id,
        children.IsDefaultOrEmpty ? MarkdownBlockKind.Paragraph : MarkdownBlockKind.Quote,
        0, 1, 0, 1, "x", children.IsDefault ? [] : children, [], ImmutableDictionary<string, string>.Empty);
    private static MarkdownDocumentSnapshot Snapshot(ImmutableArray<MarkdownBlock> blocks) => new("x", "x", blocks, []);
}
