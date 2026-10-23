using Markdig.Syntax.Inlines;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Core.Tests;

public sealed class MarkdownNestedConverterTests
{
    [Fact]
    public void NestedNullTextConvertersRetainEmphasisLinkAndCodeSemantics()
    {
        var parser = new MarkdownParser(converters:
        [new MarkdownNodeConverterRegistration<EmphasisInline>(context => new(context.Node.DelimiterCount == 2 ? "strong" : "emphasis")),
         new MarkdownNodeConverterRegistration<LinkInline>(_ => new("link")),
         new MarkdownNodeConverterRegistration<CodeInline>(_ => new("code"))]);
        var document = parser.Parse("before **outer *inner* [link](assets/a.md) `code`** after");
        var outer = document.Blocks[0].Inlines.Single(inline => inline.SemanticKind == MarkdownInlineKind.Strong);
        Assert.Equal(MarkdownInlineKind.Custom, outer.Kind);
        Assert.Equal("before outer inner link code after", document.Text);
        Assert.Contains(outer.Children, inline => inline.Kind == MarkdownInlineKind.Custom && inline.SemanticKind == MarkdownInlineKind.Emphasis);
        var link = outer.Children.Single(inline => inline.SemanticKind == MarkdownInlineKind.Link);
        Assert.Equal("assets/a.md", link.Metadata["url"]);
        Assert.Equal(MarkdownInlineKind.Custom, link.Kind);
        Assert.Contains(outer.Children, inline => inline.Kind == MarkdownInlineKind.Custom && inline.SemanticKind == MarkdownInlineKind.Code);
        AssertInlineRanges(document, outer);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void InvalidInnerConverterFallsBackOnlyThatSubtreeWithoutPartialReplacementText()
    {
        var parser = new MarkdownParser(converters:
        [new MarkdownNodeConverterRegistration<EmphasisInline>(context =>
            context.Node.DelimiterCount == 2 ? new("strong") : new("invalid key", "MUST NOT APPEAR"))]);
        const string source = "before **outer *inner* end** after";
        var document = parser.Parse(source);
        var outer = document.Blocks[0].Inlines.Single(inline => inline.SemanticKind == MarkdownInlineKind.Strong);
        var failed = outer.Children.Single(inline => inline.Kind == MarkdownInlineKind.Unsupported);
        Assert.Equal("before outer *inner* end after", document.Text);
        Assert.Equal("*inner*", failed.Text);
        Assert.Equal("*inner*", source.Substring(failed.SourceStart, failed.SourceLength));
        Assert.Empty(failed.Children);
        Assert.False(failed.Metadata.ContainsKey(MarkdownCustomNodeMetadata.RendererKey));
        Assert.False(failed.Metadata.ContainsKey(MarkdownCustomNodeMetadata.NativeKind));
        Assert.Equal("MDNODECONV", Assert.Single(document.Diagnostics).Code);
        AssertInlineRanges(document, outer);
    }

    [Fact]
    public void FailingOuterConverterDoesNotRunOrPublishAnyChildConversions()
    {
        var innerCalls = 0;
        var parser = new MarkdownParser(converters:
        [new MarkdownNodeConverterRegistration<EmphasisInline>(context =>
        {
            if (context.Node.DelimiterCount == 2) throw new FormatException("private content");
            innerCalls++;
            return new("inner", "REPLACED");
        })]);
        const string source = "before **outer *inner* end** after";
        var document = parser.Parse(source);
        var failed = document.Blocks[0].Inlines.Single(inline => inline.Kind == MarkdownInlineKind.Unsupported);
        Assert.Equal(0, innerCalls);
        Assert.Equal(source, document.Text);
        Assert.Equal("**outer *inner* end**", failed.Text);
        Assert.Empty(failed.Children);
        Assert.False(failed.Metadata.ContainsKey(MarkdownCustomNodeMetadata.NativeKind));
        var diagnostic = Assert.Single(document.Diagnostics);
        Assert.Equal("MDNODECONV", diagnostic.Code);
        Assert.DoesNotContain("private", diagnostic.Message);
        AssertInlineRanges(document, failed);
    }

    [Fact]
    public void ReplacingAnOuterContainerMakesItALeafAndDoesNotInvokeChildConverters()
    {
        var innerCalls = 0;
        var parser = new MarkdownParser(converters:
        [new MarkdownNodeConverterRegistration<EmphasisInline>(context =>
        {
            if (context.Node.DelimiterCount == 2) return new("summary", "SUMMARY");
            innerCalls++;
            return new("inner");
        })]);
        var document = parser.Parse("before **outer *inner* end** after");
        var custom = document.Blocks[0].Inlines.Single(inline => inline.Kind == MarkdownInlineKind.Custom);
        Assert.Equal(0, innerCalls);
        Assert.Equal("before SUMMARY after", document.Text);
        Assert.Equal(MarkdownInlineKind.Custom, custom.SemanticKind);
        Assert.Empty(custom.Children);
        Assert.False(custom.Metadata.ContainsKey(MarkdownCustomNodeMetadata.NativeKind));
        AssertInlineRanges(document, custom);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void NestedUnicodeReplacementKeepsIndependentSourceAndGlobalCopyRanges()
    {
        var parser = new MarkdownParser(converters:
        [new MarkdownNodeConverterRegistration<EmphasisInline>(_ => new("emphasis")),
         new MarkdownNodeConverterRegistration<LiteralInline>(context =>
             context.Node.Content.ToString() == "inner" ? new("unicode", "e\u0301😀") : null)]);
        var document = parser.Parse("**before *inner* end**");
        var outer = Assert.Single(document.Blocks[0].Inlines);
        var inner = outer.Children.Single(inline => inline.SemanticKind == MarkdownInlineKind.Emphasis);
        var unicode = Assert.Single(inner.Children);
        Assert.Equal("before e\u0301😀 end", document.Text);
        Assert.Equal("inner", document.Source.Substring(unicode.SourceStart, unicode.SourceLength));
        Assert.Equal("e\u0301😀", unicode.Text);
        Assert.NotEqual(unicode.SourceLength, unicode.TextLength);
        Assert.Equal(unicode.Text, new MarkdownSelection(unicode.TextStart + 1, unicode.TextStart + 3).GetText(document));
        AssertInlineRanges(document, outer);
        Assert.Empty(document.Diagnostics);
    }

    private static void AssertInlineRanges(MarkdownDocumentSnapshot document, MarkdownInline inline)
    {
        Assert.Equal(inline.Text, document.Text.Substring(inline.TextStart, inline.TextLength));
        Assert.InRange(inline.SourceStart, 0, document.Source.Length);
        Assert.InRange(inline.SourceLength, 0, document.Source.Length - inline.SourceStart);
        foreach (var child in inline.Children)
        {
            Assert.InRange(child.TextStart, inline.TextStart, inline.TextStart + inline.TextLength);
            Assert.InRange(child.TextLength, 0, inline.TextStart + inline.TextLength - child.TextStart);
            AssertInlineRanges(document, child);
        }
    }
}
