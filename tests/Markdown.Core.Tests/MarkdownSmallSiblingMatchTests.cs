using System.Collections.Immutable;
using System.Text.Json;
using Markdig.Syntax;
using NT.AvaloniaControl.Markdown;

namespace Markdown.Core.Tests;

public sealed class MarkdownSmallSiblingMatchTests
{
    [Theory]
    [InlineData("one\n\ntwo", "two\n\none")]
    [InlineData("one\n\ntwo", "insert\n\none\n\ntwo")]
    [InlineData("one\n\ntwo", "one\n\ntwo\n\ninsert")]
    [InlineData("one\n\ntwo\n\nthree", "two\n\nthree")]
    [InlineData("one\n\ntwo\n\nthree", "one\n\ntwo")]
    [InlineData("same\n\nsame", "same\n\nsame\n\nsame")]
    [InlineData("same\n\nsame\n\nsame", "same\n\nsame")]
    [InlineData("same\n\nother\n\nsame", "other\n\nsame\n\nsame")]
    [InlineData("same\n\nother\n\nsame", "same\n\nsame\n\nother")]
    [InlineData("word\n\nsecond", "# word\n\nsecond")]
    [InlineData("one\n\n```cs\npartial", "one\n\n```cs\npartial\n```\n\nafter")]
    [InlineData("one\n\n| A | B |\n| --- | --- |\n| a |", "one\n\n| A | B |\n| --- | --- |\n| a | b |")]
    [InlineData("- first\n- second", "- inserted\n- first\n- second")]
    [InlineData("- same\n- same", "- same\n- same\n- same")]
    [InlineData("- one\n- two", "- one changed\n- two")]
    public void SmallSiblingGroupsMatchThePreviousIndexedAlgorithm(string before, string after)
    {
        var parser = new MarkdownParser();
        AssertMatchesReference(parser.Parse(before), parser.Parse(after));
    }

    [Fact]
    public void CrossingTheSmallGroupLimitRetainsTheSameConservativeIdentityRules()
    {
        var parser = new MarkdownParser();
        for (var beforeCount = 0; beforeCount <= 6; beforeCount++)
        for (var afterCount = 0; afterCount <= 6; afterCount++)
        {
            var before = string.Join("\n\n", Enumerable.Range(0, beforeCount).Select(index => index % 2 == 0 ? "same" : "other"));
            var after = string.Join("\n\n", Enumerable.Range(0, afterCount).Select(index => index % 2 == 0 ? "other" : "same"));
            AssertMatchesReference(parser.Parse(before), parser.Parse(after));
        }
    }

    [Fact]
    public void SmallCustomGroupsPreserveCurrentMetadataAndSemanticKindWhileInvalidatingChanges()
    {
        MarkdownParser Parser(string renderer, string caption) => new(converters:
        [new MarkdownNodeConverterRegistration<ParagraphBlock>(_ => new(renderer)
         { Metadata = ImmutableDictionary<string, string>.Empty.Add("caption", caption) })]);
        const string source = "- one\n- two\n- three";
        var previous = Parser("old", "before").Parse(source);
        var parsed = Parser("new", "after").Parse(source);
        var current = AssertMatchesReference(previous, parsed);
        var paragraphs = current.Descendants().Where(block => block.SemanticKind == MarkdownBlockKind.Paragraph).ToArray();
        Assert.Equal(3, paragraphs.Length);
        Assert.All(paragraphs, block =>
        {
            Assert.Equal(MarkdownBlockKind.Custom, block.Kind);
            Assert.Equal("new", block.Metadata[MarkdownCustomNodeMetadata.RendererKey]);
            Assert.Equal("after", block.Metadata["caption"]);
        });
        var changes = MarkdownDocumentMatcher.GetChanges(previous, current);
        Assert.All(paragraphs, block => Assert.Contains(block.Id, changes.Changed));
    }

    [Fact]
    public void EverySplitOfNestedTablesAndRepeatedListItemsMatchesTheIndexedReference()
    {
        const string source = "before\n\n- repeated\n- repeated\n\n| A | B |\n| --- | --- |\n| *one* | `two` |\n\nafter";
        var parser = new MarkdownParser();
        var final = parser.Parse(source);
        for (var split = 0; split <= source.Length; split++)
            AssertMatchesReference(parser.Parse(source[..split]), final);
    }

    [Fact]
    public void ManySmallTableGroupsDoNotAllocatePerGroupMatchingIndexes()
    {
        const int count = 256;
        var source = string.Concat(Enumerable.Range(0, count).Select(index => $"| A{index} | B{index} |\n| --- | --- |\n| one | two |\n\n"));
        var parser = new MarkdownParser();
        var previous = parser.Parse(source);
        var parsed = parser.Parse(source + "tail");
        MarkdownDocumentMatcher.Reconcile(previous, parsed);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var current = MarkdownDocumentMatcher.Reconcile(previous, parsed);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(previous.Blocks.Select(block => block.Id), current.Blocks.Take(count).Select(block => block.Id));
        Assert.Equal(parsed.Text, current.Text);
        // Immutable records/arrays and the one large root index remain necessary. This generous
        // managed-allocation guard catches per-row indexes; it is independent of UI/time budgets.
        Assert.InRange(allocated, 0, 2 * 1024 * 1024);
    }

    private static MarkdownDocumentSnapshot AssertMatchesReference(MarkdownDocumentSnapshot previous, MarkdownDocumentSnapshot parsed)
    {
        var expected = parsed with { Blocks = IndexedMatch(previous.Blocks, parsed.Blocks, Edit.Between(previous.Source, parsed.Source)) };
        var current = MarkdownDocumentMatcher.Reconcile(previous, parsed);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(current));
        return current;
    }

    // The former indexed implementation is intentionally retained as a test oracle for identity
    // semantics. It uses the same frozen inputs, so the comparison includes identities as well as
    // all source/copy ranges, child order and payload, without hiding differences behind a hash.
    private static ImmutableArray<MarkdownBlock> IndexedMatch(ImmutableArray<MarkdownBlock> oldBlocks, ImmutableArray<MarkdownBlock> newBlocks, Edit edit)
    {
        if (oldBlocks.IsEmpty || newBlocks.IsEmpty) return newBlocks;
        var used = new HashSet<long>();
        var mappedSpans = oldBlocks.Select(old => (Block: old, Start: edit.MapUnchangedSpan(old.SourceStart, old.SourceLength)))
            .Where(pair => pair.Start is not null)
            .GroupBy(pair => (pair.Start!.Value, pair.Block.SourceLength, pair.Block.Kind))
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.Block).ToArray());
        var oldStarts = oldBlocks.GroupBy(block => (block.SourceStart, block.Kind)).ToDictionary(group => group.Key, group => group.ToArray());
        var oldCopies = oldBlocks.GroupBy(block => (block.Kind, block.Text)).ToDictionary(group => group.Key, group => group.Count());
        var newCopies = newBlocks.GroupBy(block => (block.Kind, block.Text)).ToDictionary(group => group.Key, group => group.Count());
        var result = ImmutableArray.CreateBuilder<MarkdownBlock>(newBlocks.Length);
        foreach (var block in newBlocks)
        {
            MarkdownBlock? match = null;
            var key = (block.Kind, block.Text);
            var ambiguous = oldCopies.TryGetValue(key, out var oldCount) && newCopies.TryGetValue(key, out var newCount) &&
                oldCount != newCount && Math.Max(oldCount, newCount) > 1;
            if (!ambiguous && mappedSpans.TryGetValue((block.SourceStart, block.SourceLength, block.Kind), out var spanCandidates))
            {
                foreach (var old in spanCandidates)
                {
                    if (!used.Contains(old.Id) && Equivalent(old, block)) { match = old; break; }
                }
            }
            if (!ambiguous && match is null && block.SourceStart < edit.PrefixLength && oldStarts.TryGetValue((block.SourceStart, block.Kind), out var startCandidates))
            {
                var candidates = startCandidates.Where(old => !used.Contains(old.Id) && old.SourceStart < edit.PrefixLength &&
                    CommonPrefix(old.Text, block.Text) > 0).ToArray();
                if (candidates.Length == 1) match = candidates[0];
            }
            if (match is null) result.Add(block);
            else
            {
                used.Add(match.Id);
                result.Add(block with { Id = match.Id, Children = IndexedMatch(match.Children, block.Children, edit) });
            }
        }
        return result.ToImmutable();
    }

    private static bool Equivalent(MarkdownBlock left, MarkdownBlock right) => left.Kind == right.Kind && left.Text == right.Text &&
        DictionariesEqual(left.Metadata, right.Metadata) && left.Inlines.Length == right.Inlines.Length &&
        left.Inlines.Zip(right.Inlines).All(pair => Equivalent(pair.First, pair.Second)) &&
        left.Children.Length == right.Children.Length && left.Children.Zip(right.Children).All(pair => Equivalent(pair.First, pair.Second));

    private static bool Equivalent(MarkdownInline left, MarkdownInline right) => left.Kind == right.Kind && left.Text == right.Text &&
        DictionariesEqual(left.Metadata, right.Metadata) && left.Children.Length == right.Children.Length &&
        left.Children.Zip(right.Children).All(pair => Equivalent(pair.First, pair.Second));

    private static bool DictionariesEqual(ImmutableDictionary<string, string> left, ImmutableDictionary<string, string> right) =>
        left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var value) && pair.Value == value);

    private static int CommonPrefix(string left, string right)
    {
        var length = Math.Min(left.Length, right.Length);
        var prefix = 0;
        while (prefix < length && left[prefix] == right[prefix]) prefix++;
        return prefix;
    }

    private readonly record struct Edit(int PrefixLength, int OldSuffixStart, int LengthDelta)
    {
        public static Edit Between(string previous, string current)
        {
            var prefix = CommonPrefix(previous, current);
            var suffix = 0;
            while (suffix < previous.Length - prefix && suffix < current.Length - prefix &&
                previous[previous.Length - suffix - 1] == current[current.Length - suffix - 1]) suffix++;
            return new(prefix, previous.Length - suffix, current.Length - previous.Length);
        }
        public int? MapUnchangedSpan(int start, int length) => start + length <= PrefixLength ? start :
            start >= OldSuffixStart ? start + LengthDelta : null;
    }
}
