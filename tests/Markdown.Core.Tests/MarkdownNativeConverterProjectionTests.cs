using System.Collections.Immutable;
using Markdig;
using Markdig.Extensions.CustomContainers;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Core.Tests;

public sealed class MarkdownNativeConverterProjectionTests
{
    [Fact]
    public void NullCustomTextPreservesListMarkersAndNativeSemanticMetadata()
    {
        var parser = new MarkdownParser(converters:
        [new MarkdownNodeConverterRegistration<ListBlock>(_ => new("list")
         { Metadata = ImmutableDictionary<string, string>.Empty.Add("ordered", "false").Add("start", "broken").Add("caption", "custom") }),
         new MarkdownNodeConverterRegistration<ListItemBlock>(_ => new("item")
         { Metadata = ImmutableDictionary<string, string>.Empty.Add("marker", "broken") })]);
        var document = parser.Parse("8. one\n9. two\n   - nested");
        var list = Assert.Single(document.Blocks);
        Assert.Equal("8. one\n9. two\n- nested", document.Text);
        Assert.Equal(MarkdownBlockKind.Custom, list.Kind);
        Assert.Equal(MarkdownBlockKind.List, list.SemanticKind);
        Assert.Equal("List", list.Metadata[MarkdownCustomNodeMetadata.NativeKind]);
        Assert.Equal("true", list.Metadata["ordered"]);
        Assert.Equal("8", list.Metadata["start"]);
        Assert.Equal("custom", list.Metadata["caption"]);
        Assert.Equal("8. ", list.Children[0].Metadata["marker"]);
        Assert.Equal("ListItem", list.Children[0].Metadata[MarkdownCustomNodeMetadata.NativeKind]);
        Assert.All(document.Descendants(), block => Assert.Equal(block.Text, document.Text.Substring(block.TextStart, block.TextLength)));
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void NullCustomTextPreservesTableTabsCellPositionsAndAlignments()
    {
        var parser = new MarkdownParser(converters:
        [new MarkdownNodeConverterRegistration<Table>(_ => new("table")),
         new MarkdownNodeConverterRegistration<TableRow>(_ => new("row")),
         new MarkdownNodeConverterRegistration<TableCell>(_ => new("cell")
         { Metadata = ImmutableDictionary<string, string>.Empty.Add("column", "broken").Add("alignment", "broken") })]);
        var document = parser.Parse("| A | B |\n| :--- | ---: |\n| **one** | two |");
        var table = Assert.Single(document.Blocks);
        Assert.Equal("A\tB\none\ttwo", document.Text);
        Assert.Equal("Table", table.Metadata[MarkdownCustomNodeMetadata.NativeKind]);
        Assert.Equal(MarkdownBlockKind.Table, table.SemanticKind);
        Assert.All(table.Children, row => Assert.Equal("TableRow", row.Metadata[MarkdownCustomNodeMetadata.NativeKind]));
        Assert.Equal("1", table.Children[1].Children[1].Metadata["column"]);
        Assert.Equal("right", table.Children[1].Children[1].Metadata["alignment"]);
        Assert.Equal("TableCell", table.Children[1].Children[1].Metadata[MarkdownCustomNodeMetadata.NativeKind]);
        Assert.All(document.Descendants(), block => Assert.Equal(block.Text, document.Text.Substring(block.TextStart, block.TextLength)));
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void NullCustomTextKeepsCodeContentButReplacementTextHasNoNativeKind()
    {
        var parser = new MarkdownParser(converters:
        [new MarkdownNodeConverterRegistration<FencedCodeBlock>(_ => new("block.code")),
         new MarkdownNodeConverterRegistration<CodeInline>(_ => new("inline.code"))]);
        var document = parser.Parse("` inline `\n\n```cs title\n  code\t \n```");
        Assert.Equal("inline\n  code\t \n", document.Text);
        Assert.Equal("Code", document.Blocks[0].Inlines[0].Metadata[MarkdownCustomNodeMetadata.NativeKind]);
        Assert.Equal(MarkdownInlineKind.Code, document.Blocks[0].Inlines[0].SemanticKind);
        Assert.Equal("Code", document.Blocks[1].Metadata[MarkdownCustomNodeMetadata.NativeKind]);
        Assert.Equal("cs title", document.Blocks[1].Metadata["info"]);
        Assert.Empty(document.Diagnostics);
        var replaced = new MarkdownParser(converters:
            [new MarkdownNodeConverterRegistration<FencedCodeBlock>(_ => new("replacement", "summary"))]).Parse("```cs\ncode\n```");
        Assert.Equal("summary", replaced.Text);
        Assert.False(replaced.Blocks[0].Metadata.ContainsKey(MarkdownCustomNodeMetadata.NativeKind));
        Assert.Equal(MarkdownBlockKind.Custom, replaced.Blocks[0].SemanticKind);
        Assert.Empty(replaced.Blocks[0].Children);
    }

    [Fact]
    public void ReplacingListItemContentKeepsTheParentsMarkerAndOriginalSourceSpan()
    {
        const string source = "8. first\n9. second";
        var parser = new MarkdownParser(converters:
            [new MarkdownNodeConverterRegistration<ListItemBlock>(_ => new("summary", "item"))]);
        var document = parser.Parse(source);
        Assert.Equal("8. item\n9. item", document.Text);
        Assert.All(document.Blocks[0].Children, item =>
        {
            Assert.Equal(MarkdownBlockKind.Custom, item.Kind);
            Assert.Equal(MarkdownBlockKind.Custom, item.SemanticKind);
            Assert.Empty(item.Children);
            Assert.False(item.Metadata.ContainsKey(MarkdownCustomNodeMetadata.NativeKind));
            Assert.StartsWith(item.Metadata["marker"], item.Text);
            Assert.Equal(item.Text, document.Text.Substring(item.TextStart, item.TextLength));
            Assert.Equal(item.Text, new MarkdownSelection(item.TextStart, item.TextStart + item.TextLength).GetText(document));
            Assert.Contains(item.Metadata["marker"], source.Substring(item.SourceStart, item.SourceLength));
        });
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void ReplacingTableCellContentKeepsParentTabsAndIndependentSourceCopyRanges()
    {
        const string source = "| A | B |\n| --- | --- |\n| one | two |";
        var parser = new MarkdownParser(converters:
            [new MarkdownNodeConverterRegistration<TableCell>(_ => new("cell", "summary")
            { Metadata = ImmutableDictionary<string, string>.Empty.Add("column-span", "99").Add("row-span", "99") })]);
        var document = parser.Parse(source);
        Assert.Equal("summary\tsummary\nsummary\tsummary", document.Text);
        Assert.All(document.Blocks[0].Children.SelectMany(row => row.Children), cell =>
        {
            Assert.Equal(MarkdownBlockKind.Custom, cell.Kind);
            Assert.Equal("summary", cell.Text);
            Assert.Empty(cell.Children);
            Assert.False(cell.Metadata.ContainsKey(MarkdownCustomNodeMetadata.NativeKind));
            Assert.Equal("1", cell.Metadata["column-span"]);
            Assert.Equal("1", cell.Metadata["row-span"]);
            Assert.Equal(cell.Text, document.Text.Substring(cell.TextStart, cell.TextLength));
            Assert.Equal(cell.Text, new MarkdownSelection(cell.TextStart, cell.TextStart + cell.TextLength).GetText(document));
            Assert.DoesNotContain("summary", source.Substring(cell.SourceStart, cell.SourceLength));
        });
        Assert.Empty(document.Diagnostics);
        const string spanningSource = "+----------+----------+\n| left     | right    |\n+==========+==========+\n| span     | A        |\n+          +----------+\n|          | B        |\n+----------+----------+";
        var spanning = parser.Parse(spanningSource);
        var span = spanning.Blocks[0].Children[1].Children[0];
        Assert.Equal("summary\tsummary\nsummary\tsummary\n\tsummary", spanning.Text);
        Assert.Equal("2", span.Metadata["row-span"]);
        Assert.Equal("1", spanning.Blocks[0].Children[2].Children[0].Metadata["column"]);
        Assert.Equal("summary", new MarkdownSelection(span.TextStart, span.TextStart + span.TextLength).GetText(spanning));
        Assert.Contains("span", spanningSource.Substring(span.SourceStart, span.SourceLength));
        const string mergedSource = "+---------+---------+\n| A       | B       |\n+=========+=========+\n| merged            |\n+-------------------+";
        var merged = parser.Parse(mergedSource);
        Assert.Equal("summary\tsummary\nsummary\t", merged.Text);
        Assert.Equal("2", merged.Blocks[0].Children[1].Children[0].Metadata["column-span"]);
        Assert.Empty(spanning.Diagnostics);
        Assert.Empty(merged.Diagnostics);
    }

    [Fact]
    public void NullCustomTextPreservesLinkAndImageSemanticsAndDestinations()
    {
        var parser = new MarkdownParser(converters:
        [new MarkdownNodeConverterRegistration<LinkInline>(_ => new("link.or.image")
         { Metadata = ImmutableDictionary<string, string>.Empty.Add("url", "broken").Add("title", "broken") })]);
        var document = parser.Parse("[text](target.md \"title\") ![alt **bold**](pic.png)");
        var link = document.Blocks[0].Inlines.Single(inline => inline.SemanticKind == MarkdownInlineKind.Link);
        var image = document.Blocks[0].Inlines.Single(inline => inline.SemanticKind == MarkdownInlineKind.Image);
        Assert.Equal("text alt bold", document.Text);
        Assert.Equal(MarkdownInlineKind.Custom, link.Kind);
        Assert.Equal(MarkdownInlineKind.Custom, image.Kind);
        Assert.Equal("target.md", link.Metadata["url"]);
        Assert.Equal("title", link.Metadata["title"]);
        Assert.Equal("pic.png", image.Metadata["url"]);
        Assert.Contains(image.Children, inline => inline.Kind == MarkdownInlineKind.Strong);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void UnknownChildlessContainerRetainsSourceWithoutInventingNativeSemantics()
    {
        var parser = new MarkdownParser(builder => builder.UseCustomContainers(),
            [new MarkdownNodeConverterRegistration<CustomContainer>(_ => new("empty.container"))]);
        const string source = "::: note\n:::";
        var document = parser.Parse(source);
        var block = Assert.Single(document.Blocks);
        Assert.Equal(MarkdownBlockKind.Custom, block.Kind);
        Assert.Equal(source, block.Text);
        Assert.Empty(block.Children);
        Assert.False(block.Metadata.ContainsKey(MarkdownCustomNodeMetadata.NativeKind));
        Assert.Equal(MarkdownBlockKind.Custom, block.SemanticKind);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void EmptyNativeFootnoteGroupDoesNotAddASeparatorAfterVisibleContent()
    {
        var document = new MarkdownParser().Parse("visible\n\n[^unused]: unused note\n\n    another paragraph");
        Assert.Equal("visible", document.Text);
        Assert.Equal(MarkdownBlockKind.Paragraph, Assert.Single(document.Blocks).Kind);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void EmptyFootnoteGroupCanHaveExplicitCustomCopyText()
    {
        var parser = new MarkdownParser(converters:
            [new MarkdownNodeConverterRegistration<FootnoteGroup>(_ => new("empty.notes", "no notes"))]);
        var document = parser.Parse("[^unused]: unused note\n\n    another paragraph");
        var block = Assert.Single(document.Blocks);
        Assert.Equal("no notes", document.Text);
        Assert.Equal(MarkdownBlockKind.Custom, block.Kind);
        Assert.False(block.Metadata.ContainsKey(MarkdownCustomNodeMetadata.NativeKind));
        Assert.Empty(document.Diagnostics);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyFootnoteGroupConverterCanDeclineOrDeclareARendererAndRunsOnlyOnce(bool handle)
    {
        var calls = 0;
        var parser = new MarkdownParser(converters:
        [new MarkdownNodeConverterRegistration<FootnoteGroup>(_ =>
        {
            calls++;
            return handle ? new("empty.notes") : null;
        })]);
        var document = parser.Parse("[^unused]: unused note\n\n    another paragraph");
        Assert.Equal(1, calls);
        Assert.Empty(document.Text);
        if (handle)
        {
            var block = Assert.Single(document.Blocks);
            Assert.Equal(MarkdownBlockKind.Custom, block.Kind);
            Assert.Equal(MarkdownBlockKind.FootnoteGroup, block.SemanticKind);
            Assert.Empty(block.Children);
        }
        else Assert.Empty(document.Blocks);
        Assert.Empty(document.Diagnostics);
    }

    [Theory]
    [InlineData("Custom")]
    [InlineData("NotAKind")]
    [InlineData("4")]
    [InlineData("list")]
    [InlineData("")]
    public void SemanticKindRejectsMissingOrNonCanonicalNativeKind(string name)
    {
        var metadata = ImmutableDictionary<string, string>.Empty.Add(MarkdownCustomNodeMetadata.NativeKind, name);
        var block = new MarkdownBlock(1, MarkdownBlockKind.Custom, 0, 0, 0, 0, string.Empty, [], [], metadata);
        var inline = new MarkdownInline(MarkdownInlineKind.Custom, 0, 0, 0, 0, string.Empty, [], metadata);
        Assert.Equal(MarkdownBlockKind.Custom, block.SemanticKind);
        Assert.Equal(MarkdownInlineKind.Custom, inline.SemanticKind);
        Assert.Equal(MarkdownBlockKind.Paragraph, (block with { Kind = MarkdownBlockKind.Paragraph }).SemanticKind);
        Assert.Equal(MarkdownInlineKind.Text, (inline with { Kind = MarkdownInlineKind.Text }).SemanticKind);
    }
}
