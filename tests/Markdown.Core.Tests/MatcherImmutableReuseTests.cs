using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Core.Tests;

// These reference assertions validate this private allocation experiment. They do
// not establish a public guarantee that equal parse results share object identity.
public sealed class MatcherImmutableReuseTests
{
    [Fact]
    public void CompletedDefaultPrefixReusesExactRootAndNestedImmutableContent()
    {
        const string source = "# stable **heading** [direct](target)\n\n" +
            "- first **item**\n- second item\n\n" +
            "| A | B |\n|---|---|\n| one | two |\n\ntail";
        var parser = new MarkdownParser();
        var previous = parser.Parse(source);
        var current = parser.Parse(source + " extended", previous);
        Assert.Equal(4, previous.Blocks.Length);
        Assert.Equal(4, current.Blocks.Length);
        // The root group uses small-group matching; list-item and cell child
        // groups also exercise the singleton path before their parents can share.
        for (var index = 0; index < 3; index++) AssertSharedTree(previous.Blocks[index], current.Blocks[index]);
        Assert.True(previous.Blocks[0].Inlines.Equals(current.Blocks[0].Inlines));
        Assert.Same(previous.Blocks[0].Metadata, current.Blocks[0].Metadata);

        var next = parser.Parse(current.Source + "\n\nextra one\n\nextra two", current);
        Assert.Equal(6, next.Blocks.Length);
        // The same prefix now passes through the general indexed matcher.
        for (var index = 0; index < 3; index++) AssertSharedTree(current.Blocks[index], next.Blocks[index]);
        AssertEquivalentSequence([source, current.Source, next.Source]);
    }

    [Fact]
    public void AddedDuplicateSiblingsKeepFreshIdentitiesWhileAnUnambiguousPrefixCanShare()
    {
        const string source = "same\n\nstable **bold** [label](target)\n\ntail";
        var parser = new MarkdownParser();
        var previous = parser.Parse(source);
        var current = parser.Parse(source + "\n\nsame", previous);
        var duplicates = current.Blocks.Where(block => block.Text == "same").ToArray();
        Assert.Equal(2, duplicates.Length);
        foreach (var duplicate in duplicates)
        {
            Assert.NotEqual(previous.Blocks[0].Id, duplicate.Id);
            Assert.NotSame(previous.Blocks[0], duplicate);
        }
        Assert.NotEqual(duplicates[0].Id, duplicates[1].Id);
        Assert.Same(previous.Blocks[1], current.Blocks[1]);
        AssertEquivalentSequence([source, current.Source]);
    }

    [Theory]
    [InlineData("```text\nunchanged\n```\n\ntail", " \n```text\nunchanged\n```\n\ntail", true, false)]
    [InlineData("**a**\n\n```text\nunchanged\n```\n\ntail", "plain\n\n```text\nunchanged\n```\n\ntail", false, true)]
    [InlineData("```text\nunchanged\n```\n\ntail", "prefix\n\n```text\nunchanged\n```\n\ntail", true, true)]
    public void SourceOrLogicalPositionChangesKeepMatchedIdButRequireCurrentDto(
        string source, string nextSource, bool sourceMoves, bool textMoves)
    {
        var parser = new MarkdownParser();
        var previous = parser.Parse(source);
        var fresh = parser.Parse(nextSource);
        var oldCode = Assert.Single(previous.Blocks, block => block.Kind == MarkdownBlockKind.Code);
        var freshCode = Assert.Single(fresh.Blocks, block => block.Kind == MarkdownBlockKind.Code);
        Assert.Equal(oldCode.Text, freshCode.Text);
        Assert.Equal(oldCode.SourceLength, freshCode.SourceLength);
        Assert.Equal(oldCode.TextLength, freshCode.TextLength);
        Assert.Equal(sourceMoves, oldCode.SourceStart != freshCode.SourceStart);
        Assert.Equal(textMoves, oldCode.TextStart != freshCode.TextStart);

        // This is a valid current parsed DTO. Reuse only its immutable metadata
        // and empty inline backing to isolate position safety from provenance.
        var candidate = freshCode with { Metadata = oldCode.Metadata, Inlines = oldCode.Inlines };
        var input = fresh with { Blocks = fresh.Blocks.SetItem(fresh.Blocks.IndexOf(freshCode), candidate) };
        var current = MarkdownDocumentMatcher.Reconcile(previous, input);
        var code = Assert.Single(current.Blocks, block => block.Kind == MarkdownBlockKind.Code);
        Assert.Equal(oldCode.Id, code.Id);
        Assert.NotSame(oldCode, code);
        Assert.Equal(freshCode.SourceStart, code.SourceStart);
        Assert.Equal(freshCode.SourceLength, code.SourceLength);
        Assert.Equal(freshCode.TextStart, code.TextStart);
        Assert.Equal(freshCode.TextLength, code.TextLength);
        Assert.Equal(freshCode.Kind, code.Kind);
        Assert.Equal(freshCode.Text, code.Text);
        Assert.Same(oldCode.Metadata, code.Metadata);
        Assert.True(oldCode.Inlines.Equals(code.Inlines));
        Assert.Equal(fresh.Text, current.Text);
        Assert.Equal(fresh.Diagnostics.ToArray(), current.Diagnostics.ToArray());
        AssertEquivalentSequence([source, nextSource]);
    }

    [Fact]
    public void LateReferenceDefinitionRebuildsResolvedInlineAndPreservesCompleteSemantics()
    {
        const string source = "[early][r]\n\nstable\n\ntail";
        var parser = new MarkdownParser();
        var previous = parser.Parse(source);
        var current = parser.Parse(source + "\n\n[r]: https://example.test/late \"caption\"", previous);
        Assert.NotSame(previous.Blocks[0], current.Blocks[0]);
        Assert.False(previous.Blocks[0].Inlines.Equals(current.Blocks[0].Inlines));
        Assert.Equal("early", current.Blocks[0].Text);
        var link = Assert.Single(AllInlines(current.Blocks[0].Inlines), inline => inline.Kind == MarkdownInlineKind.Link);
        Assert.Equal("https://example.test/late", link.Metadata["url"]);
        Assert.Equal("caption", link.Metadata["title"]);
        AssertEquivalentSequence([source, current.Source, current.Source + "\n\nafter"]);
    }

    [Fact]
    public void LateFootnoteDefinitionRebuildsReferenceAndAddsTheCompleteFootnoteTree()
    {
        const string source = "note[^n]\n\nstable\n\ntail";
        var parser = new MarkdownParser();
        var previous = parser.Parse(source);
        var current = parser.Parse(source + "\n\n[^n]: footnote **body**", previous);
        Assert.NotSame(previous.Blocks[0], current.Blocks[0]);
        Assert.False(previous.Blocks[0].Inlines.Equals(current.Blocks[0].Inlines));
        var reference = Assert.Single(AllInlines(current.Blocks[0].Inlines), inline => inline.Kind == MarkdownInlineKind.FootnoteReference);
        Assert.Equal("^n", reference.Metadata["label"]);
        Assert.Equal("1", reference.Metadata["number"]);
        Assert.Contains(current.Descendants(), block => block.Kind == MarkdownBlockKind.Footnote && block.Text.Contains("footnote body", StringComparison.Ordinal));
        Assert.Contains("footnote body", new MarkdownSelection(0, current.Text.Length).GetText(current));
        AssertEquivalentSequence([source, current.Source, current.Source + "\n\nanother[^m]\n\n[^m]: second footnote"]);
    }

    private static void AssertSharedTree(MarkdownBlock previous, MarkdownBlock current)
    {
        Assert.Same(previous, current);
        for (var index = 0; index < previous.Children.Length; index++) AssertSharedTree(previous.Children[index], current.Children[index]);
    }

    private static IEnumerable<MarkdownInline> AllInlines(ImmutableArray<MarkdownInline> inlines)
    {
        foreach (var inline in inlines)
        {
            yield return inline;
            foreach (var child in AllInlines(inline.Children)) yield return child;
        }
    }

    private static void AssertEquivalentSequence(IEnumerable<string> sources)
    {
        var candidate = new MarkdownParser();
        var reference = new MarkdownParser(_ => { });
        MarkdownDocumentSnapshot? candidatePrevious = null, referencePrevious = null;
        var candidateOptions = PersistentIdentityOptions();
        var referenceOptions = PersistentIdentityOptions();
        foreach (var source in sources)
        {
            var expected = reference.Parse(source, referencePrevious);
            var actual = candidate.Parse(source, candidatePrevious);
            Assert.Equal(JsonSerializer.Serialize(expected, referenceOptions), JsonSerializer.Serialize(actual, candidateOptions));
            Assert.Equal(actual.Text, new MarkdownSelection(0, actual.Text.Length).GetText(actual));
            candidatePrevious = actual;
            referencePrevious = expected;
        }
    }

    private static JsonSerializerOptions PersistentIdentityOptions()
    {
        var ids = new Dictionary<long, long>();
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Type != typeof(MarkdownBlock)) return;
            var property = info.Properties.Single(value => value.Name == "Id");
            var original = property.Get!;
            property.Get = node =>
            {
                var id = (long)original(node)!;
                if (!ids.TryGetValue(id, out var ordinal)) ids[id] = ordinal = ids.Count + 1;
                return ordinal;
            };
        });
        return new() { TypeInfoResolver = resolver };
    }
}
