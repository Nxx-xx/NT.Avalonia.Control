using System.Collections.Immutable;
using System.Text.Json;
using Markdig;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Core.Tests;

public sealed class MarkdownParserTests
{
    private readonly MarkdownParser _parser = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EmptyInputHasNoBlocksOrCopyText(string? source)
    {
        var document = _parser.Parse(source);
        Assert.Empty(document.Blocks);
        Assert.Equal(string.Empty, document.Text);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void VisibleTextUsesDecodedEntitiesEscapesAndInlineStyles()
    {
        var document = _parser.Parse("# Hello **world**\n\nA \\*literal\\* &amp; `code` ~~gone~~ ++new++ ==mark== ~sub~ ^sup^.");
        Assert.Equal("Hello world\nA *literal* & code gone new mark sub sup.", document.Text);
        Assert.Equal(MarkdownBlockKind.Heading, document.Blocks[0].Kind);
        Assert.Equal("1", document.Blocks[0].Metadata["level"]);
        Assert.Contains(document.Blocks[1].Inlines, inline => inline.Kind == MarkdownInlineKind.Strike);
        Assert.Contains(document.Blocks[1].Inlines, inline => inline.Kind == MarkdownInlineKind.Insert);
        Assert.Contains(document.Blocks[1].Inlines, inline => inline.Kind == MarkdownInlineKind.Mark);
        Assert.Contains(document.Blocks[1].Inlines, inline => inline.Kind == MarkdownInlineKind.Subscript);
        Assert.Contains(document.Blocks[1].Inlines, inline => inline.Kind == MarkdownInlineKind.Superscript);
    }

    [Fact]
    public void SoftHardAndVisualWrappingHaveDistinctProjectionRules()
    {
        var document = _parser.Parse("soft\nline  \nhard\n\nlast");
        Assert.Equal("soft line\nhard\nlast", document.Text);
        Assert.Equal(new[] { "false", "true" }, document.Blocks[0].Inlines.Where(inline => inline.Kind == MarkdownInlineKind.LineBreak).Select(inline => inline.Metadata["hard"]));
    }

    [Fact]
    public void GlobalSelectionSpansParagraphCodeTableAndFinalBlock()
    {
        var document = _parser.Parse("before **bold**\n\n```cs title\n  x\t  \ny\n```\n\n| A | B |\n| :- | -: |\n| *one* | two |\n\nafter");
        Assert.Equal("before bold\n  x\t  \ny\n\nA\tB\none\ttwo\nafter", document.Text);
        Assert.Equal(document.Text, new MarkdownSelection(0, document.Text.Length).GetText(document));
        var start = document.Text.IndexOf("bold", StringComparison.Ordinal);
        var end = document.Text.IndexOf("after", StringComparison.Ordinal) + "after".Length;
        Assert.Equal("bold\n  x\t  \ny\n\nA\tB\none\ttwo\nafter", new MarkdownSelection(start, end).GetText(document));
        var code = document.Blocks[1];
        Assert.Equal("cs", code.Metadata["language"]);
        Assert.Equal("cs title", code.Metadata["info"]);
        Assert.Equal("  x\t  \ny\n", code.Text);
        Assert.All(document.Descendants(), block => Assert.Equal(block.Text, document.Text.Substring(block.TextStart, block.TextLength)));
    }

    [Fact]
    public void PipeTablePreservesAlignmentsAndInlineCellContent()
    {
        var document = _parser.Parse("| Left | Center | Right |\n| :--- | :---: | ---: |\n| **a** | [b](https://b) | `c` |");
        var table = Assert.Single(document.Blocks);
        Assert.Equal(MarkdownBlockKind.Table, table.Kind);
        Assert.Equal("left,center,right", table.Metadata["alignments"]);
        Assert.Equal("Left\tCenter\tRight\na\tb\tc", document.Text);
        Assert.Equal("true", table.Children[0].Metadata["header"]);
        Assert.Equal("center", table.Children[1].Children[1].Metadata["alignment"]);
        Assert.Equal("https://b", table.Children[1].Children[1].Children[0].Inlines.Single(inline => inline.Kind == MarkdownInlineKind.Link).Metadata["url"]);
    }

    [Fact]
    public void GridTableKeepsMultiParagraphCellsAndSpanRelationships()
    {
        const string source = "+---------+---------+\n| A       | B       |\n+=========+=========+\n| one     | two     |\n|         |         |\n| para    | **bold**|\n+---------+---------+\n| merged            |\n+-------------------+";
        var document = _parser.Parse(source);
        var table = Assert.Single(document.Blocks);
        Assert.Equal(MarkdownBlockKind.Table, table.Kind);
        Assert.Equal(3, table.Children.Length);
        Assert.Equal(2, table.Children[1].Children[0].Children.Length);
        Assert.Equal("2", table.Children[2].Children[0].Metadata["column-span"]);
        Assert.Equal("A\tB\none\npara\ttwo\nbold\nmerged\t", document.Text);
    }

    [Fact]
    public void ListsTasksQuotesAlertsAndFootnotesHaveExplicitCopyMarkers()
    {
        var document = _parser.Parse("3. first\n4. second\n   - nested\n\n- [x] done\n- [ ] open\n\n> quoted\n\n> [!WARNING]\n> careful\n\nref[^a], repeat[^a]\n\n[^a]: footnote");
        Assert.Equal("3. first\n4. second\n- nested\n- [x] done\n- [ ] open\nquoted\nWARNING\ncareful\nref[1], repeat[1]\n[1] footnote", document.Text);
        Assert.Contains(document.Descendants(), block => block.Kind == MarkdownBlockKind.Alert && block.Metadata["kind"] == "WARNING");
        Assert.Equal(2, document.Descendants().SelectMany(block => block.Inlines).Count(inline => inline.Kind == MarkdownInlineKind.FootnoteReference));
    }

    [Fact]
    public void ImagesCopyAltTextAndLinksKeepDestinationsAndTitles()
    {
        var document = _parser.Parse("![an **image**](pic.png \"caption\") [label](target.md \"title\") https://example.com");
        Assert.Equal("an image label https://example.com", document.Text);
        Assert.Equal("pic.png", document.Blocks[0].Inlines[0].Metadata["url"]);
        Assert.Equal("caption", document.Blocks[0].Inlines[0].Metadata["title"]);
        Assert.Equal(MarkdownInlineKind.Image, document.Blocks[0].Inlines[0].Kind);
    }

    [Fact]
    public void HtmlHasLiteralFallbackAndADiagnostic()
    {
        var document = _parser.Parse("<div>raw</div>\n\ninline <b>raw</b>");
        Assert.Equal("<div>raw</div>\ninline <b>raw</b>", document.Text);
        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Code == "MDHTML");
    }

    [Fact]
    public void EmojiAndAllFiveAlertsAreConvertedWithNoUnsupportedNodes()
    {
        var source = string.Join("\n\n", new[] { "NOTE", "TIP", "IMPORTANT", "WARNING", "CAUTION" }.Select(kind => $"> [!{kind}]\n> :smile:"));
        var document = _parser.Parse(source);
        Assert.Equal(5, document.Blocks.Length);
        Assert.All(document.Blocks, block => Assert.Equal(MarkdownBlockKind.Alert, block.Kind));
        Assert.DoesNotContain(":smile:", document.Text);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void UnknownPipelineNodesPreserveTheirSourceAndReportDiagnostic()
    {
        var parser = new MarkdownParser(builder => builder.UseMathematics());
        var document = parser.Parse("math $x^2$ end");
        Assert.Equal("math $x^2$ end", document.Text);
        Assert.Contains(document.Diagnostics, diagnostic => diagnostic.Code == "MDNODE");
    }

    [Fact]
    public void FenceInfoRetainsInteriorWhitespace()
    {
        var document = _parser.Parse("```csharp   title=\"a b\"\tx\ncode\n```");
        Assert.Equal("csharp   title=\"a b\"\tx", document.Blocks[0].Metadata["info"]);
    }

    [Fact]
    public void SourceSpansRemainIndependentOfDecodedCopySpans()
    {
        const string source = "prefix **bold** &amp; [text](uri)";
        var document = _parser.Parse(source);
        var strong = document.Blocks[0].Inlines.Single(inline => inline.Kind == MarkdownInlineKind.Strong);
        Assert.Equal("**bold**", source.Substring(strong.SourceStart, strong.SourceLength));
        Assert.Equal("bold", document.Text.Substring(strong.TextStart, strong.TextLength));
        Assert.NotEqual(strong.SourceLength, strong.TextLength);
    }

    [Theory]
    [InlineData("```cs\nx\n")]
    [InlineData("| A | B |\n| --- |")]
    [InlineData("- first\n  continued")]
    [InlineData("[earlier][r]\n\n[r]: https://later.example")]
    [InlineData("ref[^a]\n\n[^a]: final")]
    [InlineData("a\r\n\r\n😀 &amp;")]
    public void EverySingleSplitAndRandomMultiChunkHasEquivalentFinalSnapshot(string source)
    {
        var expected = Normalize(_parser.Parse(source));
        for (var split = 0; split <= source.Length; split++)
        {
            var prefix = _parser.Parse(source[..split]);
            Assert.Equal(expected, Normalize(_parser.Parse(prefix.Source + source[split..], prefix)));
        }
        var random = new Random(314159);
        for (var pass = 0; pass < 12; pass++)
        {
            var snapshot = MarkdownDocumentSnapshot.Empty;
            for (var position = 0; position < source.Length;)
            {
                var count = Math.Min(random.Next(1, 8), source.Length - position);
                snapshot = _parser.Parse(snapshot.Source + source.Substring(position, count), snapshot);
                position += count;
            }
            Assert.Equal(expected, Normalize(snapshot));
        }
    }

    [Fact]
    public void LateReferenceDefinitionsUpdateEarlierDestinations()
    {
        var prefix = _parser.Parse("[earlier][r]\n\n");
        var final = _parser.Parse(prefix.Source + "[r]: https://later.example \"title\"", prefix);
        Assert.Equal("earlier", final.Text);
        Assert.Equal("https://later.example", Assert.Single(final.Blocks[0].Inlines).Metadata["url"]);
        Assert.NotEqual(prefix.Blocks[0].Inlines[0].Kind, final.Blocks[0].Inlines[0].Kind);
    }

    [Fact]
    public void CoreDataContainsNoAvaloniaOrMarkdigObjects()
    {
        var root = FindRoot();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src", "NT.AvaloniaControl.Markdown", "Core"), "*.cs", SearchOption.AllDirectories))
        {
            var code = File.ReadAllText(file);
            Assert.DoesNotContain("using Avalonia", code);
            Assert.DoesNotContain("using Markdig", code);
        }
        Assert.DoesNotContain(typeof(MarkdownDocumentSnapshot).Assembly.GetReferencedAssemblies(), name => name.Name == "NT.AvaloniaControl.Markdown.Themes");
    }

    private static string Normalize(MarkdownDocumentSnapshot document) => JsonSerializer.Serialize(document with
    {
        Blocks = document.Blocks.Select(StripIdentity).ToImmutableArray()
    });
    private static MarkdownBlock StripIdentity(MarkdownBlock block) => block with { Id = 0, Children = block.Children.Select(StripIdentity).ToImmutableArray() };
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NT.AvaloniaControl.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
