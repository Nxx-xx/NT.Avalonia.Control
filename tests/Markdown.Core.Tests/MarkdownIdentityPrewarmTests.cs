using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Core.Tests;

public sealed class MarkdownIdentityPrewarmTests
{
    [Fact]
    public void WarmZeroLookupKeepsTheShortestUnorderedOverlappingSpanAndFirstTie()
    {
        var document = Snapshot([
            Block(1, 4, 2),
            Block(10, 0, 10, [Block(20, 0, 6), Block(21, 0, 6)]),
            Block(30, -2, 3), Block(40, 0, 3),
            Block(50, -1, 1), Block(60, 0, 1), Block(70, 0, 0),
        ]);
        Assert.Equal(new MarkdownTextAnchor(50, 1), MarkdownTextAnchor.FromPosition(document, 0));
        Assert.Equal(new MarkdownTextAnchor(40, 2), MarkdownTextAnchor.FromPosition(document, 2));
        Assert.Equal(new MarkdownTextAnchor(1, 2), MarkdownTextAnchor.FromPosition(document, 6));
        var index = MarkdownSnapshotIdentityIndex.Get(document);
        Assert.Equal(9, index.Count);
        Assert.Equal(new MarkdownTextAnchor(50, 1), MarkdownTextAnchor.FromPosition(document, 0));
        Assert.Equal(new MarkdownTextAnchor(40, 2), MarkdownTextAnchor.FromPosition(document, 2));
        Assert.Equal(new MarkdownTextAnchor(1, 2), MarkdownTextAnchor.FromPosition(document, 6));
        Assert.Equal(new MarkdownTextAnchor(10, 10), MarkdownTextAnchor.FromPosition(document, 10));
    }

    [Fact]
    public void EqualLengthParentAndChildKeepTheFirstPreorderNodeBeforeAndAfterWarming()
    {
        var document = Snapshot([Block(1, 0, 5, [Block(2, 0, 5)]), Block(3, 0, 5)]);
        Assert.Equal(new MarkdownTextAnchor(1, 0), MarkdownTextAnchor.FromPosition(document, 0));
        _ = MarkdownSnapshotIdentityIndex.Get(document);
        Assert.Equal(new MarkdownTextAnchor(1, 0), MarkdownTextAnchor.FromPosition(document, 0));
        Assert.Equal(new MarkdownTextAnchor(1, 3), MarkdownTextAnchor.FromPosition(document, 3));
    }

    [Fact]
    public void ACompletedIndexCanRepresentNoContainingZeroSpanIncludingAnEmptySnapshot()
    {
        foreach (var document in new[] { MarkdownDocumentSnapshot.Empty, Snapshot([Block(1, 1, 2), Block(2, 0, 0)]) })
        {
            Assert.Equal(new MarkdownTextAnchor(0, 0), MarkdownTextAnchor.FromPosition(document, 0));
            _ = MarkdownSnapshotIdentityIndex.Get(document);
            Assert.Equal(new MarkdownTextAnchor(0, 0), MarkdownTextAnchor.FromPosition(document, 0));
        }
    }

    [Fact]
    public void DuplicateOldIdsStillUseFirstPreorderResolutionWithoutImplicitlyBuildingAnIndex()
    {
        var parser = new MarkdownParser();
        var parsed = parser.Parse("abcde");
        var parent = parsed.Blocks[0];
        var child = parent with { TextStart = 1, TextLength = 2, Text = "bc", Children = [], Inlines = [] };
        var previous = parsed with { Blocks = [parent with { Children = [child] }] };
        var next = parser.Parse("abcXde");
        var current = next with { Blocks = [next.Blocks[0] with { Id = parent.Id }] };

        Assert.Equal(new MarkdownTextAnchor(parent.Id, 0), MarkdownTextAnchor.FromPosition(previous, 0));
        Assert.Equal(new MarkdownTextAnchor(parent.Id, 1), MarkdownTextAnchor.FromPosition(previous, 2));
        Assert.Throws<ArgumentException>(() => MarkdownSnapshotIdentityIndex.Get(previous));
        // A failed eager build must not publish a partial cache or change the
        // original supported duplicate-old-ID migration behavior.
        _ = MarkdownSnapshotIdentityIndex.Get(current);
        Assert.Equal(new MarkdownTextAnchor(parent.Id, 0), MarkdownTextAnchor.FromPosition(previous, 0));
        Assert.Equal(new MarkdownSelection(1, 1), MarkdownDocumentMatcher.MigrateSelection(previous, current, new(2, 2)));
        Assert.Same(previous.Blocks[0], previous.FindBlock(parent.Id));
    }

    [Fact]
    public void DuplicateCurrentIdsRetainTheOriginalIndexAndMigrationExceptions()
    {
        var previous = Snapshot([Block(1, 0, 5)]);
        var current = Snapshot([Block(1, 0, 5), Block(1, 0, 1)]);
        Assert.Equal(new MarkdownTextAnchor(1, 0), MarkdownTextAnchor.FromPosition(current, 0));
        Assert.Throws<ArgumentException>(() => MarkdownSnapshotIdentityIndex.Get(current));
        Assert.Equal(new MarkdownTextAnchor(1, 0), MarkdownTextAnchor.FromPosition(current, 0));
        Assert.Throws<ArgumentException>(() => MarkdownDocumentMatcher.MigrateSelection(previous, current, new(0, 0)));
        Assert.Throws<ArgumentException>(() => MarkdownDocumentMatcher.GetChanges(previous, current));
    }

    [Fact]
    public async Task BackgroundPreparationPublishesACompleteReusableIndexWithoutChangingMigrationOrChanges()
    {
        var parser = new MarkdownParser();
        var previous = parser.Parse("first **bold**\n\n- item\n- second\n\ntail");
        var current = parser.Parse(previous.Source + " appended", previous);
        var beforeAnchor = MarkdownTextAnchor.FromPosition(previous, 0);
        var prepared = await Task.Run(() => MarkdownSnapshotIdentityIndex.Get(current));
        Assert.Equal(current.Descendants().Count(), prepared.Count);
        Assert.All(current.Descendants(), block => Assert.Same(block, prepared[block.Id]));
        Assert.Same(prepared, MarkdownSnapshotIdentityIndex.Get(current));
        _ = await Task.Run(() => MarkdownSnapshotIdentityIndex.Get(previous));
        Assert.Equal(beforeAnchor, MarkdownTextAnchor.FromPosition(previous, 0));
        Assert.Equal(new MarkdownSelection(0, 0), MarkdownDocumentMatcher.MigrateSelection(previous, current, new(0, 0)));
        var changes = MarkdownDocumentMatcher.GetChanges(previous, current);
        Assert.Empty(changes.Added);
        Assert.Empty(changes.Removed);
        Assert.Contains(current.Blocks[^1].Id, changes.Changed);
    }

    [Fact]
    public void TheIndexAndItsZeroAnchorDoNotKeepTheSnapshotOrItsSourceAlive()
    {
        var (snapshot, source) = PrepareDetachedIndex();
        for (var pass = 0; pass < 3 && (snapshot.IsAlive || source.IsAlive); pass++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.False(snapshot.IsAlive);
        Assert.False(source.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Snapshot, WeakReference Source) PrepareDetachedIndex()
    {
        var source = new string(Guid.NewGuid().ToString("N").ToCharArray());
        var document = new MarkdownDocumentSnapshot(source, "abcdefghij", [Block(1, 0, 5)], []);
        _ = MarkdownSnapshotIdentityIndex.Get(document);
        return (new(document), new(source));
    }

    private static MarkdownDocumentSnapshot Snapshot(ImmutableArray<MarkdownBlock> blocks) =>
        new("independent public source", "abcdefghij", blocks, []);

    private static MarkdownBlock Block(long id, int start, int length, ImmutableArray<MarkdownBlock> children = default) =>
        new(id, MarkdownBlockKind.Paragraph, 0, Math.Max(0, length), start, length,
            new string('x', Math.Max(0, length)), children.IsDefault ? [] : children, [], ImmutableDictionary<string, string>.Empty);
}
