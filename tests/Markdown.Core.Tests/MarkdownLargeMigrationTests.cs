using NT.AvaloniaControl.Markdown;

namespace Markdown.Core.Tests;

public sealed class MarkdownLargeMigrationTests
{
    [Fact]
    public void DeletedTailFallsBackToTheSurvivingPredecessorAmongThousandsOfNewBlocks()
    {
        var parser = new MarkdownParser();
        var previous = parser.Parse("survivor\n\n" + string.Join("\n\n", Enumerable.Range(0, 2000).Select(index => $"old paragraph {index}")));
        var current = parser.Parse("survivor\n\n" + string.Join("\n\n", Enumerable.Range(0, 2000).Select(index => $"new paragraph {index}")), previous);
        Assert.Equal(previous.Blocks[0].Id, current.Blocks[0].Id);
        Assert.DoesNotContain(current.Blocks, block => block.Id == previous.Blocks[^1].Id);
        var migrated = MarkdownDocumentMatcher.MigrateSelection(previous, current, new(previous.Text.Length - 5, previous.Text.Length));
        Assert.Equal(new MarkdownSelection("survivor".Length, "survivor".Length), migrated);
        Assert.Equal(string.Empty, migrated.GetText(current));
    }
}
