using System.Collections.Immutable;
using System.Text.Json;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Core.Tests;

public sealed class MarkdownStructureProjectionTests
{
    private const string TrailingRowSpan = "+----------+----------+\n| left     | right    |\n+==========+==========+\n| A        | span     |\n+----------+          +\n| B        |          |\n+----------+----------+";
    private const string LeadingRowSpan = "+----------+----------+\n| left     | right    |\n+==========+==========+\n| span     | A        |\n+          +----------+\n|          | B        |\n+----------+----------+";
    private const string MiddleRowSpan = "+----------+----------+----------+\n| left     | middle   | right    |\n+==========+==========+==========+\n| A        | span     | C        |\n+----------+          +----------+\n| B        |          | D        |\n+----------+----------+----------+";
    private const string LooseList = "7. first\n\n   paragraph two\n\n   > quote\n   >\n   > - nested\n\n   ```cs\n   code  \n   ```\n\n8. last";
    private const string ComplexFootnotes = "before[^z], second[^a], repeat[^z].\n\n[^a]: alpha\n\n    alpha two\n\n[^z]: zed\n\n    - nested\n\n[^unused]: hidden";
    private readonly MarkdownParser _parser = new();

    [Theory]
    [InlineData(TrailingRowSpan, "left\tright\nA\tspan\nB\t", "B\t", 0, 2)]
    [InlineData(LeadingRowSpan, "left\tright\nspan\tA\n\tB", "\tB", 1, 2)]
    [InlineData(MiddleRowSpan, "left\tmiddle\tright\nA\tspan\tC\nB\t\tD", "B\t\tD", 0, 3)]
    public void GridRowSpansPreserveLeadingMiddleAndTrailingCopySlots(string source, string expected, string lastRow, int lastFirstColumn, int columns)
    {
        var document = _parser.Parse(source);
        var table = Assert.Single(document.Blocks);
        Assert.Equal(expected, document.Text);
        Assert.Equal(columns.ToString(), table.Metadata["columns"]);
        Assert.Equal(lastRow, table.Children[^1].Text);
        Assert.Equal(lastFirstColumn.ToString(), table.Children[^1].Children[0].Metadata["column"]);
        Assert.Contains(table.Children[1].Children, cell => cell.Metadata["row-span"] == "2");
        Assert.Equal(lastRow, new MarkdownSelection(table.Children[^1].TextStart, table.Children[^1].TextStart + table.Children[^1].TextLength).GetText(document));
        AssertProjectionRanges(document);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void RaggedAndEmptyPipeRowsKeepTheNormalizedTableWidth()
    {
        var document = _parser.Parse("| A | B | C |\n| --- | --- | --- |\n| a |\n| | | |");
        var table = Assert.Single(document.Blocks);
        Assert.Equal("3", table.Metadata["columns"]);
        Assert.Equal("A\tB\tC\na\t\t\n\t\t", document.Text);
        Assert.All(table.Children, row => Assert.Equal(3, row.Children.Length));
        Assert.All(table.Children[^1].Children, cell => Assert.Equal(0, cell.TextLength));
        AssertProjectionRanges(document);
    }

    [Fact]
    public void ExtraPipeColumnsExpandEarlierRowsAndKeepEscapedCellContent()
    {
        var document = _parser.Parse("| A | B | C |\n| :--- | :---: | ---: |\n| a\\|b | **&amp;** | `code` | extra |");
        var table = Assert.Single(document.Blocks);
        Assert.Equal("4", table.Metadata["columns"]);
        Assert.Equal("A\tB\tC\t\na|b\t&\tcode\textra", document.Text);
        Assert.Equal("right", table.Children[1].Children[2].Metadata["alignment"]);
        AssertProjectionRanges(document);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void LooseListKeepsParagraphQuoteNestedListAndCodeInReadingOrder()
    {
        var document = _parser.Parse(LooseList);
        var list = Assert.Single(document.Blocks);
        Assert.Equal("true", list.Metadata["loose"]);
        Assert.Equal("7", list.Metadata["start"]);
        Assert.Equal("7. first\nparagraph two\nquote\n- nested\ncode  \n\n8. last", document.Text);
        Assert.Equal(new[] { MarkdownBlockKind.Paragraph, MarkdownBlockKind.Paragraph, MarkdownBlockKind.Quote, MarkdownBlockKind.Code }, list.Children[0].Children.Select(block => block.Kind));
        Assert.Equal("code  \n", list.Children[0].Children[^1].Text);
        Assert.Equal("7. ", list.Children[0].Metadata["marker"]);
        Assert.Equal("8. ", list.Children[1].Metadata["marker"]);
        AssertProjectionRanges(document);
        Assert.Equal(document.Text, new MarkdownSelection(document.Text.Length, 0).GetText(document));
    }

    [Fact]
    public void TightListChangesMarkerWidthAndKeepsNestedTasksInsideTheParentItem()
    {
        var document = _parser.Parse("9. one\n10. two\n    - [x] nested\n    - child\n11. last");
        var list = Assert.Single(document.Blocks);
        Assert.Equal("false", list.Metadata["loose"]);
        Assert.Equal("9. one\n10. two\n- [x] nested\n- child\n11. last", document.Text);
        Assert.Equal(new[] { "9. ", "10. ", "11. " }, list.Children.Select(item => item.Metadata["marker"]));
        var middle = list.Children[1];
        Assert.Equal(MarkdownBlockKind.List, middle.Children[1].Kind);
        var task = middle.Children[1].Children[0].Children[0].Inlines.Single(inline => inline.Kind == MarkdownInlineKind.Task);
        Assert.Equal("true", task.Metadata["checked"]);
        Assert.True(task.TextStart >= middle.TextStart);
        Assert.True(task.TextStart + task.TextLength <= middle.TextStart + middle.TextLength);
        AssertProjectionRanges(document);
    }

    [Fact]
    public void FootnotesUseFirstReferenceOrderAndExcludeBacklinkOnlyParagraphs()
    {
        var document = _parser.Parse(ComplexFootnotes);
        Assert.Equal("before[1], second[2], repeat[1].\n[1] zed\n- nested\n[2] alpha\nalpha two", document.Text);
        var notes = document.Descendants().Where(block => block.Kind == MarkdownBlockKind.Footnote).ToArray();
        Assert.Equal(new[] { "^z", "^a" }, notes.Select(note => note.Metadata["label"]));
        Assert.Equal(new[] { "1", "2" }, notes.Select(note => note.Metadata["number"]));
        Assert.Equal(2, notes[0].Children.Length);
        Assert.Equal(new[] { "1", "2", "1" }, document.Blocks[0].Inlines.Where(inline => inline.Kind == MarkdownInlineKind.FootnoteReference).Select(inline => inline.Metadata["number"]));
        Assert.DoesNotContain("hidden", document.Text);
        AssertProjectionRanges(document);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void LateNestedFootnoteDefinitionsResolveEarlierReferencesWithoutMutatingThePrefix()
    {
        const string prefixSource = "before[^z] and [late][r]\n\n";
        var prefix = _parser.Parse(prefixSource);
        var frozen = JsonSerializer.Serialize(prefix);
        var final = _parser.Parse(prefixSource + "[^z]: note\n\n    - child\n\n[r]: https://later.example", prefix);
        Assert.Equal("before[1] and late\n[1] note\n- child", final.Text);
        Assert.Equal("https://later.example", final.Blocks[0].Inlines.Single(inline => inline.Kind == MarkdownInlineKind.Link).Metadata["url"]);
        Assert.Equal(frozen, JsonSerializer.Serialize(prefix));
        AssertProjectionRanges(prefix);
        AssertProjectionRanges(final);
    }

    [Theory]
    [InlineData(TrailingRowSpan)]
    [InlineData(MiddleRowSpan)]
    [InlineData(LooseList)]
    [InlineData(ComplexFootnotes)]
    public void ComplexStructuresRemainImmutableAndEquivalentAtEverySplit(string source)
    {
        var expected = Canonical(_parser.Parse(source));
        for (var split = 0; split <= source.Length; split++)
        {
            var prefix = _parser.Parse(source[..split]);
            var frozen = JsonSerializer.Serialize(prefix);
            var final = _parser.Parse(source, prefix);
            Assert.Equal(expected, Canonical(final));
            Assert.Equal(frozen, JsonSerializer.Serialize(prefix));
            AssertProjectionRanges(prefix);
            AssertProjectionRanges(final);
        }
    }

    private static void AssertProjectionRanges(MarkdownDocumentSnapshot document)
    {
        foreach (var block in document.Descendants())
        {
            Assert.InRange(block.SourceStart, 0, document.Source.Length);
            Assert.InRange(block.SourceLength, 0, document.Source.Length - block.SourceStart);
            Assert.Equal(block.Text, document.Text.Substring(block.TextStart, block.TextLength));
            foreach (var inline in block.Inlines) AssertInlineRange(document, inline, block.TextStart, block.TextLength);
            foreach (var child in block.Children)
            {
                Assert.InRange(child.TextStart, block.TextStart, block.TextStart + block.TextLength);
                Assert.InRange(child.TextLength, 0, block.TextStart + block.TextLength - child.TextStart);
            }
        }
    }
    private static void AssertInlineRange(MarkdownDocumentSnapshot document, MarkdownInline inline, int parentStart, int parentLength)
    {
        Assert.InRange(inline.SourceStart, 0, document.Source.Length);
        Assert.InRange(inline.SourceLength, 0, document.Source.Length - inline.SourceStart);
        Assert.Equal(inline.Text, document.Text.Substring(inline.TextStart, inline.TextLength));
        Assert.InRange(inline.TextStart, parentStart, parentStart + parentLength);
        Assert.InRange(inline.TextLength, 0, parentStart + parentLength - inline.TextStart);
        foreach (var child in inline.Children) AssertInlineRange(document, child, inline.TextStart, inline.TextLength);
    }
    private static string Canonical(MarkdownDocumentSnapshot document) => JsonSerializer.Serialize(document with { Blocks = document.Blocks.Select(StripIdentity).ToImmutableArray() });
    private static MarkdownBlock StripIdentity(MarkdownBlock block) => block with { Id = 0, Children = block.Children.Select(StripIdentity).ToImmutableArray() };
}
