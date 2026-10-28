using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>A category legend entry whose value and members describe the current visible terminal nodes.</summary>
public sealed class HierarchyLegendItem
{
    internal HierarchyLegendItem(object? category, string label, double value, IBrush brush, HierarchyEntry[] nodes)
    { Category = category; Label = label; Value = value; Brush = brush; Nodes = Array.AsReadOnly(nodes); }
    public object? Category { get; }
    public string Label { get; }
    public double Value { get; }
    public IBrush Brush { get; }
    /// <summary>Original validated entries, rather than invented aggregate observations.</summary>
    public IReadOnlyList<HierarchyEntry> Nodes { get; }
}

public abstract partial class HierarchyChartBase
{
    public static readonly StyledProperty<string?> GroupPathProperty = AvaloniaProperty.Register<HierarchyChartBase, string?>(nameof(GroupPath));
    public static readonly StyledProperty<bool> ShowLegendProperty = AvaloniaProperty.Register<HierarchyChartBase, bool>(nameof(ShowLegend));
    public static readonly StyledProperty<bool> ShowSizeLegendProperty = AvaloniaProperty.Register<HierarchyChartBase, bool>(nameof(ShowSizeLegend), true);
    /// <summary>Optional category property on the original source. Leaf categories keep source-order colors across drill and collapse.</summary>
    public string? GroupPath { get => GetValue(GroupPathProperty); set => SetValue(GroupPathProperty, value); }
    /// <summary>Displays category keys below the plot. The footer is bounded to 100 DIPs and one third of the available plot.</summary>
    public bool ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }
    /// <summary>Includes a relative-area 9:4:1 circle key in BubbleCloudChart and PackedBubbleChart when ShowLegend is enabled.</summary>
    public bool ShowSizeLegend { get => GetValue(ShowSizeLegendProperty); set => SetValue(ShowSizeLegendProperty, value); }
    /// <summary>All visible terminal categories, including entries omitted from the bounded visual footer.</summary>
    public IReadOnlyList<HierarchyLegendItem> LegendItems { get { _ = GetMarks(); return _legendItems; } }
    private IReadOnlyList<HierarchyLegendItem> _legendItems = [];
    private HierarchySnapshot? _legendSnapshot;
    private string? _legendPath;
    private IReadOnlyList<IBrush>? _legendPalette;
    private int[] _legendTerminals = [];
    private readonly Dictionary<int, int> _legendColorIndices = [];
    private Rect _legendBounds;
    private static readonly object MissingGroup = new();
    private bool HasSizeKey => ShowSizeLegend && (this is BubbleCloudChart or PackedBubbleChart);

    private void ClearLegend()
    {
        _legendItems = []; _legendSnapshot = null; _legendPath = null; _legendPalette = null;
        _legendTerminals = []; _legendColorIndices.Clear();
    }

    protected override Rect GetPlotArea(Size size)
    {
        var plot = base.GetPlotArea(size); _legendBounds = default;
        if (!ShowLegend || plot.Height <= 0 || plot.Width <= 0) return plot;
        var reserve = Math.Min(100, plot.Height / 3);
        _legendBounds = new(plot.Left, plot.Bottom - reserve + Math.Min(6, reserve), plot.Width, Math.Max(0, reserve - 6));
        return new(plot.Left, plot.Top, plot.Width, plot.Height - reserve);
    }

    private void PrepareLegend()
    {
        if (_snapshot is null)
        { _legendItems = []; _legendSnapshot = null; _legendColorIndices.Clear(); _legendTerminals = []; return; }
        var entries = _frame.Where(glyph => !glyph.IsExiting).Select(glyph => glyph.Entry).ToArray();
        var parents = entries.Select(entry => entry.ParentIndex).ToHashSet();
        var terminals = entries.Where(entry => !parents.Contains(entry.Index)).OrderBy(entry => entry.Index).ToArray();
        if (ReferenceEquals(_legendSnapshot, _snapshot) && _legendPath == GroupPath && ReferenceEquals(_legendPalette, Palette) &&
            _legendTerminals.AsSpan().SequenceEqual(terminals.Select(entry => entry.Index).ToArray())) return;
        _legendSnapshot = _snapshot; _legendPath = GroupPath; _legendPalette = Palette;
        _legendTerminals = terminals.Select(entry => entry.Index).ToArray(); _legendColorIndices.Clear();
        var groupPath = GroupPath; var grouped = !string.IsNullOrWhiteSpace(groupPath);
        var colors = new Dictionary<object, int>();
        object Key(HierarchyEntry entry)
        {
            if (!grouped) return Identity(entry);
            return ChartDataAccessor.TryRead(entry.Source, groupPath, out var raw) ? raw ?? MissingGroup : MissingGroup;
        }
        // A global leaf traversal makes category colors independent of the current drill root.
        foreach (var entry in _snapshot.Nodes.Where(entry => entry.Children.Count == 0).Concat(_snapshot.Nodes.Where(entry => entry.Children.Count > 0)))
        {
            var key = Key(entry);
            if (!colors.TryGetValue(key, out var index)) { index = colors.Count; colors.Add(key, index); }
            _legendColorIndices[entry.Index] = grouped ? index : entry.Index;
        }
        var members = new Dictionary<object, List<HierarchyEntry>>();
        foreach (var entry in terminals)
        { var key = Key(entry); if (!members.TryGetValue(key, out var list)) members.Add(key, list = []); list.Add(entry); }
        var items = new List<HierarchyLegendItem>();
        foreach (var (key, nodes) in members)
        {
            double total = 0, correction = 0;
            foreach (var node in nodes) { var adjusted = node.AggregateValue - correction; var next = total + adjusted; correction = (next - total) - adjusted; total = next; }
            var first = nodes[0]; var label = grouped ? ReferenceEquals(key, MissingGroup) ? "(ungrouped)" :
                key is IFormattable formatted ? formatted.ToString(null, CultureInfo.InvariantCulture) ?? "" : key.ToString() ?? "" :
                first.Label ?? first.Id?.ToString() ?? $"Node {first.Index + 1}";
            items.Add(new(grouped && !ReferenceEquals(key, MissingGroup) ? key : grouped ? null : first.Id ?? first.Source,
                label, total, EntryBrush(first), nodes.ToArray()));
        }
        _legendItems = items.AsReadOnly();
    }

    private IBrush EntryBrush(HierarchyEntry entry) => BrushAt(!string.IsNullOrWhiteSpace(GroupPath) &&
        _legendColorIndices.TryGetValue(entry.Index, out var color) ? color : entry.Index);

    private void RenderLegend(DrawingContext context)
    {
        if (UsesLegendItemTemplate || !ShowLegend || _legendBounds.Width <= 0 || _legendBounds.Height <= 0) return;
        using var clip = context.PushClip(_legendBounds);
        var line = Math.Max(16, FontSize + 5); var sizeHeight = HasSizeKey ? Math.Min(28, _legendBounds.Height) : 0;
        var available = Math.Max(0, _legendBounds.Height - sizeHeight); var columns = Math.Max(1, (int)(_legendBounds.Width / 120));
        var rows = (int)(available / line); var capacity = Math.Min(20, rows * columns);
        var shown = Math.Min(_legendItems.Count, _legendItems.Count > capacity ? Math.Max(0, capacity - 1) : capacity);
        var columnWidth = _legendBounds.Width / columns;
        for (var i = 0; i < shown; i++)
        {
            var left = _legendBounds.Left + i % columns * columnWidth; var y = _legendBounds.Top + (i / columns + .5) * line;
            context.DrawRectangle(_legendItems[i].Brush, null, new Rect(left + 1, y - 4, 8, 8));
            DrawLabel(context, $"{_legendItems[i].Label} {F(_legendItems[i].Value)}", new(left + 14 + (columnWidth - 16) / 2, y), Math.Max(0, columnWidth - 16));
        }
        if (_legendItems.Count > shown && capacity > 0)
        {
            var left = _legendBounds.Left + shown % columns * columnWidth; var y = _legendBounds.Top + (shown / columns + .5) * line;
            DrawLabel(context, $"+{_legendItems.Count - shown} categories", new(left + columnWidth / 2, y), Math.Max(0, columnWidth - 4));
        }
        if (!HasSizeKey || sizeHeight < 16) return;
        var sizeY = _legendBounds.Bottom - sizeHeight / 2;
        if (_legendBounds.Width < 180)
        { DrawLabel(context, "Relative area 9 : 4 : 1", new(_legendBounds.Center.X, sizeY), _legendBounds.Width); return; }
        DrawLabel(context, "Relative area", new(_legendBounds.Left + 42, sizeY), 84);
        var width = (_legendBounds.Width - 90) / 3; var radius = Math.Min(9, sizeHeight * .32);
        for (var i = 0; i < 3; i++)
        {
            var relative = i == 0 ? 9 : i == 1 ? 4 : 1; var r = radius * Math.Sqrt(relative / 9d);
            var x = _legendBounds.Left + 90 + i * width + radius;
            context.DrawEllipse(Foreground ?? Brushes.Gray, null, new Point(x, sizeY), r, r);
            DrawLabel(context, relative.ToString(CultureInfo.InvariantCulture), new(x + radius + 10, sizeY), Math.Max(8, width - 2 * radius - 4));
        }
    }
}
