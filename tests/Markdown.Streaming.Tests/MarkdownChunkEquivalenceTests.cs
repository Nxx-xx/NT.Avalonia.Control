using System.Collections.Immutable;
using System.Text.Json;
using System.Threading.Channels;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Streaming.Tests;

public sealed class MarkdownChunkEquivalenceTests
{
    public static IEnumerable<object[]> AdversarialDocuments =>
    [
        ["# 中文\r\n\r\nA &amp; B 😀 e\u0301 👩‍💻\r\nnext"],
        ["before\n\n```csharp info\n  var x = 1;\n\n```\n\nafter"],
        ["before\n\n```text\n unclosed code\n"],
        ["| A | B |\n| :- | -: |\n| **left** | `right` |\n"],
        ["1. first\n   continuation\n   - nested\n2. second\n\nend"],
        ["[first][reference]\n\n[reference]: https://example.test/path \"title\"\n"],
        ["footnote[^one] twice[^one]\n\n[^one]: final definition\n"],
        ["image ![alt](image.png)\n\n> [!TIP]\n> use this\n"],
        ["- [x] checked\n- [ ] unchecked\n\n~~deleted~~ and ==marked==\n"]
    ];

    [Theory]
    [MemberData(nameof(AdversarialDocuments))]
    public async Task MT07_MT09_EveryTwoChunkSplitMatchesFullAssignment(string source)
    {
        var expected = Canonical(new MarkdownParser().Parse(source));
        for (var split = 0; split <= source.Length; split++)
        {
            MarkdownDocumentSnapshot? actual = null;
            using var scheduler = Create(update => actual = update.Snapshot);
            var session = scheduler.BeginStreaming();
            session.Append(source[..split]);
            session.Append(string.Empty);
            session.Append(source.AsMemory(split));
            var result = await session.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
            Assert.Equal(source, result.Source);
            Assert.NotNull(actual);
            Assert.Equal(expected, Canonical(actual));
        }
    }

    [Theory]
    [MemberData(nameof(AdversarialDocuments))]
    public async Task MT07_SeededRandomChunksMatchFullSnapshotIncludingTargetsAndProjection(string source)
    {
        var expected = Canonical(new MarkdownParser().Parse(source));
        for (var seed = 0; seed < 12; seed++)
        {
            MarkdownDocumentSnapshot? actual = null;
            using var scheduler = Create(update => actual = update.Snapshot);
            var session = scheduler.BeginStreaming();
            var random = new Random(seed);
            var position = 0;
            while (position < source.Length)
            {
                var count = Math.Min(random.Next(1, 8), source.Length - position);
                session.Append(source.AsMemory(position, count));
                position += count;
            }
            var result = await session.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(MarkdownUpdateStatus.Completed, result.Status);
            Assert.NotNull(actual);
            Assert.Equal(expected, Canonical(actual));
        }
    }

    [Fact]
    public async Task MT08_LateReferenceDefinitionReparsesEarlierLinkAndPreservesPrefixProgress()
    {
        var updates = Channel.CreateUnbounded<MarkdownDocumentSnapshot>();
        using var scheduler = Create(update => updates.Writer.TryWrite(update.Snapshot));
        var session = scheduler.BeginStreaming();
        session.Append("[early][ref]\n");
        var prefix = await updates.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.DoesNotContain(AllInlines(prefix), inline => inline.Kind == MarkdownInlineKind.Link);
        Assert.False(session.Completion.IsCompleted);
        session.Append("\n[ref]: https://example.test/target \"late title\"\n");
        var result = await session.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var final = await updates.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var link = Assert.Single(AllInlines(final), inline => inline.Kind == MarkdownInlineKind.Link);
        Assert.Equal("https://example.test/target", link.Metadata["url"]);
        Assert.Equal("late title", link.Metadata["title"]);
        Assert.Equal(Canonical(new MarkdownParser().Parse(result.Source)), Canonical(final));
    }

    [Theory]
    [InlineData("a\r", "\nb")]
    [InlineData("before \ud83d", "\ude00 after")]
    [InlineData("A &am", "p; B")]
    [InlineData("👩\u200d", "💻")]
    public async Task MT09_ActuallyPresentedIncompleteChunkIsCorrectedByFinalInput(string prefix, string suffix)
    {
        var updates = Channel.CreateUnbounded<MarkdownDocumentSnapshot>();
        using var scheduler = Create(update => updates.Writer.TryWrite(update.Snapshot));
        var session = scheduler.BeginStreaming();
        session.Append(prefix);
        _ = await updates.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        session.Append(suffix);
        await session.CompleteAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var final = await updates.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(Canonical(new MarkdownParser().Parse(prefix + suffix)), Canonical(final));
    }

    private static MarkdownUpdateScheduler<MarkdownDocumentSnapshot> Create(Action<MarkdownUpdate<MarkdownDocumentSnapshot>> commit)
    {
        var parser = new MarkdownParser();
        return new((request, _) => Task.FromResult(parser.Parse(request.Source)),
            (update, _) =>
            {
                update.TryCommit(() => commit(update));
                return Task.FromResult(MarkdownApplyResult.Applied);
            }, TimeSpan.Zero);
    }

    private static string Canonical(MarkdownDocumentSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot with { Blocks = snapshot.Blocks.Select(Normalize).ToImmutableArray() });

    private static MarkdownBlock Normalize(MarkdownBlock block) => block with
    {
        Id = 0,
        Children = block.Children.Select(Normalize).ToImmutableArray()
    };

    private static IEnumerable<MarkdownInline> AllInlines(MarkdownDocumentSnapshot snapshot) =>
        snapshot.Descendants().SelectMany(block => block.Inlines.SelectMany(Flatten));

    private static IEnumerable<MarkdownInline> Flatten(MarkdownInline inline)
    {
        yield return inline;
        foreach (var child in inline.Children)
            foreach (var nested in Flatten(child)) yield return nested;
    }
}
