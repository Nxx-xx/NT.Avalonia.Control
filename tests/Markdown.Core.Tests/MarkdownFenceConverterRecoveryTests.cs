using Markdig.Syntax;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Core.Tests;

public sealed class MarkdownFenceConverterRecoveryTests
{
    [Theory]
    [InlineData("```csharp title", "```csharp title", 0)]
    [InlineData("```cs title\n code\n", "```cs title\n code\n", 0)]
    [InlineData("```cs title\r\n  code\r\n\r\n", "```cs title\r\n  code\r\n\r\n", 0)]
    [InlineData("  ~~~cs title\r\n   code  \r\n\r\n", "~~~cs title\r\n   code  \r\n\r\n", 2)]
    public void UnclosedFenceContextAndFailurePreserveCompleteOwnedSource(
        string source, string expectedFragment, int expectedStart)
    {
        var document = ParseFailure(source, expectedFragment, expectedStart, 4);
        Assert.Equal(Normalize(expectedFragment), document.Text);
        Assert.Single(document.Blocks);
    }

    [Theory]
    [InlineData("> ```cs title\n> code\n\noutside", "```cs title\n> code\n", 2, "outside",
        "```cs title\n> code\n\noutside")]
    [InlineData("- first\n  - ```cs title\r\n    code\r\n  - next", "```cs title\r\n    code\r\n", 12, "next",
        "- first\n- ```cs title\n    code\n\n- next")]
    public void UnclosedNestedFenceRecoveryStopsBeforeAdjacentSibling(
        string source, string expectedFragment, int expectedStart, string siblingText, string expectedText)
    {
        var document = ParseFailure(source, expectedFragment, expectedStart, 4);
        Assert.Equal(expectedText, document.Text);
        var sibling = Assert.Single(document.Descendants(), block =>
            block.Kind == MarkdownBlockKind.Paragraph && block.Text == siblingText);
        Assert.Equal(siblingText, source.Substring(sibling.SourceStart, sibling.SourceLength));
        var fallback = Assert.Single(document.Descendants(), block => block.Kind == MarkdownBlockKind.Unsupported);
        Assert.DoesNotContain(siblingText, fallback.Text);
    }

    [Fact]
    public void ClosedFenceContextAndFailureContinueToUseOriginalAstSpan()
    {
        const string fragment = "```cs title\r\n  code\r\n```";
        var document = ParseFailure(fragment + "\r\n\r\nafter", fragment, 0, fragment.Length, 3);
        Assert.Equal(Normalize(fragment) + "\nafter", document.Text);
        Assert.Equal(2, document.Blocks.Length);
        Assert.Equal("after", document.Blocks[1].Text);
    }

    [Fact]
    public void GridCellFenceKeepsAstRangeRatherThanRecoveringAdjacentCellSource()
    {
        const string source = "+------------+------------+\n| A          | B          |\n+============+============+\n| ```cs      | right      |\n| code       | cell       |\n+------------+------------+";
        foreach (var expectedInfo in new[] { "cs", "cs title" })
        {
            var input = expectedInfo == "cs" ? source : source.Replace("```cs      ", "```cs title", StringComparison.Ordinal);
            var native = new MarkdownParser().Parse(input);
            AssertGridProjection(native, expectedInfo);
            Assert.Empty(native.Diagnostics);
            foreach (var invalidOutput in new[] { false, true })
            {
                (string Text, int Start, int Length)? observed = null;
                var parser = new MarkdownParser(converters:
                [new MarkdownNodeConverterRegistration<FencedCodeBlock>(context =>
                {
                    observed = (context.SourceText, context.SourceStart, context.SourceLength);
                    if (invalidOutput) return new("", "must not be committed");
                    throw new InvalidOperationException("secret converter details");
                })]);
                var document = parser.Parse(input);
                Assert.Equal(("```c", 86, 4), observed);
                AssertGridProjection(document, expectedInfo);
                var diagnostic = Assert.Single(document.Diagnostics);
                Assert.Equal("MDNODECONV", diagnostic.Code);
                Assert.Equal(86, diagnostic.SourceStart);
                Assert.Equal(4, diagnostic.SourceLength);
                Assert.Contains("native code text is preserved", diagnostic.Message);
                Assert.DoesNotContain("secret", diagnostic.Message);
            }
        }
    }

    private static void AssertGridProjection(MarkdownDocumentSnapshot document, string expectedInfo)
    {
        Assert.Equal("A\tB\ncode\n\tright cell", document.Text);
        var code = Assert.Single(document.Descendants(), block => block.Kind == MarkdownBlockKind.Code);
        Assert.Equal("code\n", code.Text);
        Assert.Equal("cs", code.Metadata["language"]);
        Assert.Equal(expectedInfo, code.Metadata["info"]);
        Assert.Equal(86, code.SourceStart);
        Assert.Equal(4, code.SourceLength);
        Assert.False(code.Metadata.ContainsKey(MarkdownCustomNodeMetadata.RendererKey));
        var rightCell = Assert.Single(document.Descendants(), block =>
            block.Kind == MarkdownBlockKind.TableCell && block.Text == "right cell");
        Assert.Equal("right cell", Assert.Single(rightCell.Children).Text);
        Assert.Equal(rightCell.Text, document.Text.Substring(rightCell.TextStart, rightCell.TextLength));
    }

    private static MarkdownDocumentSnapshot ParseFailure(
        string source, string expectedFragment, int expectedStart, int expectedAstLength, int expectedClosing = 0)
    {
        // Capture pure values while the callback is active, without retaining its context or AST.
        (string Source, string Text, int Start, int Length, int AstStart, int AstLength, int Closing)? observed = null;
        var parser = new MarkdownParser(converters:
        [new MarkdownNodeConverterRegistration<FencedCodeBlock>(context =>
        {
            observed = (context.Source, context.SourceText, context.SourceStart, context.SourceLength,
                context.Node.Span.Start, context.Node.Span.End - context.Node.Span.Start + 1, context.Node.ClosingFencedCharCount);
            throw new InvalidOperationException("secret converter details");
        })]);
        var document = parser.Parse(source);
        var capture = Assert.IsType<(string Source, string Text, int Start, int Length, int AstStart, int AstLength, int Closing)>(observed);
        Assert.Equal(source, capture.Source);
        Assert.Equal(expectedFragment, capture.Text);
        Assert.Equal(expectedStart, capture.Start);
        Assert.Equal(expectedFragment.Length, capture.Length);
        Assert.Equal(capture.Length, capture.Text.Length);
        Assert.Equal(capture.Text, source.Substring(capture.Start, capture.Length));
        Assert.Equal(expectedStart, capture.AstStart);
        Assert.Equal(expectedAstLength, capture.AstLength);
        Assert.Equal(expectedClosing, capture.Closing);

        var fallback = Assert.Single(document.Descendants(), block => block.Kind == MarkdownBlockKind.Unsupported);
        Assert.Equal(Normalize(expectedFragment), fallback.Text);
        Assert.Equal(expectedStart, fallback.SourceStart);
        Assert.Equal(expectedAstLength, fallback.SourceLength);
        Assert.Equal(fallback.Text, document.Text.Substring(fallback.TextStart, fallback.TextLength));
        Assert.Equal(fallback.Text, new MarkdownSelection(fallback.TextStart, fallback.TextStart + fallback.TextLength).GetText(document));
        Assert.Empty(fallback.Children);
        Assert.Empty(fallback.Inlines);
        Assert.False(fallback.Metadata.ContainsKey(MarkdownCustomNodeMetadata.RendererKey));
        var diagnostic = Assert.Single(document.Diagnostics);
        Assert.Equal("MDNODECONV", diagnostic.Code);
        Assert.Equal(expectedStart, diagnostic.SourceStart);
        Assert.Equal(expectedAstLength, diagnostic.SourceLength);
        Assert.DoesNotContain("secret", diagnostic.Message);
        return document;
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
