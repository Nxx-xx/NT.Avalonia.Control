using System.Collections.Immutable;

namespace NT.AvaloniaControl.Markdown;

/// <summary>Preserves identities only when source location and parent context provide reliable evidence.</summary>
public static class MarkdownDocumentMatcher
{
    private static long _nextId;
    internal static long NewId() => Interlocked.Increment(ref _nextId);

    public static MarkdownDocumentSnapshot Reconcile(MarkdownDocumentSnapshot previous, MarkdownDocumentSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        var edit = SourceEdit.Between(previous.Source, current.Source);
        return current with { Blocks = Match(previous.Blocks, current.Blocks, edit) };
    }

    public static MarkdownSelection MigrateSelection(MarkdownDocumentSnapshot previous, MarkdownDocumentSnapshot current, MarkdownSelection selection)
    {
        var normalized = selection.Normalize(previous.Text);
        var currentById = MarkdownSnapshotIdentityIndex.Get(current);
        var anchor = MigratePosition(previous, current, currentById, normalized.Anchor);
        // A caret has one logical anchor; repeat migration adds no information.
        var active = normalized.IsEmpty ? anchor : MigratePosition(previous, current, currentById, normalized.Active);
        return new MarkdownSelection(anchor, active).Normalize(current.Text);
    }

    public static MarkdownDocumentChanges GetChanges(MarkdownDocumentSnapshot previous, MarkdownDocumentSnapshot current)
    {
        var oldBlocks = MarkdownSnapshotIdentityIndex.Get(previous);
        var newBlocks = MarkdownSnapshotIdentityIndex.Get(current);
        return new(
            newBlocks.Keys.Except(oldBlocks.Keys).ToImmutableArray(),
            oldBlocks.Keys.Except(newBlocks.Keys).ToImmutableArray(),
            newBlocks.Values.Where(block => oldBlocks.TryGetValue(block.Id, out var old) && !Equivalent(old, block)).Select(block => block.Id).ToImmutableArray());
    }

    private static ImmutableArray<MarkdownBlock> Match(ImmutableArray<MarkdownBlock> oldBlocks, ImmutableArray<MarkdownBlock> newBlocks, SourceEdit edit)
    {
        // Most blocks are leaves. There is no identity work for empty children, and a wholly new
        // sibling group already has its correct fresh identities; avoid all matching indexes here.
        if (oldBlocks.IsEmpty || newBlocks.IsEmpty) return newBlocks;
        if (oldBlocks.Length == 1 && newBlocks.Length == 1)
        {
            // There can be no sibling-copy ambiguity in a pair of singleton groups. Follow the
            // same mapped-span and unchanged-prefix rules without building five matching indexes.
            var old = oldBlocks[0];
            var block = newBlocks[0];
            if (old.Kind != block.Kind) return newBlocks;
            var mapped = edit.MapUnchangedSpan(old.SourceStart, old.SourceLength);
            var unchanged = mapped == block.SourceStart && old.SourceLength == block.SourceLength && Equivalent(old, block);
            var continued = !unchanged && block.SourceStart < edit.PrefixLength && old.SourceStart == block.SourceStart &&
                old.SourceStart < edit.PrefixLength && CommonPrefix(old.Text, block.Text) > 0;
            if (!unchanged && !continued) return newBlocks;
            return [WithMatchedIdentity(old, block, Match(old.Children, block.Children, edit))];
        }
        if (oldBlocks.Length <= 4 && newBlocks.Length <= 4) return MatchSmall(oldBlocks, newBlocks, edit);
        var used = new HashSet<long>();
        var mappedSpans = oldBlocks.Select(old => (Block: old, Start: edit.MapUnchangedSpan(old.SourceStart, old.SourceLength)))
            .Where(pair => pair.Start is not null)
            .GroupBy(pair => (pair.Start!.Value, pair.Block.SourceLength, pair.Block.Kind))
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.Block).ToArray());
        var oldStarts = oldBlocks.GroupBy(block => (block.SourceStart, block.Kind)).ToDictionary(group => group.Key, group => group.ToArray());
        // A source diff cannot determine which copy was inserted/deleted inside a run of identical
        // siblings. Rebuild those identities rather than carrying an anchor to another occurrence.
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
                    if (!used.Contains(old.Id) && Equivalent(old, block))
                    {
                        match = old;
                        break;
                    }
                }
            }
            // Appending or revising a block may change its span. Keep its identity only when its
            // beginning belongs to the proven unchanged source prefix in this same sibling group.
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
                result.Add(WithMatchedIdentity(match, block, Match(match.Children, block.Children, edit)));
            }
        }
        return result.ToImmutable();
    }

    private static ImmutableArray<MarkdownBlock> MatchSmall(ImmutableArray<MarkdownBlock> oldBlocks, ImmutableArray<MarkdownBlock> newBlocks, SourceEdit edit)
    {
        // Short lists and table rows are frequent. A bounded linear scan keeps the exact matching
        // rules while avoiding the five indexes allocated for every small sibling group.
        Span<long> usedIds = stackalloc long[4];
        var usedCount = 0;
        var result = ImmutableArray.CreateBuilder<MarkdownBlock>(newBlocks.Length);
        foreach (var block in newBlocks)
        {
            var oldCopies = 0;
            var newCopies = 0;
            foreach (var old in oldBlocks)
                if (old.Kind == block.Kind && old.Text == block.Text) oldCopies++;
            foreach (var current in newBlocks)
                if (current.Kind == block.Kind && current.Text == block.Text) newCopies++;
            var ambiguous = oldCopies != 0 && oldCopies != newCopies && Math.Max(oldCopies, newCopies) > 1;
            MarkdownBlock? match = null;
            if (!ambiguous)
            {
                foreach (var old in oldBlocks)
                {
                    if (old.Kind == block.Kind && old.SourceLength == block.SourceLength &&
                        edit.MapUnchangedSpan(old.SourceStart, old.SourceLength) == block.SourceStart &&
                        !usedIds[..usedCount].Contains(old.Id) && Equivalent(old, block))
                    {
                        match = old;
                        break;
                    }
                }
                if (match is null && block.SourceStart < edit.PrefixLength)
                {
                    var candidates = 0;
                    foreach (var old in oldBlocks)
                    {
                        if (old.Kind != block.Kind || old.SourceStart != block.SourceStart ||
                            old.SourceStart >= edit.PrefixLength || usedIds[..usedCount].Contains(old.Id) ||
                            CommonPrefix(old.Text, block.Text) == 0) continue;
                        match = old;
                        candidates++;
                    }
                    if (candidates != 1) match = null;
                }
            }
            if (match is null) result.Add(block);
            else
            {
                usedIds[usedCount++] = match.Id;
                result.Add(WithMatchedIdentity(match, block, Match(match.Children, block.Children, edit)));
            }
        }
        return result.MoveToImmutable();
    }

    // PRIVATE EXPERIMENT: matching and fresh-ID allocation have already completed.
    // Reuse only fully unchanged immutable data; semantic equivalence alone omits
    // source/text positions and is insufficient for returning a previous node.
    private static MarkdownBlock WithMatchedIdentity(MarkdownBlock old, MarkdownBlock block,
        ImmutableArray<MarkdownBlock> children)
    {
        var sameChildren = old.Children.Equals(children);
        if (!sameChildren && !old.Children.IsDefault && !children.IsDefault && old.Children.Length == children.Length)
        {
            sameChildren = true;
            for (var index = 0; index < children.Length; index++)
                if (!ReferenceEquals(old.Children[index], children[index])) { sameChildren = false; break; }
        }
        if (sameChildren) children = old.Children;
        if (sameChildren && old.Kind == block.Kind && old.SourceStart == block.SourceStart &&
            old.SourceLength == block.SourceLength && old.TextStart == block.TextStart &&
            old.TextLength == block.TextLength && old.Text == block.Text &&
            ReferenceEquals(old.Metadata, block.Metadata) && old.Inlines.Equals(block.Inlines)) return old;
        return block with { Id = old.Id, Children = children };
    }

    private static int MigratePosition(MarkdownDocumentSnapshot previous, MarkdownDocumentSnapshot current, IReadOnlyDictionary<long, MarkdownBlock> currentById, int position)
    {
        var old = MarkdownTextAnchor.FindContainingBlock(previous, position);
        var anchor = old is null ? new MarkdownTextAnchor(0, position) : new(old.Id, position - old.TextStart);
        // Public snapshots can be constructed manually. Resolve through the ID
        // as before, including the first-preorder behavior for duplicate old IDs.
        old = previous.FindBlock(anchor.BlockId);
        var alive = currentById.GetValueOrDefault(anchor.BlockId);
        if (old is not null && alive is not null)
        {
            var textEdit = SourceEdit.Between(old.Text, alive.Text);
            var local = anchor.Offset <= textEdit.PrefixLength ? anchor.Offset :
                anchor.Offset >= textEdit.OldSuffixStart ? anchor.Offset + textEdit.LengthDelta : textEdit.PrefixLength;
            return alive.TextStart + Math.Clamp(local, 0, alive.TextLength);
        }
        // Prefer the previous surviving logical neighbor. Fall forward only when no predecessor exists.
        MarkdownBlock? previousAlive = null;
        MarkdownBlock? nextAlive = null;
        foreach (var block in previous.Descendants())
        {
            if (!block.Children.IsEmpty || !currentById.TryGetValue(block.Id, out var replacement)) continue;
            // Preserve preorder ties: last preceding leaf, first following leaf.
            if (block.TextStart <= position) previousAlive = replacement;
            else nextAlive ??= replacement;
        }
        if (previousAlive is not null) return previousAlive.TextStart + previousAlive.TextLength;
        return nextAlive is not null ? nextAlive.TextStart : Math.Clamp(position, 0, current.Text.Length);
    }

    private static bool Equivalent(MarkdownBlock left, MarkdownBlock right)
    {
        if (left.Kind != right.Kind || left.Text != right.Text || !DictionariesEqual(left.Metadata, right.Metadata) ||
            left.Inlines.Length != right.Inlines.Length) return false;
        if (!left.Inlines.Equals(right.Inlines))
            for (var index = 0; index < left.Inlines.Length; index++)
                if (!Equivalent(left.Inlines[index], right.Inlines[index])) return false;
        if (left.Children.Length != right.Children.Length) return false;
        if (!left.Children.Equals(right.Children))
            for (var index = 0; index < left.Children.Length; index++)
                if (!Equivalent(left.Children[index], right.Children[index])) return false;
        return true;
    }

    private static bool Equivalent(MarkdownInline left, MarkdownInline right)
    {
        if (left.Kind != right.Kind || left.Text != right.Text || !DictionariesEqual(left.Metadata, right.Metadata) ||
            left.Children.Length != right.Children.Length) return false;
        if (!left.Children.Equals(right.Children))
            for (var index = 0; index < left.Children.Length; index++)
                if (!Equivalent(left.Children[index], right.Children[index])) return false;
        return true;
    }

    private static bool DictionariesEqual(ImmutableDictionary<string, string> left, ImmutableDictionary<string, string> right)
    {
        if (ReferenceEquals(left, right) && !ReferenceEquals(left, null)) return true;
        var leftEntries = left!;
        var rightEntries = right!;
        if (leftEntries.Count != rightEntries.Count) return false;
        foreach (var pair in leftEntries)
            if (!rightEntries.TryGetValue(pair.Key, out var value) || value != pair.Value) return false;
        return true;
    }

    private static int CommonPrefix(string left, string right)
    {
        var length = Math.Min(left.Length, right.Length);
        var prefix = 0;
        while (prefix < length && left[prefix] == right[prefix]) prefix++;
        return prefix;
    }

    private readonly record struct SourceEdit(int PrefixLength, int OldSuffixStart, int LengthDelta)
    {
        public static SourceEdit Between(string previous, string current)
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

public sealed record MarkdownDocumentChanges(ImmutableArray<long> Added, ImmutableArray<long> Removed, ImmutableArray<long> Changed);

/// <summary>A reading position that can survive layout and presenter recycling.</summary>
public readonly record struct MarkdownTextAnchor(long BlockId, int Offset)
{
    public static MarkdownTextAnchor FromPosition(MarkdownDocumentSnapshot document, int position)
    {
        position = MarkdownTextBoundary.SnapBackward(document.Text, position);
        var block = FindContainingBlock(document, position);
        return block is null ? new(0, position) : new(block.Id, position - block.TextStart);
    }

    internal static MarkdownBlock? FindContainingBlock(MarkdownDocumentSnapshot document, int position)
    {
        if (position == 0 && MarkdownSnapshotIdentityIndex.TryGetZeroContainingBlock(document, out var cached)) return cached;
        MarkdownBlock? candidate = null;
        foreach (var block in document.Descendants())
            if (block.TextLength > 0 && block.TextStart <= position && block.TextStart + block.TextLength >= position &&
                (candidate is null || block.TextLength < candidate.TextLength)) candidate = block;
        return candidate;
    }

    public int Resolve(MarkdownDocumentSnapshot document)
    {
        var block = document.FindBlock(BlockId);
        return MarkdownTextBoundary.SnapBackward(document.Text, block is null ? Offset : block.TextStart + Math.Clamp(Offset, 0, block.TextLength));
    }
}
