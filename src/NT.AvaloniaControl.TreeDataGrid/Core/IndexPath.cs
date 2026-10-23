using System.Collections;

namespace NT.AvaloniaControl.TreeDataGrid;

/// <summary>An immutable, zero-based path through the source collections, independent of the view order.</summary>
public readonly struct IndexPath : IEquatable<IndexPath>, IReadOnlyList<int>
{
    private readonly int[]? _indices;

    public IndexPath(params int[] indices)
    {
        ArgumentNullException.ThrowIfNull(indices);
        if (Array.Exists(indices, static index => index < 0))
            throw new ArgumentOutOfRangeException(nameof(indices), "Source indices cannot be negative.");
        _indices = (int[])indices.Clone();
    }

    public int Count => _indices?.Length ?? 0;

    public int this[int index] => (_indices ?? Array.Empty<int>())[index];

    public IndexPath Append(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        var result = new int[Count + 1];
        _indices?.CopyTo(result, 0);
        result[^1] = index;
        return new IndexPath(result);
    }

    public bool Equals(IndexPath other) => this.SequenceEqual(other);

    public override bool Equals(object? obj) => obj is IndexPath other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var index in this)
            hash.Add(index);
        return hash.ToHashCode();
    }

    public override string ToString() => string.Join(".", this);

    public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)(_indices ?? Array.Empty<int>())).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static bool operator ==(IndexPath left, IndexPath right) => left.Equals(right);

    public static bool operator !=(IndexPath left, IndexPath right) => !left.Equals(right);
}
