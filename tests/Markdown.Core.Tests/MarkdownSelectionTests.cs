using NT.AvaloniaControl.Markdown;

namespace Markdown.Core.Tests;

public sealed class MarkdownSelectionTests
{
    [Fact]
    public void LargeTextEndpointsAndWholeSelectionDoNotRequireAFullGraphemeIndex()
    {
        var text = new string('x', 1024 * 1024);
        new MarkdownSelection(0, 0).Normalize("warm");
        MarkdownTextBoundary.Previous("warm", 0);
        MarkdownTextBoundary.Next("warm", 4);
        MarkdownTextBoundary.SnapForward("warm", 0);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var collapsed = new MarkdownSelection(0, 0).Normalize(text);
        var all = new MarkdownSelection(0, text.Length).Normalize(text);
        var previous = MarkdownTextBoundary.Previous(text, int.MinValue);
        var next = MarkdownTextBoundary.Next(text, int.MaxValue);
        var first = MarkdownTextBoundary.SnapForward(text, 0);
        var last = MarkdownTextBoundary.SnapBackward(text, text.Length);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(new MarkdownSelection(0, 0), collapsed);
        Assert.Equal(new MarkdownSelection(0, text.Length), all);
        Assert.Equal(0, previous);
        Assert.Equal(text.Length, next);
        Assert.Equal(0, first);
        Assert.Equal(text.Length, last);
        // This guards managed index allocation for known endpoints, not any UI/timing budget.
        Assert.InRange(allocated, 0, 8 * 1024);
    }

    [Fact]
    public void GraphemeNavigationDoesNotSplitSurrogatesCombiningMarksOrZwjEmoji()
    {
        const string text = "中😀e\u0301👨‍👩‍👧‍👦אב";
        var expected = new[] { "中", "😀", "e\u0301", "👨‍👩‍👧‍👦", "א", "ב" };
        var position = 0;
        foreach (var grapheme in expected)
        {
            var next = MarkdownTextBoundary.Next(text, position);
            Assert.Equal(grapheme, text[position..next]);
            Assert.Equal(position, MarkdownTextBoundary.Previous(text, next));
            position = next;
        }
        Assert.Equal(text.Length, position);
        Assert.Equal(text.Length, MarkdownTextBoundary.Next(text, position));
        Assert.Equal(0, MarkdownTextBoundary.Previous(text, 0));
    }

    [Fact]
    public void ArbitraryRangesExpandToCompleteGraphemesAndKeepDirection()
    {
        const string text = "x😀e\u0301y";
        Assert.Equal(new MarkdownSelection(1, 5), new MarkdownSelection(2, 4).Normalize(text));
        Assert.Equal(new MarkdownSelection(5, 1), new MarkdownSelection(4, 2).Normalize(text));
        Assert.Equal(new MarkdownSelection(1, 1), new MarkdownSelection(2, 2).Normalize(text));
        Assert.Equal(new MarkdownSelection(0, 6), new MarkdownSelection(-10, 100).Normalize(text));
        Assert.Equal(new MarkdownSelection(0, 0), new MarkdownSelection(-10, 100).Normalize(string.Empty));
    }

    [Fact]
    public void RepeatedParagraphInsertionPreservesEachOriginalIdentityAndSelection()
    {
        var parser = new MarkdownParser();
        var previous = parser.Parse("same\n\nseparator\n\nsame\n\nlast");
        var originalId = previous.Blocks[2].Id;
        var selection = new MarkdownSelection(previous.Blocks[2].TextStart, previous.Blocks[2].TextStart + 4);
        var current = parser.Parse("new\n\nsame\n\nseparator\n\nsame\n\nlast", previous);
        Assert.Equal(previous.Blocks[0].Id, current.Blocks[1].Id);
        Assert.Equal(originalId, current.Blocks[3].Id);
        Assert.NotEqual(current.Blocks[1].Id, current.Blocks[3].Id);
        var migrated = MarkdownDocumentMatcher.MigrateSelection(previous, current, selection);
        Assert.Equal(current.Blocks[3].TextStart, migrated.Start);
        Assert.Equal("same", migrated.GetText(current));
    }

    [Fact]
    public void InsertionBetweenRepeatedParagraphsDoesNotReuseTheNewParagraph()
    {
        var parser = new MarkdownParser();
        var previous = parser.Parse("same\n\nmarker\n\nsame");
        var current = parser.Parse("same\n\nnew\n\nmarker\n\nsame", previous);
        Assert.Equal(previous.Blocks[0].Id, current.Blocks[0].Id);
        Assert.Equal(previous.Blocks[1].Id, current.Blocks[2].Id);
        Assert.Equal(previous.Blocks[2].Id, current.Blocks[3].Id);
        Assert.DoesNotContain(previous.Descendants(), block => block.Id == current.Blocks[1].Id);
    }

    [Fact]
    public void AmbiguousIdenticalInsertionsRebuildInsteadOfMovingAnAnchorToAnotherCopy()
    {
        var parser = new MarkdownParser();
        var previous = parser.Parse("same\n\nsame\n\nlast");
        var current = parser.Parse("same\n\nsame\n\nsame\n\nlast", previous);
        Assert.DoesNotContain(current.Blocks.Take(3), block => previous.Blocks.Take(2).Any(old => old.Id == block.Id));
        Assert.Equal(previous.Blocks[2].Id, current.Blocks[3].Id);
    }

    [Fact]
    public void DeletingSelectionAnchorFallsBackToThePreviousSurvivingPosition()
    {
        var parser = new MarkdownParser();
        var previous = parser.Parse("first\n\nremoved\n\nlast");
        var current = parser.Parse("first\n\nlast", previous);
        var migrated = MarkdownDocumentMatcher.MigrateSelection(previous, current, new MarkdownSelection(7, 10));
        Assert.Equal(new MarkdownSelection(5, 5), migrated);
    }

    [Fact]
    public void StreamingAppendKeepsExistingSelectionAndIdentifiesChangedBlock()
    {
        var parser = new MarkdownParser();
        var previous = parser.Parse("stable\n\npar");
        var current = parser.Parse("stable\n\nparagraph", previous);
        Assert.Equal(previous.Blocks[0].Id, current.Blocks[0].Id);
        Assert.Equal(previous.Blocks[1].Id, current.Blocks[1].Id);
        Assert.Equal(new MarkdownSelection(0, 3), MarkdownDocumentMatcher.MigrateSelection(previous, current, new MarkdownSelection(0, 3)));
        var changes = MarkdownDocumentMatcher.GetChanges(previous, current);
        Assert.Empty(changes.Added);
        Assert.Empty(changes.Removed);
        Assert.Equal(new[] { current.Blocks[1].Id }, changes.Changed);
    }

    [Fact]
    public void ReadingAnchorSurvivesPrecedingInsertionAndNeedsNoVisual()
    {
        var parser = new MarkdownParser();
        var previous = parser.Parse("first\n\nreading here\n\nlast");
        var anchor = MarkdownTextAnchor.FromPosition(previous, previous.Blocks[1].TextStart + 3);
        var current = parser.Parse("new\n\n" + previous.Source, previous);
        Assert.Equal(current.Blocks[2].TextStart + 3, anchor.Resolve(current));
    }
}
