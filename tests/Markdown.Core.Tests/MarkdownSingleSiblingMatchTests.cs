using System.Collections.Immutable;
using Markdig.Syntax;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Core.Tests;

public sealed class MarkdownSingleSiblingMatchTests
{
    [Theory]
    [InlineData("same", "same", true)]
    [InlineData("word", "word appended", true)]
    [InlineData("word", "\n\nword", true)]
    [InlineData("word", "other", false)]
    [InlineData("word", "# word", false)]
    [InlineData("**partial", "**partial**", false)]
    [InlineData("[label](a)", "[label](b)", true)]
    public void OneSiblingKeepsIdentityOnlyForProvenUnchangedSourceOrVisibleContinuation(string before, string after, bool sameIdentity)
    {
        var parser = new MarkdownParser();
        var previous = parser.Parse(before);
        var parsed = parser.Parse(after);
        var current = MarkdownDocumentMatcher.Reconcile(previous, parsed);
        var oldBlock = Assert.Single(previous.Blocks);
        var newBlock = Assert.Single(current.Blocks);
        Assert.Equal(sameIdentity, oldBlock.Id == newBlock.Id);
        Assert.Equal(parsed.Text, current.Text);
        Assert.Equal(parsed.Blocks[0].SourceStart, newBlock.SourceStart);
        Assert.Equal(parsed.Blocks[0].SourceLength, newBlock.SourceLength);
    }

    [Fact]
    public void SameCopyTextWithChangedInlineOrderStillInvalidatesTheRetainedBlock()
    {
        var parser = new MarkdownParser();
        var previous = parser.Parse("*x* **y**");
        var current = parser.Parse("**x** *y*", previous);
        Assert.Equal("x y", previous.Text);
        Assert.Equal(previous.Text, current.Text);
        Assert.Equal(previous.Blocks[0].Id, current.Blocks[0].Id);
        Assert.Equal(MarkdownInlineKind.Emphasis, previous.Blocks[0].Inlines[0].Kind);
        Assert.Equal(MarkdownInlineKind.Strong, current.Blocks[0].Inlines[0].Kind);
        Assert.Contains(current.Blocks[0].Id, MarkdownDocumentMatcher.GetChanges(previous, current).Changed);
    }

    [Fact]
    public void OneChildMetadataChangesInvalidateOnlyItsChangedSemanticNodes()
    {
        MarkdownParser Parser(string caption) => new(converters:
        [new MarkdownNodeConverterRegistration<ParagraphBlock>(_ => new("paragraph")
         { Metadata = ImmutableDictionary<string, string>.Empty.Add("caption", caption) })]);
        const string source = "> same";
        var previous = Parser("old").Parse(source);
        var current = Parser("new").Parse(source, previous);
        var oldChild = Assert.Single(previous.Blocks[0].Children);
        var newChild = Assert.Single(current.Blocks[0].Children);
        Assert.Equal(previous.Blocks[0].Id, current.Blocks[0].Id);
        Assert.Equal(oldChild.Id, newChild.Id);
        Assert.Equal("old", oldChild.Metadata["caption"]);
        Assert.Equal("new", newChild.Metadata["caption"]);
        var changes = MarkdownDocumentMatcher.GetChanges(previous, current);
        Assert.Contains(current.Blocks[0].Id, changes.Changed);
        Assert.Contains(newChild.Id, changes.Changed);
    }

    [Theory]
    [InlineData("same", "same\n\nsame")]
    [InlineData("same\n\nsame", "same")]
    public void ChangingTheNumberOfIdenticalSiblingsStillRebuildsTheirIdentities(string before, string after)
    {
        var parser = new MarkdownParser();
        var previous = parser.Parse(before);
        var current = parser.Parse(after, previous);
        Assert.DoesNotContain(current.Blocks, block => previous.Blocks.Any(old => old.Id == block.Id));
    }

    [Fact]
    public void ReconcilingASingleChildChainAvoidsPerLevelMatchingIndexes()
    {
        const int depth = 128;
        var previousBlock = Block(depth, []);
        var currentBlock = Block(depth + 1000, []);
        for (var id = depth - 1; id >= 1; id--)
        {
            previousBlock = Block(id, [previousBlock]);
            currentBlock = Block(id + 1000, [currentBlock]);
        }
        var previous = new MarkdownDocumentSnapshot("x", "x", [previousBlock], []);
        var parsed = new MarkdownDocumentSnapshot("x", "x", [currentBlock], []);
        MarkdownDocumentMatcher.Reconcile(previous, parsed); // Warm comparisons and pooled metadata enumerators.
        var before = GC.GetAllocatedBytesForCurrentThread();
        var current = MarkdownDocumentMatcher.Reconcile(previous, parsed);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(previous.Descendants().Select(block => block.Id), current.Descendants().Select(block => block.Id));
        Assert.Equal(parsed.Text, current.Text);
        // Allows cloned immutable records/arrays, while detecting the previous per-level index
        // allocations. This managed allocation guard is independent of UI/timing budgets.
        Assert.InRange(allocated, 0, 96 * 1024);
    }

    private static MarkdownBlock Block(long id, ImmutableArray<MarkdownBlock> children) => new(id,
        children.IsEmpty ? MarkdownBlockKind.Paragraph : MarkdownBlockKind.Quote,
        0, 1, 0, 1, "x", children, [], ImmutableDictionary<string, string>.Empty);
}
