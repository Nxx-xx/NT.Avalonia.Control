using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls.Templates;
using Avalonia.Animation.Easings;
using Avalonia;
using Avalonia.Media;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public enum UsageExportFormat { Png, Jpeg }
public enum UsageExportBackground { TransparentOrWhite, White, LightBlue }

public sealed partial class ChartUsageViewModel : INotifyPropertyChanged
{
    private ChartLineInterpolation _lineAreaInterpolation = ChartLineInterpolation.Smooth;
    private ChartStackMode _lineAreaStackMode = ChartStackMode.Stacked;
    private double _lineAreaBaseline;
    private bool _lineAreaGap = true;
    public ChartLineInterpolation LineAreaInterpolation { get => _lineAreaInterpolation; set { if (_lineAreaInterpolation == value) return; _lineAreaInterpolation = value; Notify(); } }
    public ChartStackMode LineAreaStackMode { get => _lineAreaStackMode; set { if (_lineAreaStackMode == value) return; _lineAreaStackMode = value; Notify(); } }
    public double LineAreaBaseline { get => _lineAreaBaseline; set { if (_lineAreaBaseline == value) return; _lineAreaBaseline = value; Notify(); } }
    public bool LineAreaGap { get => _lineAreaGap; set { if (_lineAreaGap == value) return; _lineAreaGap = value; Notify(); Notify(nameof(LineAreaSecondaryPoints)); } }
    public ChartLineInterpolation[] LineAreaInterpolations { get; } = Enum.GetValues<ChartLineInterpolation>();
    public ChartStackMode[] LineAreaStackModes { get; } = Enum.GetValues<ChartStackMode>();
    public double[] LineAreaBaselines { get; } = [0, -20, 10];
    public ChartPoint[] LineAreaPrimaryPoints { get; } = [new(1, 20), new(3, -20), new(5, 30)];
    public ChartPoint[] LineAreaSecondaryPoints => [new(1, 30), new(3, -30), new(5, LineAreaGap ? double.NaN : 70)];
    public ChartPoint[] LineAreaLinePoints { get; } = [new(1, 15), new(3, -15), new(5, 40)];
    public RangeAreaUsageRecord[] LineAreaRangeRecords { get; } = [new("usage-range-a", 1, 10, 40), new("usage-range-b", 3, 20, 70), new("usage-range-c", 5, 15, 55)];
    private bool _accessibilityKeyboardEnabled = true;
    private bool _accessibilityPointBrowsing = true;
    private ChartHighContrastMode _accessibilityContrast;
    private string _accessibilityStatus = "Tab 聚焦后使用方向键浏览，Enter 调用当前点。";
    public bool AccessibilityKeyboardEnabled { get => _accessibilityKeyboardEnabled; set { if (_accessibilityKeyboardEnabled == value) return; _accessibilityKeyboardEnabled = value; Notify(); } }
    public bool AccessibilityPointBrowsing { get => _accessibilityPointBrowsing; set { if (_accessibilityPointBrowsing == value) return; _accessibilityPointBrowsing = value; Notify(); } }
    public ChartHighContrastMode AccessibilityContrast { get => _accessibilityContrast; set { if (_accessibilityContrast == value) return; _accessibilityContrast = value; Notify(); } }
    public ChartHighContrastMode[] AccessibilityContrasts { get; } = Enum.GetValues<ChartHighContrastMode>();
    public string AccessibilityStatus { get => _accessibilityStatus; set { if (_accessibilityStatus == value) return; _accessibilityStatus = value; Notify(); } }
    public ICommand AccessibilityPointInvokedCommand => new ActionCommand(() => AccessibilityStatus = "已调用当前点，可将此命令接入业务详情。" );
    public TrackballUsageRecord[] AccessibilityRecords { get; } = [new("access-a", "开始", 10, 25), new("access-missing", "缺失", 25, double.NaN), new("access-b", "检查", 45, 65), new("access-c", "结束", 80, 40)];
    public ChartPoint[] AccessibilityBars { get; } = [new(25, 35), new(60, 55), new(90, 75)];
    public ChartPoint[] AccessibilityScatter { get; } = [new(20, 1850), new(55, 1450), new(85, 1700)];
    private UsageExportFormat _exportFormat;
    private UsageExportBackground _exportBackground = UsageExportBackground.White;
    private decimal? _exportPixelWidth = 1200, _exportPixelHeight = 700;
    private double _exportDpi = 96;
    private int _exportQuality = 90;
    private bool _exportInferredSize, _exportIsBusy;
    private string _exportStatus = "选择选项后保存；本地路径失败或取消时保留原文件。";
    public UsageExportFormat[] ExportFormats { get; } = Enum.GetValues<UsageExportFormat>();
    public UsageExportBackground[] ExportBackgrounds { get; } = Enum.GetValues<UsageExportBackground>();
    public double[] ExportDpis { get; } = [96, 144, 192];
    public int[] ExportQualities { get; } = [40, 60, 77, 90, 100];
    public UsageExportFormat ExportFormat { get => _exportFormat; set { if (_exportFormat == value) return; _exportFormat = value; Notify(); } }
    public UsageExportBackground ExportBackground { get => _exportBackground; set { if (_exportBackground == value) return; _exportBackground = value; Notify(); } }
    public decimal? ExportPixelWidth { get => _exportPixelWidth; set { if (_exportPixelWidth == value) return; _exportPixelWidth = value; Notify(); } }
    public decimal? ExportPixelHeight { get => _exportPixelHeight; set { if (_exportPixelHeight == value) return; _exportPixelHeight = value; Notify(); } }
    public double ExportDpi { get => _exportDpi; set { if (_exportDpi == value) return; _exportDpi = value; Notify(); } }
    public int ExportQuality { get => _exportQuality; set { if (_exportQuality == value) return; _exportQuality = value; Notify(); } }
    public bool ExportInferredSize { get => _exportInferredSize; set { if (_exportInferredSize == value) return; _exportInferredSize = value; Notify(); } }
    public bool ExportIsBusy { get => _exportIsBusy; set { if (_exportIsBusy == value) return; _exportIsBusy = value; Notify(); Notify(nameof(ExportCanStart)); } }
    public bool ExportCanStart => !ExportIsBusy;
    public string ExportStatus { get => _exportStatus; set { if (_exportStatus == value) return; _exportStatus = value; Notify(); } }
    internal ChartExportOptions CreateExportOptions() => new()
    {
        PixelSize = ExportInferredSize ? null : new PixelSize(checked((int)(ExportPixelWidth ?? 0)), checked((int)(ExportPixelHeight ?? 0))),
        Dpi = ExportDpi, JpegQuality = ExportQuality,
        Background = ExportBackground switch { UsageExportBackground.White => Brushes.White, UsageExportBackground.LightBlue => new SolidColorBrush(Color.Parse("#E1F1FF")), _ => null },
    };
    private bool _animationEnabled, _animationPaused, _animationReduceMotion;
    private TimeSpan _animationDuration = TimeSpan.FromMilliseconds(800), _animationDelay = TimeSpan.FromMilliseconds(150);
    private IEasing _animationEasing = new CubicEaseOut();
    private int _animationPointLimit = 10_000;
    private ChartViewport? _animationViewport;
    private ChartPoint[] _animationLinePoints = [new(1, 25), new(3, 65), new(5, 40), new(7, 80), new(9, 55)];
    public bool AnimationEnabled { get => _animationEnabled; set { if (_animationEnabled == value) return; _animationEnabled = value; Notify(); Notify(nameof(AnimationTriggers)); } }
    public ChartAnimationTriggers AnimationTriggers => AnimationEnabled ? ChartAnimationTriggers.All : ChartAnimationTriggers.None;
    public bool AnimationPaused { get => _animationPaused; set { if (_animationPaused == value) return; _animationPaused = value; Notify(); } }
    public bool AnimationReduceMotion { get => _animationReduceMotion; set { if (_animationReduceMotion == value) return; _animationReduceMotion = value; Notify(); } }
    public TimeSpan AnimationDuration { get => _animationDuration; set { if (_animationDuration == value) return; _animationDuration = value; Notify(); } }
    public TimeSpan AnimationDelay { get => _animationDelay; set { if (_animationDelay == value) return; _animationDelay = value; Notify(); } }
    public IEasing AnimationEasing { get => _animationEasing; set { if (ReferenceEquals(_animationEasing, value)) return; _animationEasing = value; Notify(); } }
    public int AnimationPointLimit { get => _animationPointLimit; set { if (_animationPointLimit == value) return; _animationPointLimit = value; Notify(); } }
    public ChartViewport? AnimationViewport { get => _animationViewport; set { if (_animationViewport == value) return; _animationViewport = value; Notify(); } }
    public ChartPoint[] AnimationLinePoints { get => _animationLinePoints; set { if (ReferenceEquals(_animationLinePoints, value)) return; _animationLinePoints = value; Notify(); } }
    public ChartPoint[] AnimationBarPoints { get; } = [new(2, 20), new(4, 45), new(6, 35), new(8, 60)];
    public ChartPoint[] AnimationScatterPoints { get; } = [new(1.5, 75), new(4.5, 85), new(7.5, 65)];
    public TimeSpan[] AnimationDurations { get; } = [TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(800), TimeSpan.FromMilliseconds(1600)];
    public TimeSpan[] AnimationDelays { get; } = [TimeSpan.Zero, TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(600)];
    public ICommand AnimationUpdateCommand => new ActionCommand(() => AnimationLinePoints = AnimationLinePoints.Select(point => new ChartPoint(point.X, 100 - point.Y)).ToArray());
    public ICommand AnimationViewCommand => new ActionCommand(() => AnimationViewport = AnimationViewport is null ? new ChartViewport(2, 8, 20, 90) : null);
    private double _annotationCornerRadius = 12, _annotationRadiusX = 10, _annotationRadiusY = 120;
    private string _annotationShapeLabel = "观察区间", _annotationOverlayMessage = "多轴参考 · 可缩放查看";
    public double AnnotationCornerRadius { get => _annotationCornerRadius; set { if (_annotationCornerRadius == value) return; _annotationCornerRadius = value; Notify(); } }
    public double AnnotationRadiusX { get => _annotationRadiusX; set { if (_annotationRadiusX == value) return; _annotationRadiusX = value; Notify(); } }
    public double AnnotationRadiusY { get => _annotationRadiusY; set { if (_annotationRadiusY == value) return; _annotationRadiusY = value; Notify(); } }
    public string AnnotationShapeLabel { get => _annotationShapeLabel; set { if (_annotationShapeLabel == value) return; _annotationShapeLabel = value; Notify(); } }
    public string AnnotationOverlayMessage { get => _annotationOverlayMessage; set { if (_annotationOverlayMessage == value) return; _annotationOverlayMessage = value; Notify(); } }
    private bool _inputTouchEnabled = true;
    private double _inputTouchThreshold = 8, _inputPanSensitivity = 48, _inputZoomSensitivity = 1.2;
    private ChartWheelMode _inputWheelMode = ChartWheelMode.Auto;
    private ChartInputRoutingMode _inputRoutingMode = ChartInputRoutingMode.Auto;
    public bool InputTouchEnabled { get => _inputTouchEnabled; set { if (_inputTouchEnabled == value) return; _inputTouchEnabled = value; Notify(); } }
    public double InputTouchThreshold { get => _inputTouchThreshold; set { if (_inputTouchThreshold == value) return; _inputTouchThreshold = value; Notify(); } }
    public double InputPanSensitivity { get => _inputPanSensitivity; set { if (_inputPanSensitivity == value) return; _inputPanSensitivity = value; Notify(); } }
    public double InputZoomSensitivity { get => _inputZoomSensitivity; set { if (_inputZoomSensitivity == value) return; _inputZoomSensitivity = value; Notify(); } }
    public ChartWheelMode InputWheelMode { get => _inputWheelMode; set { if (_inputWheelMode == value) return; _inputWheelMode = value; Notify(); } }
    public ChartInputRoutingMode InputRoutingMode { get => _inputRoutingMode; set { if (_inputRoutingMode == value) return; _inputRoutingMode = value; Notify(); } }
    public ChartWheelMode[] InputWheelModes { get; } = Enum.GetValues<ChartWheelMode>();
    public ChartInputRoutingMode[] InputRoutingModes { get; } = Enum.GetValues<ChartInputRoutingMode>();
    private ChartRange _selectionRange = new(0, 1);
    private IReadOnlyList<ChartSeries> _selectedSeries = Array.Empty<ChartSeries>();
    private ChartSelectionMode _seriesSelectionMode = ChartSelectionMode.Multiple;
    public ChartRange SelectionRange { get => _selectionRange; set { if (_selectionRange == value) return; _selectionRange = value; Notify(); } }
    public IReadOnlyList<ChartSeries> SelectedSeries { get => _selectedSeries; set { if (ReferenceEquals(_selectedSeries, value)) return; _selectedSeries = value; Notify(); } }
    public ChartSelectionMode SeriesSelectionMode { get => _seriesSelectionMode; set { if (_seriesSelectionMode == value) return; _seriesSelectionMode = value; Notify(); } }
    public ChartPoint[] SelectionLinePoints { get; } = [new(10, 30), new(30, 65), new(50, 45), new(70, 80), new(90, 60)];
    public ChartPoint[] SelectionScatterPoints { get; } = [new(15, 85), new(45, 88), new(75, 92)];
    public ChartPoint[] SelectionSecondaryPoints { get; } = [new(100, 30), new(300, 65), new(500, 45), new(700, 80), new(900, 60)];
    private ObservableCollection<ChartSeries> _seriesSource;
    private ChartViewport? _viewport;
    private ChartPointHit? _selectedPoint;
    private string _axisTitle = "销售额 / 万元";
    private string _seriesTitle = "门店销售";
    private string _status = "修改数值或追加月份，观察两张图同步更新。";
    private double? _secondaryMinimum = .2;
    private double? _secondaryMaximum = .8;
    private double _bandStart = 1;
    private double _breakStart = 35;
    private bool _showMinorGrid = true;
    private bool _isStoreVisible = true;
    private bool _showCrosshair = true;
    private bool _showDataLabels;
    private bool _isLegendFloating;
    private ChartSelectionMode _selectionMode = ChartSelectionMode.Single;
    private IReadOnlyList<ChartPointHit> _selectedPoints = Array.Empty<ChartPointHit>();
    private ChartTooltipMode _readingTooltipMode = ChartTooltipMode.SharedY;
    private ChartCrosshairDirection _readingCrosshairDirection = ChartCrosshairDirection.Both;
    private bool _showReadingLabels = true;
    private bool _showReadingCrosshair = true;
    private bool _useRichReadingTooltip = true;
    private IDataTemplate? _readingTooltipTemplate;
    private IDataTemplate? _richReadingTemplate;
    private IDataTemplate? _legendControlTemplate;
    private IDataTemplate? _dataLabelControlTemplate;
    private bool _useLegendControlTemplate = true;
    private bool _useDataLabelControlTemplate = true;
    private string _templateSeriesTitle = "直营订单";
    private ChartTrackballMode _trackballMode = ChartTrackballMode.Vertical;
    private ChartTrackballDisplayMode _trackballDisplayMode = ChartTrackballDisplayMode.GroupAllPoints;
    private ChartTrackballSnapMode _trackballSnapMode = ChartTrackballSnapMode.LinearInterpolation;
    private ChartTooltipMode _trackballTooltipMode = ChartTooltipMode.NearestPoint;
    private bool _useBusinessTrackball = true;
    private IDataTemplate? _businessTrackballTemplate;

    public ChartUsageViewModel()
    {
        Records = [new("jan", "一月", 32), new("feb", "二月", 49), new("mar", "三月", 43), new("apr", "四月", 68)];
        _seriesSource = CreateSeries();
        ResetViewportCommand = new ActionCommand(() => Viewport = null);
        UpdateValueCommand = new ActionCommand(() => { Records[0].Value += 12; Status = "一月销售额已更新；两张图保留原数据项。"; });
        AppendCommand = new ActionCommand(() => { var index = Records.Count + 1; Records.Add(new($"month-{index}", $"{index}月", 40 + index * 7)); ResetViewportCommand.Execute(null); Status = $"已追加第 {index} 个月。"; });
        ReplaceSeriesCommand = new ActionCommand(() => { SeriesSource = CreateSeries(bar: SeriesSource.FirstOrDefault() is LineSeries); Status = "已替换整个系列集合，原始业务数据保持不变。"; });
        PointInvokedCommand = new ActionCommand(() => Status = SelectedPoint is { } selected ? $"已选择“{selected.Series.Title}”的第 {selected.Index + 1} 条数据。" : "选择已清除。");
        FocusSecondaryCommand = new ActionCommand(() => { SecondaryMinimum = .3; SecondaryMaximum = .7; Status = "成功率单独聚焦到 30%–70%。"; });
        ChangeSelectionModeCommand = new ActionCommand(() =>
        {
            SelectionMode = SelectionMode switch { ChartSelectionMode.None => ChartSelectionMode.Single, ChartSelectionMode.Single => ChartSelectionMode.Multiple, _ => ChartSelectionMode.None };
            Status = SelectionMode switch { ChartSelectionMode.None => "已关闭点选择。", ChartSelectionMode.Single => "已切换为单点选择。", _ => "已切换为多点选择，可用 Shift + 拖动框选。" };
        });
        UpdateTemplateValueCommand = new ActionCommand(() => { TemplateRecords[0].Value = TemplateRecords[0].Value == 37 ? 54 : 37; Status = "直营订单已更新，业务标签同步显示新的订单数。"; });
        TrackballUpdateCommand = new ActionCommand(() => { TrackballPrimaryRecords[1].Y = TrackballPrimaryRecords[1].Y == 60 ? 80 : 60; Status = "09:40 端点已更新，插值估算保留两端业务身份。"; });
    }

    public ObservableCollection<ChartUsageRecord> Records { get; }
    public ObservableCollection<TrackballUsageRecord> TrackballPrimaryRecords { get; } = [new("track-a0", "09:00", 1, 20), new("track-a1", "09:40", 5, 60), new("track-a2", "10:20", 9, 35)];
    public ObservableCollection<TrackballUsageRecord> TrackballSecondaryRecords { get; } = [new("track-b0", "批次 A", 0, 80), new("track-b1", "批次 B", 4, 40), new("track-b2", "批次 C", 8, 70)];
    public ChartTrackballMode[] TrackballModes { get; } = [ChartTrackballMode.None, ChartTrackballMode.Vertical, ChartTrackballMode.Horizontal];
    public ChartTrackballDisplayMode[] TrackballDisplayModes { get; } = [ChartTrackballDisplayMode.GroupAllPoints, ChartTrackballDisplayMode.FloatAllPoints];
    public ChartTrackballSnapMode[] TrackballSnapModes { get; } = [ChartTrackballSnapMode.NearestPoint, ChartTrackballSnapMode.LinearInterpolation];
    public ChartTooltipMode[] TrackballTooltipModes { get; } = [ChartTooltipMode.NearestPoint, ChartTooltipMode.NearestSegment, ChartTooltipMode.InterpolateX, ChartTooltipMode.InterpolateY, ChartTooltipMode.SharedX, ChartTooltipMode.SharedY];
    public ChartTrackballMode TrackballMode { get => _trackballMode; set { if (_trackballMode == value) return; _trackballMode = value; Notify(); Notify(nameof(IsTrackballTooltipEnabled)); } }
    public ChartTrackballDisplayMode TrackballDisplayMode { get => _trackballDisplayMode; set { if (_trackballDisplayMode == value) return; _trackballDisplayMode = value; Notify(); } }
    public ChartTrackballSnapMode TrackballSnapMode { get => _trackballSnapMode; set { if (_trackballSnapMode == value) return; _trackballSnapMode = value; Notify(); } }
    public ChartTooltipMode TrackballTooltipMode { get => _trackballTooltipMode; set { if (_trackballTooltipMode == value) return; _trackballTooltipMode = value; Notify(); } }
    public bool IsTrackballTooltipEnabled => TrackballMode == ChartTrackballMode.None;
    public bool UseBusinessTrackball { get => _useBusinessTrackball; set { if (_useBusinessTrackball == value) return; _useBusinessTrackball = value; Notify(); Notify(nameof(ActiveTrackballTemplate)); } }
    public IDataTemplate? ActiveTrackballTemplate => UseBusinessTrackball ? _businessTrackballTemplate : null;
    public void ConfigureTrackballTemplate(IDataTemplate template) { _businessTrackballTemplate = template; Notify(nameof(ActiveTrackballTemplate)); }
    public ObservableCollection<TemplateUsageRecord> TemplateRecords { get; } = [new("template-primary", "直营订单", 1, 37), new("template-second", "会员订单", 3, 62), new("template-third", "到店订单", 5, 45)];
    public ChartPoint[] TemplateTargets { get; } = [new(1, 70), new(3, 76), new(5, 81)];
    public ChartPoint[] TemplateObservations { get; } = [new(1.5, 21), new(3.5, 26), new(4.7, 32)];
    public string TemplateSeriesTitle { get => _templateSeriesTitle; set { if (_templateSeriesTitle == value) return; _templateSeriesTitle = value; Notify(); } }
    public bool UseLegendControlTemplate { get => _useLegendControlTemplate; set { if (_useLegendControlTemplate == value) return; _useLegendControlTemplate = value; Notify(); Notify(nameof(ActiveLegendTemplate)); } }
    public bool UseDataLabelControlTemplate { get => _useDataLabelControlTemplate; set { if (_useDataLabelControlTemplate == value) return; _useDataLabelControlTemplate = value; Notify(); Notify(nameof(ActiveDataLabelTemplate)); } }
    public IDataTemplate? ActiveLegendTemplate => UseLegendControlTemplate ? _legendControlTemplate : null;
    public IDataTemplate? ActiveDataLabelTemplate => UseDataLabelControlTemplate ? _dataLabelControlTemplate : null;
    public void ConfigureControlTemplates(IDataTemplate legend, IDataTemplate label)
    {
        _legendControlTemplate = legend; _dataLabelControlTemplate = label;
        Notify(nameof(ActiveLegendTemplate)); Notify(nameof(ActiveDataLabelTemplate));
    }
    public ObservableCollection<ChartUsageRecord> ReadingRecords { get; } = [new("response-a", "09:00", 20, 1), new("response-b", "09:30", 37, 2), new("response-c", "10:00", 62, 5)];
    public ObservableCollection<ChartUsageRecord> ReadingVolumeRecords { get; } = [new("volume-a", "批次 A", 10, 3.5), new("volume-b", "批次 B", Math.Pow(10, 1.48), 7), new("volume-c", "批次 C", 1000, 9)];
    public ChartTooltipMode[] ReadingTooltipModes { get; } = [ChartTooltipMode.NearestPoint, ChartTooltipMode.SharedX, ChartTooltipMode.SharedY];
    public ChartCrosshairDirection[] ReadingCrosshairDirections { get; } = [ChartCrosshairDirection.Both, ChartCrosshairDirection.Horizontal, ChartCrosshairDirection.Vertical];
    public ChartTooltipMode ReadingTooltipMode { get => _readingTooltipMode; set { if (_readingTooltipMode == value) return; _readingTooltipMode = value; Notify(); } }
    public ChartCrosshairDirection ReadingCrosshairDirection { get => _readingCrosshairDirection; set { if (_readingCrosshairDirection == value) return; _readingCrosshairDirection = value; Notify(); } }
    public bool ShowReadingLabels { get => _showReadingLabels; set { if (_showReadingLabels == value) return; _showReadingLabels = value; Notify(); } }
    public bool ShowReadingCrosshair { get => _showReadingCrosshair; set { if (_showReadingCrosshair == value) return; _showReadingCrosshair = value; Notify(); } }
    public bool UseRichReadingTooltip { get => _useRichReadingTooltip; set { if (_useRichReadingTooltip == value) return; _useRichReadingTooltip = value; ReadingTooltipTemplate = value ? _richReadingTemplate : null; Notify(); } }
    public IDataTemplate? ReadingTooltipTemplate { get => _readingTooltipTemplate; private set { if (ReferenceEquals(_readingTooltipTemplate, value)) return; _readingTooltipTemplate = value; Notify(); } }
    public void ConfigureReadingTemplate(IDataTemplate template) { _richReadingTemplate = template; ReadingTooltipTemplate = UseRichReadingTooltip ? template : null; }
    public ObservableCollection<ChartSeries> SeriesSource { get => _seriesSource; set { if (ReferenceEquals(value, _seriesSource)) return; _seriesSource = value; Notify(); } }
    public ChartViewport? Viewport { get => _viewport; set { if (_viewport == value) return; _viewport = value; Notify(); Notify(nameof(ViewportSummary)); } }
    public ChartPointHit? SelectedPoint { get => _selectedPoint; set { if (ReferenceEquals(_selectedPoint, value)) return; _selectedPoint = value; Notify(); } }
    public string AxisTitle { get => _axisTitle; set { if (_axisTitle == value) return; _axisTitle = value; Notify(); } }
    public string SeriesTitle { get => _seriesTitle; set { if (_seriesTitle == value) return; _seriesTitle = value; Notify(); } }
    public string Status { get => _status; set { if (_status == value) return; _status = value; Notify(); } }
    public double? SecondaryMinimum { get => _secondaryMinimum; set { if (_secondaryMinimum == value) return; _secondaryMinimum = value; Notify(); } }
    public double? SecondaryMaximum { get => _secondaryMaximum; set { if (_secondaryMaximum == value) return; _secondaryMaximum = value; Notify(); } }
    public double BandStart { get => _bandStart; set { if (_bandStart == value) return; _bandStart = value; Notify(); } }
    public double BreakStart { get => _breakStart; set { if (_breakStart == value) return; _breakStart = value; Notify(); } }
    public bool ShowMinorGrid { get => _showMinorGrid; set { if (_showMinorGrid == value) return; _showMinorGrid = value; Notify(); } }
    public bool IsStoreVisible { get => _isStoreVisible; set { if (_isStoreVisible == value) return; _isStoreVisible = value; Notify(); } }
    public bool ShowCrosshair { get => _showCrosshair; set { if (_showCrosshair == value) return; _showCrosshair = value; Notify(); } }
    public bool ShowDataLabels { get => _showDataLabels; set { if (_showDataLabels == value) return; _showDataLabels = value; Notify(); } }
    public bool IsLegendFloating { get => _isLegendFloating; set { if (_isLegendFloating == value) return; _isLegendFloating = value; Notify(); } }
    public ChartSelectionMode SelectionMode { get => _selectionMode; set { if (_selectionMode == value) return; _selectionMode = value; Notify(); } }
    public IReadOnlyList<ChartPointHit> SelectedPoints { get => _selectedPoints; set { value ??= Array.Empty<ChartPointHit>(); if (ReferenceEquals(_selectedPoints, value)) return; _selectedPoints = value; Notify(); } }
    public ChartPoint[] AxisValues { get; } = [new(0, 20), new(1, 28), new(2, 70), new(3, 85), new(4, 25)];
    public ChartPoint[] QualityValues { get; } = [new(0, .4), new(1, .55), new(2, .65), new(3, .5), new(4, .45)];
    public ObservableCollection<ChartScaleBreak> AxisBreaks { get; } = [new() { Start = 2.2, End = 2.7, CompressionRatio = .1 }];
    public string ViewportSummary => Viewport is { } view ? $"当前视图：X {view.XMinimum:0.#}–{view.XMaximum:0.#}，Y {view.YMinimum:0.#}–{view.YMaximum:0.#}" : "当前视图：自动适配全部数据";
    public ICommand UpdateValueCommand { get; }
    public ICommand AppendCommand { get; }
    public ICommand ReplaceSeriesCommand { get; }
    public ICommand ResetViewportCommand { get; }
    public ICommand PointInvokedCommand { get; }
    public ICommand FocusSecondaryCommand { get; }
    public ICommand ChangeSelectionModeCommand { get; }
    public ICommand UpdateTemplateValueCommand { get; }
    public ICommand TrackballUpdateCommand { get; }

    private ObservableCollection<ChartSeries> CreateSeries(bool bar = false)
    {
        ChartSeries series = bar ? new BarSeries() : new LineSeries();
        series.Title = "线上销售";
        series.ItemsSource = Records;
        series.CategoryPath = nameof(ChartUsageRecord.Month);
        series.ValuePath = nameof(ChartUsageRecord.Value);
        series.IdPath = nameof(ChartUsageRecord.Id);
        return [series];
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class ChartUsageRecord(string id, string month, double value, double position = 0) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    private double _value = value;
    public string Id { get; } = id;
    public string Month { get; } = month;
    public double Position { get; } = position;
    public double Value { get => _value; set { if (_value == value) return; _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
    // Used by the consumer probe to check that detaching a chart releases item subscriptions.
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}

public sealed class TemplateUsageRecord(string id, string name, double position, double value) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    private double _value = value;
    public string Id { get; } = id;
    public string Name { get; } = name;
    public double Position { get; } = position;
    public double Value { get => _value; set { if (_value == value) return; _value = value; _changed?.Invoke(this, new(nameof(Value))); } }
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}

public sealed class TrackballUsageRecord(string id, string caption, double x, double y) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    private double _y = y;
    public string Id { get; } = id;
    public string Caption { get; } = caption;
    public double X { get; } = x;
    public double Y { get => _y; set { if (_y == value) return; _y = value; _changed?.Invoke(this, new(nameof(Y))); } }
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}

public sealed class RangeAreaUsageRecord(string id, double x, double low, double high) : INotifyPropertyChanged
{
    private PropertyChangedEventHandler? _changed;
    private double _low = low, _high = high;
    public string Id { get; } = id;
    public double X { get; } = x;
    public double Low { get => _low; set { if (_low == value) return; _low = value; _changed?.Invoke(this, new(nameof(Low))); } }
    public double High { get => _high; set { if (_high == value) return; _high = value; _changed?.Invoke(this, new(nameof(High))); } }
    public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
    public event PropertyChangedEventHandler? PropertyChanged { add => _changed += value; remove => _changed -= value; }
}

internal sealed class ActionCommand(Action action) : ICommand
{
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => action();
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}
