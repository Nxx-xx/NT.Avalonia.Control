using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

/// <summary>Deterministic shelf-packed measured words. Omitted indices are public; source words are not merged. Collision boxes include four DIPs of padding.</summary>
public sealed class WordCloudChart : OperationalChartBase
{
    public static readonly StyledProperty<double> MinFontSizeProperty = AvaloniaProperty.Register<WordCloudChart, double>(nameof(MinFontSize), 12, validate: x => double.IsFinite(x) && x >= 1 && x <= 512);
    public static readonly StyledProperty<double> MaxFontSizeProperty = AvaloniaProperty.Register<WordCloudChart, double>(nameof(MaxFontSize), 48, validate: x => double.IsFinite(x) && x >= 1 && x <= 512);
    public WordCloudChart() => ValuePath = "Weight";
    protected override int SourceItemLimit => ChartOperational.MaximumWords;
    public double MinFontSize { get => GetValue(MinFontSizeProperty); set => SetValue(MinFontSizeProperty, value); }
    public double MaxFontSize { get => GetValue(MaxFontSizeProperty); set => SetValue(MaxFontSizeProperty, value); }
    private OperationalWords? _words;
    public OperationalWords? Words { get { _ = GetMarks(); return _words; } }
    private FormattedText Text(string value, double fontSize, IBrush brush) => new(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyle, FontWeight), fontSize, brush);
    protected override IReadOnlyList<EngineeringMarkInfo> BuildMarks(ChartDataSnapshot data, Rect plot)
    {
        BeginScene(); var values = data.Points.Select(p => new WordPoint(Identity(p), ReadText(p.Item, TextPath)!, Numeric(p, ValuePath))).ToArray(); _words = ChartOperational.Words(values, plot.Width, plot.Height, (text, size) => { var measured = Text(text, size, Foreground ?? Brushes.Gray); return (measured.WidthIncludingTrailingWhitespace, measured.Height); }, MinFontSize, MaxFontSize); var marks = new List<EngineeringMarkInfo>();
        foreach (var word in _words.Words) { var first = ApplyViewport(new(plot.Left + word.X, plot.Top + word.Y), plot); var last = ApplyViewport(new(plot.Left + word.X + word.Width, plot.Top + word.Y + word.Height), plot); var source = data.Points[word.SourceIndex]; marks.Add(Single(source, new RectangleGeometry(new Rect(first, last)), Brushes.Transparent, new((first.X + last.X) / 2, (first.Y + last.Y) / 2), Identity(source), word.Source.Weight, $"{word.Source.Text}: {ValueText(word.Source.Weight)}")); } return marks;
    }
    protected override void RenderEngineeringMarks(DrawingContext context, IReadOnlyList<EngineeringMarkInfo> marks, EngineeringPointHit? selected, EngineeringPointHit? active)
    { for (var i = 0; i < marks.Count; i++) if (i == selected?.Index || i == active?.Index) context.DrawGeometry(null, new Pen(Foreground ?? Brushes.Gray, 2), marks[i].Geometry); }
    protected override void RenderAnnotations(DrawingContext context, Rect plot)
    { if (_words is null || !ShowLabels) return; using var clip = context.PushClip(plot); foreach (var word in _words.Words) { var position = ApplyViewport(new(plot.Left + word.X + 2, plot.Top + word.Y + 2), plot); context.DrawText(Text(word.Source.Text, word.FontSize * Zoom, BrushAt(word.SourceIndex)), position); } }
    protected override void ClearProjection(Exception error) { _words = null; base.ClearProjection(error); }
}
