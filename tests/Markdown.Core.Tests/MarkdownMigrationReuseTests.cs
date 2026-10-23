using NT.AvaloniaControl.Markdown;

namespace Markdown.Core.Tests;

public sealed class MarkdownMigrationReuseTests
{
    [Fact]
    public void EmptyCaretAfterAmbiguousDuplicateGrowthUsesTheFirstSurvivingSuccessor()
    {
        var parser = new MarkdownParser();
        var previous = parser.Parse(string.Join("\n\n", Enumerable.Repeat("same", 2000)) + "\n\n尾部😀");
        var current = parser.Parse("same\n\n" + previous.Source, previous);
        Assert.DoesNotContain(current.Blocks.Take(2001), block => block.Id == previous.Blocks[0].Id);
        Assert.Equal(previous.Blocks[^1].Id, current.Blocks[^1].Id);
        var migrated = MarkdownDocumentMatcher.MigrateSelection(previous, current, new(0, 0));
        Assert.Equal(new MarkdownSelection(current.Blocks[^1].TextStart, current.Blocks[^1].TextStart), migrated);
    }

    [Fact]
    public void EqualLengthParentAndChildAnchorsKeepTheFirstPreorderIdentity()
    {
        var parser = new MarkdownParser();
        var previous = parser.Parse("> abc");
        var current = parser.Parse("> abcx", previous);
        Assert.Equal(previous.Blocks[0].TextLength, previous.Blocks[0].Children[0].TextLength);
        var anchor = MarkdownTextAnchor.FromPosition(previous, 2);
        Assert.Equal(previous.Blocks[0].Id, anchor.BlockId);
        Assert.Equal(2, anchor.Resolve(current));
        Assert.Equal(new MarkdownSelection(3, 1), MarkdownDocumentMatcher.MigrateSelection(previous, current, new(3, 1)));
    }

    [Fact]
    public void ReverseSelectionAcrossDeletedLeafAndSurvivingUnicodeTailKeepsDirection()
    {
        var parser = new MarkdownParser();
        var previous = parser.Parse("first\n\nremoved\n\ne\u0301😀尾");
        var current = parser.Parse("first\n\ne\u0301😀尾", previous);
        var selection = new MarkdownSelection(previous.Text.Length, previous.Blocks[1].TextStart + 2);
        var migrated = MarkdownDocumentMatcher.MigrateSelection(previous, current, selection);
        Assert.Equal(new MarkdownSelection(current.Text.Length, current.Blocks[0].TextLength), migrated);
        Assert.Equal("\ne\u0301😀尾", migrated.GetText(current));
    }

    [Fact]
    public void ManuallyConstructedDuplicateOldIdsStillResolveTheFirstPreorderNode()
    {
        var parser = new MarkdownParser();
        var parsed = parser.Parse("abcde");
        var parent = parsed.Blocks[0];
        var child = parent with { TextStart = 1, TextLength = 2, Text = "bc", Children = [], Inlines = [] };
        var previous = parsed with { Blocks = [parent with { Children = [child] }] };
        var next = parser.Parse("abcXde");
        var current = next with { Blocks = [next.Blocks[0] with { Id = parent.Id }] };
        Assert.Equal(new MarkdownTextAnchor(parent.Id, 1), MarkdownTextAnchor.FromPosition(previous, 2));
        Assert.Equal(new MarkdownSelection(1, 1), MarkdownDocumentMatcher.MigrateSelection(previous, current, new(2, 2)));
    }
}
