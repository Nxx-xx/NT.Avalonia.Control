using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Primitives = Avalonia.Controls.Primitives;

namespace NT.AvaloniaControl.Markdown;

/// <summary>Viewport block realization. The immutable document is the selection authority.</summary>
public class MarkdownDocumentPresenter : Panel
{
    private MarkdownDocumentSnapshot _document = MarkdownDocumentSnapshot.Empty;
    private readonly Dictionary<long, RealizedBlock> _realized = [];
    private Dictionary<long, int> _blockIndices = [];
    private double[] _heights = [];
    private double[] _tops = [];
    private int _topsDirtyFrom = int.MaxValue;
    private double _offset;
    private double _viewport = 700;
    private double _width;
    private Func<bool>? _followingViewportGuard;
    private Func<double>? _followingActualOffset;
    internal Markdown? Owner { get; set; }
    public int RealizedBlockCount => _realized.Count;
    public int RetainedBlockCount => 0;

    internal Control? GetRealizedBlockView(long nodeId) =>
        _realized.TryGetValue(nodeId, out var block) ? block.View : null;

    internal void SetDocument(MarkdownDocumentSnapshot document, bool preserveHeights = true)
    {
        var previousHeights = _heights;
        var previousIndices = _blockIndices;
        _document = document;
        _heights = new double[document.Blocks.Length];
        _tops = new double[_heights.Length + 1];
        _topsDirtyFrom = 0;
        _blockIndices = new(document.Blocks.Length);
        for (var i = 0; i < document.Blocks.Length; i++)
        {
            var block = document.Blocks[i];
            _blockIndices.Add(block.Id, i);
            _heights[i] = preserveHeights && previousIndices.TryGetValue(block.Id, out var previousIndex)
                ? previousHeights[previousIndex] : Estimate(block);
        }
        foreach (var pair in _realized.ToArray())
        {
            var block = _blockIndices.TryGetValue(pair.Key, out var index) ? document.Blocks[index] : null;
            if (block is null || !SameContent(pair.Value.Block, block)) Remove(pair.Key);
            else pair.Value.Block = block;
        }
        InvalidateMeasure();
    }

    internal void ResetPresentation()
    {
        foreach (var id in _realized.Keys.ToArray()) Remove(id);
        _heights = _document.Blocks.Select(Estimate).ToArray();
        _topsDirtyFrom = 0;
        InvalidateMeasure();
    }

    internal void SetViewport(double offset, double height)
    {
        var nextHeight = height > 0 ? height : 700;
        // During the synchronous following commit, the new document's tail defines
        // the range. ScrollViewer still has its previous extent until layout returns.
        var nextOffset = _followingViewportGuard is not null ? _offset : Math.Max(0, offset);
        if (Math.Abs(_offset - nextOffset) < .01 && Math.Abs(_viewport - nextHeight) < .01) return;
        _offset = nextOffset;
        _viewport = nextHeight;
        InvalidateMeasure();
    }

    internal void BeginFollowingViewport(double height, Func<bool> stillFollowing, Func<double> actualOffset)
    {
        _followingViewportGuard = stillFollowing;
        _followingActualOffset = actualOffset;
        _viewport = height > 0 ? height : 700;
        InvalidateMeasure();
    }

    internal void EndFollowingViewport(double offset, double height)
    {
        _followingViewportGuard = null;
        _followingActualOffset = null;
        SetViewport(offset, height);
    }

    private void UpdateFollowingViewport()
    {
        if (_followingViewportGuard is not { } guard) return;
        if (!guard())
        {
            var actualOffset = _followingActualOffset?.Invoke() ?? _offset;
            _followingViewportGuard = null;
            _followingActualOffset = null;
            _offset = Math.Max(0, actualOffset);
            return;
        }
        _offset = Math.Max(0, _tops[^1] - _viewport);
    }

    internal (long Id, double Offset) CaptureAnchor(double offset)
    {
        BuildTops();
        var index = IndexAt(offset);
        return index < _document.Blocks.Length ? (_document.Blocks[index].Id, offset - _tops[index]) : (0, 0);
    }

    internal double? AnchorOffset(long id, double offset)
    {
        BuildTops();
        return _blockIndices.TryGetValue(id, out var index) ? _tops[index] + Math.Clamp(offset, 0, _heights[index]) : null;
    }

    internal void InvalidateSelection()
    {
        foreach (var text in this.GetVisualDescendants().OfType<MarkdownTextPresenter>()) text.InvalidateVisual();
    }

    internal int Hit(Point point)
    {
        var nearest = this.GetVisualDescendants().OfType<MarkdownTextPresenter>()
            .Select(text => (Text: text, Point: this.TranslatePoint(point, text)))
            .Where(pair => pair.Point.HasValue)
            .OrderBy(pair => Distance(pair.Point!.Value, pair.Text.Bounds.Size)).FirstOrDefault();
        if (nearest.Text is not null) return nearest.Text.Hit(nearest.Point!.Value);
        var index = IndexAt(point.Y);
        return _document.Blocks.Length == 0 ? 0 : _document.Blocks[Math.Min(index, _document.Blocks.Length - 1)].TextStart;
    }

    internal Point? Position(int position)
    {
        foreach (var text in this.GetVisualDescendants().OfType<MarkdownTextPresenter>())
        {
            if (position < text.TextStart || position > text.TextStart + text.TextLength) continue;
            var rect = text.TextLayout.HitTestTextPosition(position - text.TextStart);
            return text.TranslatePoint(new Point(rect.X + text.Padding.Left, rect.Y + rect.Height / 2 + text.Padding.Top), this);
        }
        return null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? Math.Max(1, availableSize.Width) : 1200;
        if (Math.Abs(width - _width) > .5)
        {
            _width = width;
            _heights = _document.Blocks.Select(Estimate).ToArray();
            _topsDirtyFrom = 0;
        }
        if (_document.Blocks.IsEmpty) return new Size(width, 0);
        if (TryMeasureRefinedRange(width, out var refinedSize)) return refinedSize;
        if (_document.Blocks.IsEmpty) return new Size(width, 0);
        return MeasureLegacyRange(width);
    }

    // Each iteration completes one previously unmeasured index
    // from the current necessary range. Previously unnecessary indices can become
    // necessary later; the measured set therefore tracks work, not an interval.
    private bool TryMeasureRefinedRange(double width, out Size desiredSize)
    {
        desiredSize = default;
        if (GetType() != typeof(MarkdownDocumentPresenter) || Owner is not { CanRefineDefaultNativeRange: true }) return false;
        foreach (var block in _realized.Values)
            if (!block.RangeEligibleAtCreation || block.HasImages || block.Resources.Count != 0 || block.RendererScopes.Count != 0) return false;

        var document = _document;
        var measured = new HashSet<long>();
        var eligibility = new Dictionary<int, bool>();
        while (true)
        {
            if (!ReferenceEquals(document, _document)) return false;
            if (Owner is not { CanRefineDefaultNativeRange: true }) return false;
            BuildTops();
            UpdateFollowingViewport();
            CurrentRange(out var first, out var last);
            // A correction can expose a previously unseen custom/resource subtree.
            // Resume the original algorithm without creating that subtree here.
            for (var index = first; index <= last; index++)
            {
                if (!eligibility.TryGetValue(index, out var eligible))
                    eligibility[index] = eligible = IsDefaultNative(_document.Blocks[index]);
                if (!eligible) return false;
            }

            var next = -1;
            if (_followingViewportGuard is not null && Owner?.CodeHighlighter is null)
            {
                // The tail fixes a following viewport. Measuring an estimated head
                // first cannot reveal that later tail heights make it unnecessary.
                for (var index = last; index >= first; index--)
                    if (!measured.Contains(document.Blocks[index].Id)) { next = index; break; }
            }
            else
            {
                for (var index = first; index <= last; index++)
                    if (!measured.Contains(document.Blocks[index].Id)) { next = index; break; }
            }
            if (next < 0)
            {
                var keep = new HashSet<long>();
                for (var index = first; index <= last; index++) keep.Add(_document.Blocks[index].Id);
                foreach (var id in _realized.Keys.Where(id => !keep.Contains(id)).ToArray()) Remove(id);
                desiredSize = new Size(width, _tops.LastOrDefault());
                return true;
            }

            var dto = _document.Blocks[next];
            if (!_realized.TryGetValue(dto.Id, out var realized))
            {
                realized = new RealizedBlock(dto) { RangeEligibleAtCreation = Owner?.CanRefineDefaultNativeRange == true };
                realized.View = CreateBlock(dto, realized);
                if (!ReferenceEquals(document, _document))
                {
                    // This native-only realization has no renderer scopes/resources.
                    // It never entered the active dictionary or visual tree.
                    realized.Cancellation.Cancel();
                    var abandoned = realized.View!;
                    foreach (var node in abandoned.GetVisualDescendants().Prepend(abandoned))
                    {
                        if (node is MarkdownTextPresenter text) text.Owner = null;
                        else if (node is MarkdownImage image) image.Owner = null;
                        else if (node is MarkdownCodeBlock code) code.Owner = null;
                    }
                    realized.Cancellation.Dispose();
                    return false;
                }
                _realized.Add(dto.Id, realized);
                Children.Add(realized.View);
            }
            if (!ReferenceEquals(document, _document)) return false;
            realized.View!.Measure(new Size(width, double.PositiveInfinity));
            if (!ReferenceEquals(document, _document)) return false;
            var height = Math.Max(1, realized.View.DesiredSize.Height);
            if (_heights[next] != height) { _heights[next] = height; _topsDirtyFrom = Math.Min(_topsDirtyFrom, next); }
            measured.Add(dto.Id);
        }
    }

    private void CurrentRange(out int first, out int last)
    {
        var visibleFirst = IndexAt(_offset);
        var visibleLast = IndexAt(_offset + _viewport);
        TrimVisibleMargins(ref visibleFirst, ref visibleLast);
        first = Math.Max(0, visibleFirst - Overscan(Math.Min(visibleFirst, _document.Blocks.Length - 1)));
        last = Math.Min(_document.Blocks.Length - 1, visibleLast + Overscan(Math.Max(0, visibleLast)));
        foreach (var pair in _realized)
            if (pair.Value.HasImages && _blockIndices.TryGetValue(pair.Key, out var index) && index >= visibleFirst - 8 && index <= visibleLast + 8)
            { first = Math.Min(first, index); last = Math.Max(last, index); }
    }

    private static bool IsDefaultNative(MarkdownBlock block)
    {
        if (!Enum.IsDefined(block.Kind) || block.Metadata.ContainsKey("renderer-key") || block.Kind is MarkdownBlockKind.Custom or MarkdownBlockKind.Html or MarkdownBlockKind.Unsupported) return false;
        foreach (var inline in block.Inlines) if (!IsDefaultNative(inline)) return false;
        foreach (var child in block.Children) if (!IsDefaultNative(child)) return false;
        return true;
    }

    private static bool IsDefaultNative(MarkdownInline inline)
    {
        if (!Enum.IsDefined(inline.Kind) || inline.Metadata.ContainsKey("renderer-key") || inline.Kind is MarkdownInlineKind.Custom or MarkdownInlineKind.Image or MarkdownInlineKind.Html or MarkdownInlineKind.Unsupported) return false;
        foreach (var child in inline.Children) if (!IsDefaultNative(child)) return false;
        return true;
    }

    private Size MeasureLegacyRange(double width)
    {
        BuildTops();
        UpdateFollowingViewport();
        var visibleFirst = IndexAt(_offset);
        var visibleLast = IndexAt(_offset + _viewport);
        TrimVisibleMargins(ref visibleFirst, ref visibleLast);
        var first = Math.Max(0, visibleFirst - Overscan(Math.Min(visibleFirst, _document.Blocks.Length - 1)));
        var last = Math.Min(_document.Blocks.Length - 1, visibleLast + Overscan(Math.Max(0, visibleLast)));
        // A nearby native image can change height asynchronously above the viewport.
        // Keep and measure its existing realization within the ordinary eight-block band.
        foreach (var pair in _realized)
            if (pair.Value.HasImages && _blockIndices.TryGetValue(pair.Key, out var index) && index >= visibleFirst - 8 && index <= visibleLast + 8)
            { first = Math.Min(first, index); last = Math.Max(last, index); }
        var keep = new HashSet<long>();
        int measuredFirst;
        int measuredLast;
        while (true)
        {
            for (var i = first; i <= last; i++)
            {
                var block = _document.Blocks[i];
                keep.Add(block.Id);
                if (!_realized.TryGetValue(block.Id, out var realized))
                {
                    realized = new RealizedBlock(block) { RangeEligibleAtCreation = Owner?.CanRefineDefaultNativeRange == true };
                    realized.View = CreateBlock(block, realized);
                    _realized.Add(block.Id, realized);
                    Children.Add(realized.View);
                }
                realized.View!.Measure(new Size(width, double.PositiveInfinity));
                var height = Math.Max(1, realized.View.DesiredSize.Height);
                if (_heights[i] != height) { _heights[i] = height; _topsDirtyFrom = Math.Min(_topsDirtyFrom, i); }
            }
            BuildTops();
            UpdateFollowingViewport();
            // Child margins are outside Bounds. A viewport edge in a margin must not
            // add an extra ordinary block to the visible + 16 retention allowance.
            visibleFirst = IndexAt(_offset);
            visibleLast = IndexAt(_offset + _viewport);
            TrimVisibleMargins(ref visibleFirst, ref visibleLast);
            measuredFirst = Math.Max(0, visibleFirst - Overscan(Math.Min(visibleFirst, _document.Blocks.Length - 1)));
            measuredLast = Math.Min(_document.Blocks.Length - 1, visibleLast + Overscan(Math.Max(0, visibleLast)));
            foreach (var pair in _realized)
                if (pair.Value.HasImages && _blockIndices.TryGetValue(pair.Key, out var index) && index >= visibleFirst - 8 && index <= visibleLast + 8)
                { measuredFirst = Math.Min(measuredFirst, index); measuredLast = Math.Max(measuredLast, index); }
            if (measuredFirst >= first && measuredLast <= last) break;
            first = Math.Min(first, measuredFirst);
            last = Math.Max(last, measuredLast);
        }
        keep.Clear();
        for (var i = measuredFirst; i <= measuredLast; i++) keep.Add(_document.Blocks[i].Id);
        foreach (var id in _realized.Keys.Where(id => !keep.Contains(id)).ToArray()) Remove(id);
        return new Size(width, _tops.LastOrDefault());
    }

    private void TrimVisibleMargins(ref int first, ref int last)
    {
        // Apply the same actual margin boundaries before realization and before
        // pruning. Otherwise a viewport edge inside a margin recreates one extra
        // overscan block and removes it on every measure, invalidating layout again.
        while (first <= last && _realized.TryGetValue(_document.Blocks[first].Id, out var leading) &&
            _tops[first + 1] - leading.View!.Margin.Bottom <= _offset + .01) first++;
        while (last >= first && _realized.TryGetValue(_document.Blocks[last].Id, out var trailing) &&
            _tops[last] + trailing.View!.Margin.Top >= _offset + _viewport - .01) last--;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        BuildTops();
        foreach (var pair in _realized)
            if (_blockIndices.TryGetValue(pair.Key, out var index))
                pair.Value.View!.Arrange(new Rect(0, _tops[index], finalSize.Width, _heights[index]));
        return finalSize;
    }

    private readonly record struct TableRowLayout(CoordinatedTable Table, int Index)
    {
        public int Columns => Table.ColumnDefinitions.Count;
        public int Count => Table.RowDefinitions.Count;
    }
    private Control CreateBlock(MarkdownBlock block, RealizedBlock lifetime, bool skipCustom = false, bool? listLoose = null, TableRowLayout? tableRow = null)
    {
        if (!skipCustom && block.Metadata.TryGetValue("renderer-key", out var key) && Owner is { } owner)
        {
            if (owner.NodeRenderers.Blocks.TryGetValue(key, out var renderer))
            {
                var resourceStart = lifetime.Resources.Count;
                var scopeStart = lifetime.RendererScopes.Count;
                var scope = new RendererScope(lifetime, owner);
                lifetime.RendererScopes.Add(scope);
                try
                {
                    var document = _document;
                    var context = new MarkdownBlockRenderContext(owner, document, block, () =>
                        {
                            scope.VerifyActive();
                            var native = CreateBlock(block, lifetime, true, listLoose, tableRow);
                            (lifetime.PreparedDefaultRoots ??= []).Add(native);
                            return native;
                        },
                        (start, length) => { scope.VerifyActive(); return CreateText(document.Text.Substring(start, length), start, [], lifetime, block.Id); }, scope.RegisterResource);
                    var rendered = renderer(context) ?? throw new InvalidOperationException($"Renderer {key} returned null.");
                    ValidateBlockRendering(rendered, document, block);
                    rendered.Classes.Add("custom");
                    AddStyleClasses(rendered, block.Metadata.GetValueOrDefault("style-classes"));
                    if (lifetime.PreparedDefaultRoots?.Contains(rendered) != true) owner.PrepareElement(rendered, block);
                    return rendered;
                }
                catch (Exception error)
                {
                    CloseRendererScopes(lifetime, scopeStart);
                    var resources = DetachResources(lifetime, resourceStart);
                    // Keep the original renderer failure observable even when cleanup fails.
                    // Detach the batch and invalidate its contexts before invoking host code.
                    try { owner.ReportRenderingDiagnostic(key, block.Id, block.SourceStart, block.SourceLength, "Custom block renderer failed; native text fallback is used.", error); }
                    finally { ReportResourceErrors(owner, DisposeResources(resources)); }
                }
            }
            else owner.ReportRenderingDiagnostic(key, block.Id, block.SourceStart, block.SourceLength, $"No block renderer is registered for {key}; native text fallback is used.");
        }
        var kind = block.SemanticKind;
        MarkdownBlockPresenter border = kind == MarkdownBlockKind.Code ? new MarkdownCodeBlock { Owner = Owner, Code = block.Text, Language = block.Metadata.GetValueOrDefault("language") } : new MarkdownBlockPresenter();
        border.NodeId = block.Id;
        border.Classes.Add(kind.ToString().ToLowerInvariant());
        if (block.Kind == MarkdownBlockKind.Custom) border.Classes.Add("custom");
        if (kind == MarkdownBlockKind.Paragraph && listLoose == false) border.Classes.Add("tight-list");
        if (kind == MarkdownBlockKind.List) border.Classes.Add(block.Metadata.GetValueOrDefault("loose") == "true" ? "loose" : "tight");
        AddStyleClasses(border, block.Metadata.GetValueOrDefault("style-classes"));
        if (block.Metadata.TryGetValue("alignment", out var alignment))
            border.SetValue(TextBlock.TextAlignmentProperty, alignment switch { "center" => TextAlignment.Center, "right" => TextAlignment.Right, _ => TextAlignment.Left });
        if (kind == MarkdownBlockKind.Heading) border.Classes.Add("h" + block.Metadata.GetValueOrDefault("level", "1"));
        if (kind == MarkdownBlockKind.Alert) border.Classes.Add(block.Metadata.GetValueOrDefault("kind", "NOTE").ToLowerInvariant());
        if (kind == MarkdownBlockKind.ThematicBreak)
        {
            var rule = new Border { Height = 1, Margin = new Thickness(0, 8) };
            rule.Bind(Border.BackgroundProperty, rule.GetResourceObservable("MarkdownBorderBrush"));
            border.Child = rule;
        }
        else if (kind == MarkdownBlockKind.Table)
        {
            var columns = Int(block, "columns", 1);
            var customRows = block.Children.Any(row => row.Kind == MarkdownBlockKind.Custom);
            Grid table = customRows ? new CoordinatedTable(columns, block.Children.Length) : new Grid();
            if (!customRows)
                for (var i = 0; i < columns; i++) table.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto) { MinWidth = 140 });
            for (var rowIndex = 0; rowIndex < block.Children.Length; rowIndex++)
            {
                if (!customRows) table.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var row = block.Children[rowIndex];
                if (row.Kind == MarkdownBlockKind.Custom)
                {
                    var coordinated = (CoordinatedTable)table;
                    var view = CreateBlock(row, lifetime, tableRow: new(coordinated, rowIndex));
                    view.Margin = default;
                    Grid.SetRow(view, rowIndex); Grid.SetColumnSpan(view, columns);
                    var span = Math.Clamp(row.Children.Select(cell => Int(cell, "row-span", 1)).DefaultIfEmpty(1).Max(), 1, block.Children.Length - rowIndex);
                    Grid.SetRowSpan(view, span);
                    coordinated.RegisterRow(view, rowIndex, span);
                    table.Children.Add(view);
                    continue;
                }
                foreach (var cell in row.Children)
                {
                    var view = CreateBlock(cell, lifetime);
                    view.Margin = default;
                    if (row.Metadata.GetValueOrDefault("header") == "true") view.Classes.Add("header");
                    Grid.SetRow(view, rowIndex);
                    Grid.SetColumn(view, Math.Clamp(Int(cell, "column", 0), 0, columns - 1));
                    Grid.SetColumnSpan(view, Math.Clamp(Int(cell, "column-span", 1), 1, columns));
                    Grid.SetRowSpan(view, Math.Max(1, Int(cell, "row-span", 1)));
                    if (table is CoordinatedTable coordinated) coordinated.RegisterCell(view, rowIndex, direct: true);
                    table.Children.Add(view);
                }
            }
            border.Child = new ScrollViewer { Content = table, HorizontalScrollBarVisibility = Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Primitives.ScrollBarVisibility.Disabled };
        }
        else if (kind == MarkdownBlockKind.TableRow && !block.Children.IsEmpty && tableRow is { } rowLayout)
        {
            var row = new Grid();
            for (var i = 0; i < rowLayout.Columns; i++) row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto) { MinWidth = 140 });
            var span = Math.Clamp(block.Children.Select(cell => Int(cell, "row-span", 1)).DefaultIfEmpty(1).Max(), 1, rowLayout.Count - rowLayout.Index);
            for (var i = 0; i < span; i++) row.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            rowLayout.Table.RegisterGrid(row, rowLayout.Index, span);
            foreach (var cell in block.Children)
            {
                var view = CreateBlock(cell, lifetime); view.Margin = default;
                if (block.Metadata.GetValueOrDefault("header") == "true") view.Classes.Add("header");
                Grid.SetColumn(view, Math.Clamp(Int(cell, "column", 0), 0, rowLayout.Columns - 1));
                Grid.SetColumnSpan(view, Math.Clamp(Int(cell, "column-span", 1), 1, rowLayout.Columns));
                Grid.SetRowSpan(view, Math.Clamp(Int(cell, "row-span", 1), 1, span));
                rowLayout.Table.RegisterCell(view, rowLayout.Index);
                row.Children.Add(view);
            }
            border.Child = row;
        }
        else if (!block.Children.IsEmpty)
        {
            var stack = new StackPanel();
            var prefixLength = block.Children[0].TextStart - block.TextStart;
            foreach (var child in block.Children) stack.Children.Add(CreateBlock(child, lifetime, listLoose: kind == MarkdownBlockKind.List ? block.Metadata.GetValueOrDefault("loose") == "true" : kind == MarkdownBlockKind.ListItem ? listLoose : null));
            if (prefixLength > 0)
            {
                var markerText = block.Text[..Math.Min(prefixLength, block.Text.Length)];
                if (kind == MarkdownBlockKind.Alert) markerText = markerText.TrimEnd('\n');
                var marker = CreateText(markerText, block.TextStart, [], lifetime, block.Id);
                if (kind is MarkdownBlockKind.ListItem or MarkdownBlockKind.Footnote)
                {
                    var row = new DockPanel();
                    DockPanel.SetDock(marker, Dock.Left);
                    row.Children.Add(marker);
                    row.Children.Add(stack);
                    border.Child = row;
                }
                else
                {
                    Control header = marker;
                    if (kind == MarkdownBlockKind.Alert)
                    {
                        var alertKind = block.Metadata.GetValueOrDefault("kind", "NOTE");
                        marker.Classes.Add("alert-title");
                        var icon = new Avalonia.Controls.Shapes.Path { Width = 18, Height = 18, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = AlertIcon(alertKind) };
                        icon.Classes.Add("alerticon"); icon.Classes.Add(alertKind.ToLowerInvariant());
                        icon.Bind(Avalonia.Controls.Shapes.Shape.FillProperty, icon.GetResourceObservable("Markdown" + alertKind[..1] + alertKind[1..].ToLowerInvariant() + "Brush"));
                        header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { icon, marker } };
                    }
                    stack.Children.Insert(0, header); border.Child = stack;
                }
            }
            else border.Child = stack;
        }
        else
        {
            var text = CreateText(block.Text, block.TextStart, block.Inlines, lifetime, block.Id);
            if (kind == MarkdownBlockKind.Code)
            {
                text.Classes.Add("code");
                text.TextWrapping = TextWrapping.NoWrap;
                var stack = new StackPanel();
                var copy = new Button { Content = "复制代码", HorizontalAlignment = HorizontalAlignment.Right };
                var copyOwner = Owner is { } currentOwner ? new WeakReference<Markdown>(currentOwner) : null;
                var codeText = block.Text;
                copy.Click += async (_, _) => { if (copyOwner?.TryGetTarget(out var owner) == true) await owner.CopyTextAsync(codeText); };
                stack.Children.Add(copy);
                stack.Children.Add(new ScrollViewer { Content = text, HorizontalScrollBarVisibility = Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Primitives.ScrollBarVisibility.Disabled });
                border.Child = stack;
                ((MarkdownCodeBlock)border).TextPresenter = text;
            }
            else
            {
                var images = Flatten(block.Inlines).Where(inline => inline.SemanticKind == MarkdownInlineKind.Image).ToArray();
                lifetime.HasImages |= images.Length > 0;
                if (images.Length == 0) border.Child = text;
                else
                {
                    var stack = new StackPanel();
                    stack.Children.Add(text);
                    foreach (var inline in images)
                    {
                        var image = new MarkdownImage { Owner = Owner, NodeId = block.Id, TextStart = inline.TextStart, AltText = inline.Text, Source = inline.Metadata.GetValueOrDefault("url") };
                        Owner?.PrepareElement(image, block, inline);
                        stack.Children.Add(image);
                    }
                    border.Child = stack;
                }
            }
        }
        Owner?.PrepareElement(border, block);
        return border;
    }

    private sealed class CoordinatedTable : Grid
    {
        private readonly List<(Control View, int Row, bool Direct)> _cells = [];
        private readonly Dictionary<int, (Control View, int Span)> _rows = [];
        private readonly List<(Grid View, int Row, int Span)> _grids = [];

        public CoordinatedTable(int columns, int rows)
        {
            for (var i = 0; i < columns; i++) ColumnDefinitions.Add(new ColumnDefinition(new GridLength(140)));
            for (var i = 0; i < rows; i++) RowDefinitions.Add(new RowDefinition(new GridLength(1)));
        }
        public void RegisterCell(Control view, int row, bool direct = false) => _cells.Add((view, row, direct));
        public void RegisterRow(Control view, int row, int span) => _rows[row] = (view, span);
        public void RegisterGrid(Grid view, int row, int span) => _grids.Add((view, row, span));

        protected override Size MeasureOverride(Size availableSize)
        {
            // Outer and nested definitions must not participate in the same SharedSizeGroup:
            // the spanning row root feeds its complete width back into every column and causes
            // unbounded layout invalidation. Compute one set of dimensions from selected cells,
            // then give both grids the same fixed dimensions without a shared-size feedback loop.
            var widths = Enumerable.Repeat(140d, ColumnDefinitions.Count).ToArray();
            var heights = new double[RowDefinitions.Count];
            var cells = _cells.Where(cell => cell.Direct || IsRendered(cell.View, cell.Row)).ToArray();
            foreach (var cell in cells)
            {
                cell.View.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                if (GetColumnSpan(cell.View) == 1) widths[GetColumn(cell.View)] = Math.Max(widths[GetColumn(cell.View)], cell.View.DesiredSize.Width);
                if (GetRowSpan(cell.View) == 1) heights[cell.Row] = Math.Max(heights[cell.Row], cell.View.DesiredSize.Height);
            }
            foreach (var cell in cells) Grow(widths, GetColumn(cell.View), GetColumnSpan(cell.View), cell.View.DesiredSize.Width);
            var grids = _grids.Where(grid => IsRendered(grid.View, grid.Row)).ToArray();
            foreach (var row in _rows)
            {
                if (grids.Any(grid => grid.Row == row.Key)) continue;
                row.Value.View.Measure(new Size(widths.Sum(), double.PositiveInfinity));
                Grow(heights, row.Key, row.Value.Span, row.Value.View.DesiredSize.Height);
            }
            foreach (var cell in cells) Grow(heights, cell.Row, GetRowSpan(cell.View), cell.View.DesiredSize.Height);
            for (var i = 0; i < heights.Length; i++) heights[i] = Math.Max(1, heights[i]);
            for (var i = 0; i < widths.Length; i++) SetLength(ColumnDefinitions[i], widths[i]);
            for (var i = 0; i < heights.Length; i++) SetLength(RowDefinitions[i], heights[i]);
            foreach (var grid in grids)
            {
                for (var i = 0; i < widths.Length; i++) SetLength(grid.View.ColumnDefinitions[i], widths[i]);
                for (var i = 0; i < grid.Span; i++) SetLength(grid.View.RowDefinitions[i], heights[grid.Row + i]);
            }
            return base.MeasureOverride(availableSize);
        }

        private bool IsRendered(Control view, int row) => _rows.TryGetValue(row, out var root)
            ? ReferenceEquals(root.View, view) || view.GetVisualAncestors().Any(parent => ReferenceEquals(parent, root.View)) ||
              view.GetLogicalAncestors().Any(parent => ReferenceEquals(parent, root.View))
            : Children.Contains(view);
        private static void Grow(double[] sizes, int start, int count, double desired)
        {
            count = Math.Min(count, sizes.Length - start);
            var current = 0d;
            for (var i = start; i < start + count; i++) current += sizes[i];
            if (desired <= current || count <= 0) return;
            var extra = (desired - current) / count;
            for (var i = start; i < start + count; i++) sizes[i] += extra;
        }
        private static void SetLength(ColumnDefinition definition, double value)
        {
            var length = new GridLength(value);
            if (definition.Width != length) definition.Width = length;
        }
        private static void SetLength(RowDefinition definition, double value)
        {
            var length = new GridLength(value);
            if (definition.Height != length) definition.Height = length;
        }
    }

    private MarkdownTextPresenter CreateText(string text, int start, System.Collections.Immutable.ImmutableArray<MarkdownInline> inlines, RealizedBlock lifetime, long nodeId)
    {
        var view = new MarkdownTextPresenter { Owner = Owner, NodeId = nodeId, TopBlockId = lifetime.Block.Id, TextStart = start, TextLength = text.Length, TextWrapping = TextWrapping.Wrap };
        if (inlines.IsEmpty) view.Text = text;
        else foreach (var inline in inlines) view.Inlines!.Add(CreateInline(inline));
        return view;
    }

    private static void ValidateBlockRendering(Control rendered, MarkdownDocumentSnapshot document, MarkdownBlock block)
    {
        if (rendered.Parent is not null || rendered.GetVisualParent() is not null) throw new InvalidOperationException("A renderer must return an unparented control.");
        var texts = rendered.GetVisualDescendants().Prepend(rendered).Concat(rendered.GetLogicalDescendants().OfType<Visual>())
            .OfType<MarkdownTextPresenter>().Distinct().OrderBy(text => text.TextStart).ToArray();
        var position = block.TextStart;
        var end = block.TextStart + block.TextLength;
        foreach (var text in texts)
        {
            if (text.TextStart < position || text.TextLength < 0 || text.TextStart > end - text.TextLength ||
                !IsProjectionSeparator(document.Text.AsSpan(position, text.TextStart - position)))
                throw new InvalidOperationException("Custom block text helpers must cover the node's logical text in order.");
            var projection = text.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.Select(InlineText)) : text.Text ?? string.Empty;
            if (projection != document.Text.Substring(text.TextStart, text.TextLength))
                throw new InvalidOperationException("Custom block text helpers must preserve their logical text.");
            position = text.TextStart + text.TextLength;
        }
        if (!IsProjectionSeparator(document.Text.AsSpan(position, end - position)))
            throw new InvalidOperationException("Custom blocks with text require native text helpers for selection geometry.");
    }
    private static bool IsProjectionSeparator(ReadOnlySpan<char> text)
    {
        foreach (var character in text) if (character is not ('\n' or '\t')) return false;
        return true;
    }
    private static Geometry AlertIcon(string kind) => Geometry.Parse(kind switch
    {
        "TIP" => "M8,1 C4,1 2,4 2,7 C2,10 5,11 5,13 L11,13 C11,11 14,10 14,7 C14,4 12,1 8,1 Z M5,15 L11,15 L10,17 L6,17 Z",
        "IMPORTANT" => "M8,0 L16,8 L8,16 L0,8 Z M7,4 L9,4 L9,10 L7,10 Z M7,12 L9,12 L9,14 L7,14 Z",
        "WARNING" => "M8,0 L17,16 L0,16 Z M7,5 L9,5 L9,10 L7,10 Z M7,12 L9,12 L9,14 L7,14 Z",
        "CAUTION" => "M4,0 L12,0 L16,4 L16,12 L12,16 L4,16 L0,12 L0,4 Z M7,3 L9,3 L9,10 L7,10 Z M7,12 L9,12 L9,14 L7,14 Z",
        _ => "M8,0 A8,8 0 1 1 8,16 A8,8 0 1 1 8,0 Z M7,3 L9,3 L9,5 L7,5 Z M7,7 L9,7 L9,13 L7,13 Z",
    });

    private Inline CreateInline(MarkdownInline inline, bool skipCustom = false)
    {
        if (!skipCustom && inline.Metadata.TryGetValue("renderer-key", out var key) && Owner is { } owner)
        {
            if (owner.NodeRenderers.Inlines.TryGetValue(key, out var renderer))
            {
                try
                {
                    var rendered = renderer(new(owner, _document, inline, () => CreateInline(inline, true))) ?? throw new InvalidOperationException($"Renderer {key} returned null.");
                    if (InlineText(rendered) != inline.Text) throw new InvalidOperationException($"Renderer {key} must preserve the node's logical text.");
                    var mapped = new MarkdownSpan { TextStart = inline.TextStart, TextLength = inline.TextLength };
                    mapped.Classes.Add("custom"); AddStyleClasses(mapped, inline.Metadata.GetValueOrDefault("style-classes"));
                    AddInlineSemantics(mapped, inline);
                    mapped.Inlines.Add(rendered);
                    return mapped;
                }
                catch (Exception error) { owner.ReportRenderingDiagnostic(key, 0, inline.SourceStart, inline.SourceLength, "Custom inline renderer failed; native text fallback is used.", error); }
            }
            else owner.ReportRenderingDiagnostic(key, 0, inline.SourceStart, inline.SourceLength, $"No inline renderer is registered for {key}; native text fallback is used.");
        }
        if (inline.SemanticKind == MarkdownInlineKind.Text && inline.Children.IsEmpty)
        {
            var run = new MarkdownRun(inline.Text);
            run.Classes.Add("text");
            if (inline.Kind == MarkdownInlineKind.Custom) run.Classes.Add("custom");
            AddStyleClasses(run, inline.Metadata.GetValueOrDefault("style-classes"));
            return run;
        }
        var span = new MarkdownSpan { TextStart = inline.TextStart, TextLength = inline.TextLength };
        span.Classes.Add(inline.Kind.ToString().ToLowerInvariant());
        AddStyleClasses(span, inline.Metadata.GetValueOrDefault("style-classes"));
        AddInlineSemantics(span, inline);
        if (inline.Children.IsEmpty) span.Inlines.Add(new MarkdownRun(inline.Text));
        else foreach (var child in inline.Children) span.Inlines.Add(CreateInline(child));
        return span;
    }

    private void AddInlineSemantics(MarkdownSpan span, MarkdownInline inline)
    {
        span.Classes.Add(inline.SemanticKind.ToString().ToLowerInvariant());
        if (inline.SemanticKind == MarkdownInlineKind.Link)
        {
            span.Url = inline.Metadata.GetValueOrDefault("url");
            if (Owner?.IsLinkVisited(span.Url) == true) span.Classes.Add("visited");
        }
        if (inline.SemanticKind == MarkdownInlineKind.FootnoteReference) span.Classes.Add("link");
    }

    private void Remove(long id)
    {
        if (!_realized.Remove(id, out var realized)) return;
        realized.IsDisposed = true;
        CloseRendererScopes(realized, 0);
        var resources = DetachResources(realized, 0);
        var owner = Owner;
        realized.Cancellation.Cancel();
        try
        {
            if (realized.View is { } view)
            {
                foreach (var node in view.GetVisualDescendants().Prepend(view))
                {
                    if (node is MarkdownTextPresenter text) text.Owner = null;
                    else if (node is MarkdownImage image) image.Owner = null;
                    else if (node is MarkdownCodeBlock code) code.Owner = null;
                }
                Children.Remove(view);
            }
        }
        finally
        {
            var errors = DisposeResources(resources);
            realized.Cancellation.Dispose();
            ReportResourceErrors(owner, errors);
        }
    }

    private static void CloseRendererScopes(RealizedBlock lifetime, int start)
    {
        for (var index = start; index < lifetime.RendererScopes.Count; index++) lifetime.RendererScopes[index].IsClosed = true;
        lifetime.RendererScopes.RemoveRange(start, lifetime.RendererScopes.Count - start);
    }
    private static IDisposable[] DetachResources(RealizedBlock lifetime, int start)
    {
        var count = lifetime.Resources.Count - start;
        if (count == 0) return [];
        var resources = new IDisposable[count];
        lifetime.Resources.CopyTo(start, resources, 0, count);
        lifetime.Resources.RemoveRange(start, count);
        return resources;
    }
    private static List<Exception>? DisposeResources(IDisposable[] resources)
    {
        List<Exception>? errors = null;
        foreach (var resource in resources)
        {
            try { resource.Dispose(); }
            catch (Exception error) { (errors ??= []).Add(error); }
        }
        return errors;
    }
    private static void ReportResourceErrors(Markdown? owner, List<Exception>? errors)
    {
        if (owner is null || errors is null) return;
        foreach (var error in errors) owner.ReportError("RendererResource", error);
    }
    private void BuildTops()
    {
        if (_topsDirtyFrom == int.MaxValue) return;
        for (var i = _topsDirtyFrom; i < _heights.Length; i++) _tops[i + 1] = _tops[i] + _heights[i];
        _topsDirtyFrom = int.MaxValue;
    }
    private int IndexAt(double y) { var index = Array.BinarySearch(_tops, Math.Max(0, y)); return Math.Clamp(index >= 0 ? index : ~index - 1, 0, Math.Max(0, _document.Blocks.Length - 1)); }
    private int Overscan(int index) => index >= 0 && index < _document.Blocks.Length && _document.Blocks[index].SemanticKind == MarkdownBlockKind.Code ? 0 : 2;
    private static double Estimate(MarkdownBlock block) => block.SemanticKind == MarkdownBlockKind.Table ? Math.Max(40, block.Children.Length * 36) : Math.Max(40, block.Text.Length / 80.0 * 20 + 24);
    private static double Distance(Point p, Size size) => Math.Pow(Math.Max(0, Math.Max(-p.X, p.X - size.Width)), 2) + Math.Pow(Math.Max(0, Math.Max(-p.Y, p.Y - size.Height)), 2);
    private static int Int(MarkdownBlock block, string key, int fallback) => int.TryParse(block.Metadata.GetValueOrDefault(key), out var value) ? value : fallback;
    private static bool SameContent(MarkdownBlock a, MarkdownBlock b) => a.Kind == b.Kind && a.Text == b.Text && a.TextStart == b.TextStart && a.Metadata.SequenceEqual(b.Metadata) && a.Children.Length == b.Children.Length && a.Children.Zip(b.Children).All(pair => SameContent(pair.First, pair.Second)) && a.Inlines.Length == b.Inlines.Length && a.Inlines.Zip(b.Inlines).All(pair => SameInline(pair.First, pair.Second));
    private static bool SameInline(MarkdownInline a, MarkdownInline b) => a.Kind == b.Kind && a.Text == b.Text && a.TextStart == b.TextStart && a.TextLength == b.TextLength && a.Metadata.SequenceEqual(b.Metadata) && a.Children.Length == b.Children.Length && a.Children.Zip(b.Children).All(pair => SameInline(pair.First, pair.Second));
    private static IEnumerable<MarkdownInline> Flatten(IEnumerable<MarkdownInline> inlines) { foreach (var inline in inlines) { yield return inline; foreach (var child in Flatten(inline.Children)) yield return child; } }
    private sealed class RealizedBlock(MarkdownBlock block)
    {
        public MarkdownBlock Block { get; set; } = block;
        public Control? View { get; set; }
        public CancellationTokenSource Cancellation { get; } = new();
        public List<IDisposable> Resources { get; } = [];
        public List<RendererScope> RendererScopes { get; } = [];
        public bool IsDisposed { get; set; }
        public bool HasImages { get; set; }
        public bool RangeEligibleAtCreation { get; set; }
        public HashSet<Control>? PreparedDefaultRoots { get; set; }
    }
    private sealed class RendererScope(RealizedBlock lifetime, Markdown owner)
    {
        public bool IsClosed { get; set; }
        public void VerifyActive() => ObjectDisposedException.ThrowIf(IsClosed || lifetime.IsDisposed, nameof(MarkdownBlockRenderContext));
        public void RegisterResource(IDisposable resource)
        {
            if (IsClosed || lifetime.IsDisposed)
            {
                try { resource.Dispose(); }
                catch (Exception error) { owner.ReportError("RendererResource", error); }
                throw new ObjectDisposedException(nameof(MarkdownBlockRenderContext));
            }
            lifetime.Resources.Add(resource);
        }
    }
    private static void AddStyleClasses(StyledElement element, string? classes) { if (classes is not null) foreach (var name in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries)) element.Classes.Add(name); }
    private static string InlineText(Inline inline) => inline switch { Run run => run.Text ?? string.Empty, Span span => string.Concat(span.Inlines.Select(InlineText)), LineBreak => "\n", _ => throw new NotSupportedException("A custom inline must use native text runs/spans or line breaks.") };
}
