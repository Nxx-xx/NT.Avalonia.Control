using System.Collections.Immutable;
using System.Text.Json;
using Markdig;
using Markdig.Extensions.CustomContainers;
using Markdig.Extensions.Mathematics;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Core.Tests;

public sealed class MarkdownNodeConverterTests
{
    [Fact]
    public void CustomInlineHasIndependentSourceAndCopySpansAndPurePayload()
    {
        const string source = "before $x^2$ after";
        (string Source, string SourceText, int SourceStart, int SourceLength)? observed = null;
        var parser = new MarkdownParser(builder => builder.UseMathematics(),
        [new MarkdownNodeConverterRegistration<MathInline>(context =>
        {
            observed = (context.Source, context.SourceText, context.SourceStart, context.SourceLength);
            return new("math.text", "math:" + context.SourceText[1..^1])
            {
                StyleClasses = ["formula", "formula", "accent"],
                Metadata = ImmutableDictionary<string, string>.Empty.Add("label", "square")
            };
        })]);
        var document = parser.Parse(source);
        var custom = document.Blocks[0].Inlines.Single(inline => inline.Kind == MarkdownInlineKind.Custom);
        var capture = Assert.IsType<(string Source, string SourceText, int SourceStart, int SourceLength)>(observed);
        Assert.Equal(source, capture.Source);
        Assert.Equal("$x^2$", capture.SourceText);
        Assert.Equal(7, capture.SourceStart);
        Assert.Equal(5, capture.SourceLength);
        Assert.Equal("before math:x^2 after", document.Text);
        Assert.Equal("math:x^2", custom.Text);
        Assert.Equal(custom.Text, document.Text.Substring(custom.TextStart, custom.TextLength));
        Assert.Equal("$x^2$", source.Substring(custom.SourceStart, custom.SourceLength));
        Assert.Equal("math.text", custom.Metadata[MarkdownCustomNodeMetadata.RendererKey]);
        Assert.Equal("formula accent", custom.Metadata[MarkdownCustomNodeMetadata.StyleClasses]);
        Assert.Equal("square", custom.Metadata["label"]);
        Assert.Empty(custom.Children);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void CustomContainerDelegatesChildrenAndGlobalOffsetsToTheLibrary()
    {
        var parser = new MarkdownParser(builder => builder.UseCustomContainers(),
        [new MarkdownNodeConverterRegistration<CustomContainer>(context => new("callout")
        {
            StyleClasses = ["callout"],
            Metadata = ImmutableDictionary<string, string>.Empty.Add("label", context.Node.Info ?? string.Empty)
        })]);
        var document = parser.Parse("before\n\n::: note\nfirst **bold**\n\nsecond [link](https://example.test)\n:::\n\nafter");
        var custom = document.Blocks.Single(block => block.Kind == MarkdownBlockKind.Custom);
        Assert.Equal("first bold\nsecond link", custom.Text);
        Assert.Equal(2, custom.Children.Length);
        Assert.All(custom.Children, child => Assert.Equal(MarkdownBlockKind.Paragraph, child.Kind));
        Assert.Contains(custom.Children[0].Inlines, inline => inline.Kind == MarkdownInlineKind.Strong);
        Assert.Contains(custom.Children[1].Inlines, inline => inline.Kind == MarkdownInlineKind.Link);
        Assert.All(document.Descendants(), block => Assert.Equal(block.Text, document.Text.Substring(block.TextStart, block.TextLength)));
        Assert.Equal(document.Text, new MarkdownSelection(0, document.Text.Length).GetText(document));
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void BuiltInLeafAndInlineContainersCanKeepConvertedChildren()
    {
        var parser = new MarkdownParser(converters:
        [new MarkdownNodeConverterRegistration<ParagraphBlock>(_ => new("paragraph")),
         new MarkdownNodeConverterRegistration<EmphasisInline>(context => context.Node.DelimiterCount == 2 ? new("strong") : null)]);
        var document = parser.Parse("before **bold *nested*** after");
        var block = Assert.Single(document.Blocks);
        Assert.Equal(MarkdownBlockKind.Custom, block.Kind);
        var strong = block.Inlines.Single(inline => inline.Kind == MarkdownInlineKind.Custom);
        Assert.Contains(strong.Children, child => child.Kind == MarkdownInlineKind.Emphasis);
        Assert.Equal("bold nested", strong.Text);
        Assert.Equal("before bold nested after", document.Text);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void RegistrationsUseSuppliedAssignableOrderAndNullDeclines()
    {
        var calls = new List<string>();
        var parser = new MarkdownParser(builder => builder.UseMathematics(),
        [new MarkdownNodeConverterRegistration<Inline>(context => { calls.Add("base:" + context.Node.GetType().Name); return null; }),
         new MarkdownNodeConverterRegistration<MathInline>(_ => { calls.Add("declined"); return null; }),
         new MarkdownNodeConverterRegistration<MathInline>(_ => { calls.Add("chosen"); return new("chosen", "value"); }),
         new MarkdownNodeConverterRegistration<MathInline>(_ => throw new InvalidOperationException("Must not run after a match."))]);
        var document = parser.Parse("$x$");
        Assert.Equal(new[] { "base:MathInline", "declined", "chosen" }, calls);
        Assert.Equal("value", document.Text);
        Assert.Empty(document.Diagnostics);
    }

    [Fact]
    public void ConverterFailureKeepsProjectionAtomicAndDoesNotExposeItsExceptionMessage()
    {
        var parser = new MarkdownParser(builder => builder.UseMathematics(),
        [new MarkdownNodeConverterRegistration<MathInline>(_ => throw new FormatException("secret document contents"))]);
        const string source = "before $x$ after";
        var document = parser.Parse(source);
        Assert.Equal(source, document.Text);
        var fallback = document.Blocks[0].Inlines.Single(inline => inline.Kind == MarkdownInlineKind.Unsupported);
        Assert.Equal("$x$", fallback.Text);
        Assert.False(fallback.Metadata.ContainsKey(MarkdownCustomNodeMetadata.RendererKey));
        var diagnostic = Assert.Single(document.Diagnostics);
        Assert.Equal("MDNODECONV", diagnostic.Code);
        Assert.DoesNotContain("secret", diagnostic.Message);
        Assert.Equal("$x$", source.Substring(diagnostic.SourceStart, diagnostic.SourceLength));
    }

    [Theory]
    [InlineData("empty-renderer")]
    [InlineData("null-renderer")]
    [InlineData("space-renderer")]
    [InlineData("control-renderer")]
    [InlineData("reserved-renderer")]
    [InlineData("reserved-style")]
    [InlineData("reserved-ast")]
    [InlineData("reserved-native")]
    [InlineData("reserved-native-case")]
    [InlineData("reserved-case")]
    [InlineData("empty-metadata-key")]
    [InlineData("trimmed-metadata-key")]
    [InlineData("null-metadata-value")]
    [InlineData("null-metadata")]
    [InlineData("empty-style-class")]
    [InlineData("space-style-class")]
    [InlineData("null-style-class")]
    public void InvalidCustomOutputFallsBackBeforeItsTextCanBeCommitted(string invalid)
    {
        var parser = new MarkdownParser(builder => builder.UseMathematics(),
        [new MarkdownNodeConverterRegistration<MathInline>(_ => InvalidOutput(invalid))]);
        var document = parser.Parse("before $x$ after");
        Assert.Equal("before $x$ after", document.Text);
        Assert.Equal("MDNODECONV", Assert.Single(document.Diagnostics).Code);
        Assert.DoesNotContain(document.Blocks[0].Inlines, inline => inline.Kind == MarkdownInlineKind.Custom);
    }

    [Fact]
    public void EmptyTextEmptyPayloadAndDefaultClassesAreValidAndDifferentFromNullText()
    {
        var parser = new MarkdownParser(builder => builder.UseMathematics(),
        [new MarkdownNodeConverterRegistration<MathInline>(_ => new("empty", string.Empty)
        {
            StyleClasses = default,
            Metadata = ImmutableDictionary<string, string>.Empty.Add("caption", string.Empty)
        })]);
        var document = parser.Parse("left $x$ right");
        Assert.Equal("left  right", document.Text);
        var custom = document.Blocks[0].Inlines.Single(inline => inline.Kind == MarkdownInlineKind.Custom);
        Assert.Equal(0, custom.TextLength);
        Assert.Equal(string.Empty, custom.Metadata[MarkdownCustomNodeMetadata.StyleClasses]);
        Assert.Equal(string.Empty, custom.Metadata["caption"]);
        Assert.Empty(document.Diagnostics);
        var literal = new MarkdownParser(builder => builder.UseMathematics(),
            [new MarkdownNodeConverterRegistration<MathInline>(_ => new("literal"))]).Parse("$x$");
        Assert.Equal("$x$", literal.Text);
        Assert.Empty(literal.Diagnostics);
    }

    [Fact]
    public void CustomReplacementTextNormalizesCrLfBeforeAssigningGlobalRanges()
    {
        var parser = new MarkdownParser(builder => builder.UseMathematics(),
            [new MarkdownNodeConverterRegistration<MathInline>(_ => new("lines", "one\r\ntwo\rthree"))]);
        var document = parser.Parse("a $x$ b");
        var custom = document.Blocks[0].Inlines.Single(inline => inline.Kind == MarkdownInlineKind.Custom);
        Assert.Equal("one\ntwo\nthree", custom.Text);
        Assert.Equal(custom.Text, document.Text.Substring(custom.TextStart, custom.TextLength));
    }

    [Fact]
    public void ParserCapturesRegistrationsAndOptionsWithoutConstructorNullAmbiguity()
    {
        var registrations = new List<MarkdownNodeConverterRegistration>
        { new MarkdownNodeConverterRegistration<ParagraphBlock>(_ => new("captured", "value")) };
        var parser = new MarkdownParser(converters: registrations);
        registrations.Clear();
        Assert.Equal("value", parser.Parse("source").Text);
        Assert.Single(parser.Options.Converters);
        Assert.Equal("source", new MarkdownParser(null).Parse("source").Text);
        Assert.Equal("source", MarkdownParser.FromOptions(new() { Converters = default }).Parse("source").Text);
        Assert.Throws<ArgumentNullException>(() => MarkdownParser.FromOptions(null!));
        Assert.Throws<ArgumentException>(() => new MarkdownParser(converters: new MarkdownNodeConverterRegistration[] { null! }));
        Assert.Throws<ArgumentException>(() => new MarkdownNodeConverterRegistration<MarkdownDocument>(_ => new("root")));
    }

    [Fact]
    public void RendererPayloadAndClassChangesInvalidateTheMatchedBlock()
    {
        const string source = "> stable **text**";
        MarkdownParser Parser(string renderer, string style, string payload) => new(converters:
        [new MarkdownNodeConverterRegistration<QuoteBlock>(_ => new(renderer)
        {
            StyleClasses = [style],
            Metadata = ImmutableDictionary<string, string>.Empty.Add("caption", payload)
        })]);
        var previous = Parser("first", "a", "one").Parse(source);
        foreach (var current in new[] { Parser("second", "a", "one"), Parser("first", "b", "one"), Parser("first", "a", "two") }.Select(parser => parser.Parse(source, previous)))
        {
            Assert.Equal(previous.Blocks[0].Id, current.Blocks[0].Id);
            Assert.Contains(current.Blocks[0].Id, MarkdownDocumentMatcher.GetChanges(previous, current).Changed);
            Assert.Equal(previous.Text, current.Text);
        }
    }

    [Fact]
    public async Task CustomPipelineAndConvertersProduceEquivalentSnapshotsForEveryStreamingSplit()
    {
        var parser = new MarkdownParser(builder => builder.UseCustomContainers().UseMathematics(),
        [new MarkdownNodeConverterRegistration<CustomContainer>(_ => new("container") { StyleClasses = ["custom"] }),
         new MarkdownNodeConverterRegistration<MathInline>(context => new("math", context.SourceText[1..^1]))]);
        const string source = "before\n\n::: note\ncustom $x^2$ **strong**\n\n[late][r]\n:::\n\n[r]: https://example.test/target\n";
        var expected = Canonical(parser.Parse(source));
        for (var split = 0; split <= source.Length; split++)
        {
            MarkdownDocumentSnapshot? applied = null;
            using var scheduler = new MarkdownUpdateScheduler<MarkdownDocumentSnapshot>(
                (request, _) => Task.FromResult(parser.Parse(request.Source)),
                (update, _) => { update.TryCommit(() => applied = update.Snapshot); return Task.FromResult(MarkdownApplyResult.Applied); }, TimeSpan.Zero);
            var session = scheduler.BeginStreaming();
            session.Append(source[..split]); session.Append(source.AsMemory(split));
            Assert.Equal(MarkdownUpdateStatus.Completed, (await session.CompleteAsync()).Status);
            Assert.NotNull(applied);
            Assert.Equal(expected, Canonical(applied));
        }
        var random = new Random(20261014);
        var snapshot = MarkdownDocumentSnapshot.Empty;
        for (var position = 0; position < source.Length;)
        {
            var count = Math.Min(random.Next(1, 8), source.Length - position);
            snapshot = parser.Parse(snapshot.Source + source.Substring(position, count), snapshot);
            position += count;
        }
        Assert.Equal(expected, Canonical(snapshot));
    }

    [Fact]
    public async Task ChangingConverterConfigurationKeepsAcceptedSourceAndRejectsTheOldCustomSnapshot()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MarkdownParser Parser(string key) => MarkdownParser.FromOptions(new()
        { Converters = [new MarkdownNodeConverterRegistration<ParagraphBlock>(_ => new(key))] });
        var oldParser = Parser("old"); var newParser = Parser("new");
        var applied = new List<MarkdownDocumentSnapshot>();
        using var scheduler = new MarkdownUpdateScheduler<MarkdownDocumentSnapshot>(
            async (request, _) =>
            {
                if (request.ConfigurationVersion == 0) { started.TrySetResult(); await release.Task; }
                return (request.ConfigurationVersion == 0 ? oldParser : newParser).Parse(request.Source);
            },
            (update, _) => { update.TryCommit(() => applied.Add(update.Snapshot)); return Task.FromResult(MarkdownApplyResult.Applied); }, TimeSpan.Zero);
        var session = scheduler.BeginStreaming(); session.Append("seed");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        session.Append(" tail");
        var configuration = scheduler.Reconfigure(1);
        Assert.Equal(MarkdownUpdateStatus.Cancelled, (await session.Completion).Status);
        release.TrySetResult();
        Assert.Equal(MarkdownUpdateStatus.Completed, (await configuration.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Status);
        var final = Assert.Single(applied);
        Assert.Equal("seed tail", final.Source);
        Assert.Equal("new", final.Blocks[0].Metadata[MarkdownCustomNodeMetadata.RendererKey]);
    }

    private static MarkdownCustomNode InvalidOutput(string invalid)
    {
        var node = new MarkdownCustomNode("valid", "must not be committed");
        return invalid switch
        {
            "empty-renderer" => node with { RendererKey = string.Empty },
            "null-renderer" => node with { RendererKey = null! },
            "space-renderer" => node with { RendererKey = "bad key" },
            "control-renderer" => node with { RendererKey = "bad\nkey" },
            "reserved-renderer" => node with { Metadata = node.Metadata.Add("renderer-key", "override") },
            "reserved-style" => node with { Metadata = node.Metadata.Add("style-classes", "override") },
            "reserved-ast" => node with { Metadata = node.Metadata.Add("ast-type", "override") },
            "reserved-native" => node with { Metadata = node.Metadata.Add("native-kind", "List") },
            "reserved-native-case" => node with { Metadata = node.Metadata.Add("Native-Kind", "List") },
            "reserved-case" => node with { Metadata = node.Metadata.Add("Renderer-Key", "override") },
            "empty-metadata-key" => node with { Metadata = node.Metadata.Add(string.Empty, "value") },
            "trimmed-metadata-key" => node with { Metadata = node.Metadata.Add(" key ", "value") },
            "null-metadata-value" => node with { Metadata = node.Metadata.Add("key", null!) },
            "null-metadata" => node with { Metadata = null! },
            "empty-style-class" => node with { StyleClasses = [string.Empty] },
            "space-style-class" => node with { StyleClasses = ["two classes"] },
            "null-style-class" => node with { StyleClasses = [null!] },
            _ => throw new ArgumentOutOfRangeException(nameof(invalid))
        };
    }
    private static string Canonical(MarkdownDocumentSnapshot document) => JsonSerializer.Serialize(document with { Blocks = document.Blocks.Select(StripIdentity).ToImmutableArray() });
    private static MarkdownBlock StripIdentity(MarkdownBlock block) => block with { Id = 0, Children = block.Children.Select(StripIdentity).ToImmutableArray() };
}
