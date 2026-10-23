using System.Collections.Immutable;
using System.Text.Json;
using Markdig.Syntax.Inlines;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Core.Tests;

public sealed class MarkdownProjectionCopyTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public void FenceInfoAndBlankCodeLinesKeepTheirProjectionAndOriginalSource(string newline)
    {
        var source = "```csharp   title=\"a b\"\tx" + newline + "  α\t  " + newline + newline + "```";
        var document = new MarkdownParser().Parse(source);
        var code = Assert.Single(document.Blocks);
        Assert.Equal("csharp   title=\"a b\"\tx", code.Metadata["info"]);
        Assert.Equal("  α\t  \n\n", code.Text);
        Assert.Equal(code.Text, document.Text);
        Assert.Equal(source, document.Source);
        Assert.Equal(0, code.SourceStart);
        Assert.Equal(source.Length, code.SourceLength);
        AssertProjectionRanges(document);
    }

    [Theory]
    [InlineData("```csharp   title=\"a b\"\tx", "csharp   title=\"a b\"\tx", "", 4)]
    [InlineData("~~~lang\r\n  α\t\r\n", "lang", "  α\t\n", 4)]
    [InlineData("```\n```", "", "", null)]
    [InlineData("```cs\r\n```", "cs", "", null)]
    [InlineData("~~~~ csharp  title\t=1\n\n~~~~", "csharp  title\t=1", "\n", null)]
    public void UnclosedAndEmptyFencesDoNotInventCodeOrDropInfo(string source, string info, string text, int? sourceLength)
    {
        var document = new MarkdownParser().Parse(source);
        var code = Assert.Single(document.Blocks);
        Assert.Equal(MarkdownBlockKind.Code, code.Kind);
        Assert.Equal(info, code.Metadata["info"]);
        Assert.Equal(text, document.Text);
        Assert.Equal(0, code.SourceStart);
        // Preserve Markdig's source span even when an unclosed opening line's End
        // stops at its first info character; complete info is independent metadata.
        Assert.Equal(sourceLength ?? source.Length, code.SourceLength);
        AssertProjectionRanges(document);
    }

    [Theory]
    [InlineData("literal", "sentinel")]
    [InlineData("entity", "&amp;")]
    public void ADefaultAstSliceRemainsEmptyWithoutChangingItsSourceSpan(string kind, string source)
    {
        var parser = new MarkdownParser(builder => builder.DocumentProcessed += document =>
        {
            var paragraph = (Markdig.Syntax.ParagraphBlock)document[0];
            if (kind == "literal") ((LiteralInline)paragraph.Inline!.FirstChild!).Content = default;
            else ((HtmlEntityInline)paragraph.Inline!.FirstChild!).Transcoded = default;
        });
        var document = parser.Parse(source);
        var block = Assert.Single(document.Blocks);
        var inline = Assert.Single(block.Inlines);
        Assert.Empty(document.Text);
        Assert.Equal(source, document.Source);
        Assert.Equal(0, inline.SourceStart);
        Assert.Equal(source.Length, inline.SourceLength);
        AssertProjectionRanges(document);
    }

    [Fact]
    public void AnOffsetAstSliceCopiesOnlyItsDeclaredContent()
    {
        var parser = new MarkdownParser(builder => builder.DocumentProcessed += document =>
            ((LiteralInline)((Markdig.Syntax.ParagraphBlock)document[0]).Inline!.FirstChild!).Content =
                new Markdig.Helpers.StringSlice("beforeafter", 2, 4));
        var document = parser.Parse("sentinel");
        Assert.Equal("for", document.Text);
        var inline = Assert.Single(Assert.Single(document.Blocks).Inlines);
        Assert.Equal(0, inline.SourceStart);
        Assert.Equal(8, inline.SourceLength);
        Assert.Equal(3, inline.TextLength);
        AssertProjectionRanges(document);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedProjectionCandidatesKeepSeparatorsCustomSemanticsAndEverySplit(bool custom)
    {
        const string source = "> **α &amp;**\r\n\r\n9. first\r\n10. second\r\n\r\n| A | B |\r\n| --- | --- |\r\n| one | **two** |\r\n\r\n```cs title\r\n \tcode  \r\n\r\n```";
        var parser = new MarkdownParser(converters: custom
            ? [new MarkdownNodeConverterRegistration<EmphasisInline>(_ => new("span.same"))] : []);
        var expected = parser.Parse(source);
        Assert.Equal("α &\n9. first\n10. second\nA\tB\none\ttwo\n \tcode  \n\n", expected.Text);
        AssertProjectionRanges(expected);
        var canonical = Canonical(expected);
        for (var split = 0; split <= source.Length; split++)
        {
            var prefix = parser.Parse(source[..split]);
            var final = parser.Parse(source, prefix);
            Assert.Equal(canonical, Canonical(final));
            AssertProjectionRanges(final);
        }
    }

    private static void AssertProjectionRanges(MarkdownDocumentSnapshot document)
    {
        foreach (var block in document.Descendants())
        {
            Assert.Equal(block.Text, document.Text.Substring(block.TextStart, block.TextLength));
            foreach (var inline in block.Inlines) AssertInline(document, inline);
        }
    }
    private static void AssertInline(MarkdownDocumentSnapshot document, MarkdownInline inline)
    {
        Assert.Equal(inline.Text, document.Text.Substring(inline.TextStart, inline.TextLength));
        foreach (var child in inline.Children) AssertInline(document, child);
    }
    private static string Canonical(MarkdownDocumentSnapshot document) => JsonSerializer.Serialize(document with
    { Blocks = document.Blocks.Select(StripIdentity).ToImmutableArray() });
    private static MarkdownBlock StripIdentity(MarkdownBlock block) => block with
    { Id = 0, Children = block.Children.Select(StripIdentity).ToImmutableArray() };
}
