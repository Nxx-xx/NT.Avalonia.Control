using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Markdig;
using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdownNativeBenchmarks;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Core.Tests;

public sealed class MarkdownNormalizationReuseTests
{
    [Fact]
    public void NativeMixedAllNinetyTwoAppendPrefixesMatchCompleteDtosAndPersistentIdentityRelations()
    {
        var source = CorpusFactory.Mixed(1024 * 1024);
        var prefixes = Enumerable.Range(1, (source.Length + 8191) / 8192)
            .Select(index => source[..Math.Min(index * 8192, source.Length)]).ToArray();
        Assert.Equal(92, prefixes.Length);
        AssertEquivalentSequence(prefixes);
    }

    [Fact]
    public void StableDirectLinkContentIsSharedWhileAddedDuplicateSiblingsStillGetFreshIds()
    {
        var parser = new MarkdownParser();
        var previous = parser.Parse("same\n\nstable **bold** [label](target \"title\")\n\ntail");
        var current = parser.Parse(previous.Source + "\n\nsame", previous);
        Assert.Same(previous.Blocks[1].Inlines[0], current.Blocks[1].Inlines[0]);
        Assert.Same(previous.Blocks[1].Metadata, current.Blocks[1].Metadata);
        Assert.Equal(previous.Blocks[1].Id, current.Blocks[1].Id);
        Assert.NotEqual(previous.Blocks[0].Id, current.Blocks[0].Id);
        Assert.NotEqual(previous.Blocks[0].Id, current.Blocks[^1].Id);
        Assert.NotSame(previous.Blocks[^1].Inlines[0], current.Blocks[2].Inlines[0]);
        AssertEquivalentSequence([previous.Source, current.Source]);
    }

    public static IEnumerable<object[]> AdversarialSequences =>
    [
        [new[] { "[early][r]\n\nstable\n\ntail", "[early][r]\n\nstable\n\ntail\n\n[r]: https://example.test/late \"title\"", "[early][r]\n\nstable\n\ntail\n\n[r]: https://example.test/late \"title\"\n\nafter" }],
        [new[] { "note[^one]\n\nstable\n\ntail", "note[^one]\n\nstable\n\ntail\n\n[^one]: first\n", "note[^one]\n\nstable\n\ntail\n\n[^one]: first\n\nsecond[^two]\n\n[^two]: next" }],
        [new[] { "same\n\nsame\n\n- same\n- same", "same\n\nsame\n\n- same\n- same\n- same", "same\n\nsame\n\n- same\n- same\n- same\n\nsame" }],
        [new[] { "# head\n\nbody\n\n```cs title\n  α\t", "# head\n\nbody\n\n```cs title\n  α\t\n```\n\nafter", "# head\n\nbody\n\n```cs title\n  α\t\n```\n\nafter\n\nnext" }],
        [new[] { "start\n\n| A | B |", "start\n\n| A | B |\n|---|:--:|\n| α | data |", "start\n\n| A | B |\n|---|:--:|\n| α | data |\n\nafter" }],
        [new[] { "start\n\n> quoted\n\n- [x] item\n- other", "start\n\n> quoted\n\n- [x] item\n- other\n\n  loose body\n", "start\n\n> quoted\n\n- [x] item\n- other\n\n  loose body\n\nafter" }],
        [new[] { "[link](uri) ![alt](pic.png \"title\") https://example.test\n\nA &amp; 😀 e\u0301 👩‍💻\r\n\rtail", "[link](uri) ![alt](pic.png \"title\") https://example.test\n\nA &amp; 😀 e\u0301 👩‍💻\r\n\rtail\n\nnext" }],
        [new[] { "start\n\n<div>literal</div>\n\ntail", "start\n\n<div>literal</div>\n\ntail\n\nafter" }],
        [new[] { "start\n\nold\n\ntail", "start\n\nreplacement", "new\n\nstart\n\nreplacement\n\nafter" }]
    ];

    [Theory]
    [MemberData(nameof(AdversarialSequences))]
    public void DependencyStructuralAndReplacementSequencesMatchCacheDisabledParsing(string[] sources) => AssertEquivalentSequence(sources);

    [Fact]
    public void DifferentParserProvenanceCannotShareNormalization()
    {
        var previous = new MarkdownParser().Parse("first **bold**\n\nsecond\n\ntail");
        var current = new MarkdownParser().Parse(previous.Source + " appended", previous);
        Assert.NotSame(previous.Blocks[0].Inlines[0], current.Blocks[0].Inlines[0]);
        Assert.Equal(previous.Blocks[0].Id, current.Blocks[0].Id);
    }

    [Fact]
    public void UnfinishedPipeTableTailKeepsCompletedPrefixReusableAndRecreatesOnlyCurrentDiagnostics()
    {
        var parser = new MarkdownParser();
        const string source = "stable **bold** [direct](target)\n\nbody\n\n| A ";
        var previous = parser.Parse(source);
        Assert.Equal("MDNODE", Assert.Single(previous.Diagnostics).Code);
        var input = source + "| B |\n|---|:--:|\n| a | b |";
        var current = parser.Parse(input, previous);
        Assert.Same(previous.Blocks[0].Inlines[0], current.Blocks[0].Inlines[0]);
        Assert.Empty(current.Diagnostics);
        Assert.Equal(MarkdownBlockKind.Table, current.Blocks[^1].Kind);
        AssertEquivalentSequence([source, input, input + "\n\nafter"]);
        AssertEquivalentSequence(["[late]\n\nbody\n\n| A ", "[late]\n\nbody\n\n| A | B |\n|---|---|\n| a | b |\n\n[late]: /resolved"]);
        const string nonTailSource = "stable **bold**\n\n| A \n\nfooter";
        var nonTail = parser.Parse(nonTailSource);
        var nonTailAppend = parser.Parse(nonTailSource + " appended", nonTail);
        Assert.Equal("MDNODE", Assert.Single(nonTail.Diagnostics).Code);
        Assert.NotSame(nonTail.Blocks[0].Inlines[0], nonTailAppend.Blocks[0].Inlines[0]);
        AssertEquivalentSequence([nonTailSource, nonTailSource + " appended"]);
    }

    [Fact]
    public void NativeDenseFinalPipeTailBoundaryReusesStablePrefixWithCompleteDtoAndPersistentIds()
    {
        var source = CorpusFactory.Mixed(1024 * 1024);
        var parser = new MarkdownParser();
        var previous = parser.Parse(source[..745472]);
        Assert.Equal("MDNODE", Assert.Single(previous.Diagnostics).Code);
        var current = parser.Parse(source, previous);
        Assert.Same(previous.Blocks[0].Inlines[0], current.Blocks[0].Inlines[0]);
        Assert.Equal(previous.Blocks[0].Id, current.Blocks[0].Id);
        Assert.Empty(current.Diagnostics);
        AssertEquivalentSequence([source[..745472], source]);
    }

    [Fact]
    public void ConfiguredPipelineMutatorsAndWholeSourceConvertersContinueToRunForEveryNode()
    {
        var calls = 0;
        var configured = new MarkdownParser(builder => builder.DocumentProcessed += ast =>
        {
            calls++;
            ((LiteralInline)((ParagraphBlock)ast[0]).Inline!.FirstChild!).Content = new StringSlice("changed:" + calls);
        });
        var before = configured.Parse("first\n\nsecond\n\ntail");
        var after = configured.Parse(before.Source + " appended", before);
        Assert.Equal(2, calls);
        Assert.StartsWith("changed:2", after.Text);
        var convertedNodes = 0;
        var converted = new MarkdownParser(converters:
        [new MarkdownNodeConverterRegistration<ParagraphBlock>(context =>
        { convertedNodes++; return new("length", context.Source.Length.ToString()); })]);
        var customBefore = converted.Parse("first\n\nsecond\n\ntail");
        var customAfter = converted.Parse(customBefore.Source + " appended", customBefore);
        Assert.Equal(6, convertedNodes);
        Assert.All(customAfter.Blocks, block => Assert.Equal(customAfter.Source.Length.ToString(), block.Text));
    }

    [Fact]
    public void AKeptSnapshotDoesNotKeepTheOriginatingParserAlive()
    {
        var (snapshot, parser) = DetachedParser();
        for (var attempt = 0; attempt < 8 && parser.IsAlive; attempt++)
        { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Assert.False(parser.IsAlive);
        Assert.Equal("first\nsecond\ntail", snapshot.Text);
        GC.KeepAlive(snapshot);
    }

    [Fact]
    public void AKeptParserAndNewSnapshotDoNotKeepThePreviousSnapshotOrSourceBufferAlive()
    {
        var (parser, current, previous, oldSource) = DetachedPreviousSnapshot();
        for (var attempt = 0; attempt < 8 && (previous.IsAlive || oldSource.IsAlive); attempt++)
        { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Assert.False(previous.IsAlive);
        Assert.False(oldSource.IsAlive);
        Assert.EndsWith(" appended", current.Text);
        GC.KeepAlive(current);
        GC.KeepAlive(parser);
    }

    [Fact]
    public void NestedGridReuseKeepsOwnedFenceCodeAndAdjacentCellWithEquivalentIdentityRelations()
    {
        const string grid = "+------------+------------+\n| A          | B          |\n+============+============+\n| ```cs title| right      |\n| code       | cell       |\n+------------+------------+";
        var source = string.Join('\n', grid.Split('\n').Select(line => "> " + line)) + "\n\nfooter";
        var parser = new MarkdownParser();
        var previous = parser.Parse(source);
        var current = parser.Parse(source + " appended", previous);
        var oldCode = Assert.Single(previous.Descendants(), block => block.Kind == MarkdownBlockKind.Code);
        var code = Assert.Single(current.Descendants(), block => block.Kind == MarkdownBlockKind.Code);
        Assert.Equal("code\n", code.Text);
        Assert.Equal("cs title", code.Metadata["info"]);
        Assert.Same(oldCode.Text, code.Text);
        Assert.Equal(oldCode.Id, code.Id);
        Assert.Equal(1, current.Text.Split("right cell", StringSplitOptions.None).Length - 1);
        var right = Assert.Single(current.Descendants(), block => block.Kind == MarkdownBlockKind.TableCell && block.Text == "right cell");
        Assert.Equal(right.Text, current.Text.Substring(right.TextStart, right.TextLength));
        Assert.Empty(current.Diagnostics);
        AssertEquivalentSequence([source, source + " appended", source + " appended\n\nnext"]);
    }

    [Fact]
    public async Task ConcurrentAppendParsesUseImmutableEntriesAndOnlyTheirSuppliedPreviousSnapshot()
    {
        var parser = new MarkdownParser();
        var previous = parser.Parse("first **bold**\n\nsecond\n\ntail");
        var current = await Task.WhenAll(Enumerable.Range(0, 4).Select(index => Task.Run(() =>
            parser.Parse(previous.Source + " " + index, previous))));
        for (var index = 0; index < current.Length; index++)
        {
            Assert.Equal(previous.Source + " " + index, current[index].Source);
            Assert.Equal(previous.Blocks[0].Id, current[index].Blocks[0].Id);
            Assert.Same(previous.Blocks[0].Inlines[0], current[index].Blocks[0].Inlines[0]);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (MarkdownDocumentSnapshot Snapshot, WeakReference Parser) DetachedParser()
    {
        var parser = new MarkdownParser();
        return (parser.Parse("first\n\nsecond\n\ntail"), new(parser));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (MarkdownParser Parser, MarkdownDocumentSnapshot Current, WeakReference Previous, WeakReference Source) DetachedPreviousSnapshot()
    {
        var parser = new MarkdownParser();
        // A runtime-created buffer avoids the string intern pool and is distinct from the appended source.
        var source = "# " + Guid.NewGuid().ToString("N") + "\n\n**stable** [direct](target)\n\n" + new string('x', 10_000);
        var previous = parser.Parse(source);
        var current = parser.Parse(source + " appended", previous);
        Assert.Same(previous.Blocks[1].Inlines[0], current.Blocks[1].Inlines[0]);
        return (parser, current, new(previous), new(source));
    }

    private static void AssertEquivalentSequence(IEnumerable<string> sources)
    {
        var cached = new MarkdownParser();
        // A no-op configuration builds the same default pipeline while disabling normalization reuse.
        var reference = new MarkdownParser(_ => { });
        MarkdownDocumentSnapshot? cachedPrevious = null, referencePrevious = null;
        var cachedOptions = IdentityOptions();
        var referenceOptions = IdentityOptions();
        foreach (var source in sources)
        {
            var expected = reference.Parse(source, referencePrevious);
            var actual = cached.Parse(source, cachedPrevious);
            var expectedBytes = JsonSerializer.SerializeToUtf8Bytes(expected, referenceOptions);
            var actualBytes = JsonSerializer.SerializeToUtf8Bytes(actual, cachedOptions);
            Assert.True(expectedBytes.AsSpan().SequenceEqual(actualBytes), $"Complete DTO or persistent identity differs at UTF-16 source length {source.Length}.");
            Assert.Equal(actual.Text, new MarkdownSelection(0, actual.Text.Length).GetText(actual));
            cachedPrevious = actual; referencePrevious = expected;
        }
    }

    private static JsonSerializerOptions IdentityOptions()
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
