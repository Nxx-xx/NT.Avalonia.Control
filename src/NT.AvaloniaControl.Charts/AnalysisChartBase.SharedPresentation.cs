using System.Globalization;
using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public abstract partial class AnalysisChartBase
{
    public static readonly StyledProperty<IDataTemplate?> LegendItemTemplateProperty = EngineeringChartBase.LegendItemTemplateProperty.AddOwner<AnalysisChartBase>();
    public static readonly StyledProperty<IDataTemplate?> DataLabelTemplateProperty = EngineeringChartBase.DataLabelTemplateProperty.AddOwner<AnalysisChartBase>();
    public static readonly StyledProperty<int> MaxLegendItemsProperty = EngineeringChartBase.MaxLegendItemsProperty.AddOwner<AnalysisChartBase>();
    public static readonly StyledProperty<int> MaxDataLabelsProperty = EngineeringChartBase.MaxDataLabelsProperty.AddOwner<AnalysisChartBase>();

    /// <summary>Optional passive permanent legend controls. Context is ChartDataLegendItemContext. Null retains the family's drawn legend; null Build hides an entry. Controls are included in raster export.</summary>
    public IDataTemplate? LegendItemTemplate { get => GetValue(LegendItemTemplateProperty); set => SetValue(LegendItemTemplateProperty, value); }
    /// <summary>Optional passive permanent data-label controls. Context is ChartDataLabelItemContext. Current family label/value gates apply; null Build hides a candidate. Controls are included in raster export.</summary>
    public IDataTemplate? DataLabelTemplate { get => GetValue(DataLabelTemplateProperty); set => SetValue(DataLabelTemplateProperty, value); }
    /// <summary>Maximum current shared legend entries, including semantic scales; zero hides the shared legend. Range 0..1024.</summary>
    public int MaxLegendItems { get => GetValue(MaxLegendItemsProperty); set => SetValue(MaxLegendItemsProperty, value); }
    /// <summary>Maximum current shared label candidates in family order; zero hides labels. Range 0..4096.</summary>
    public int MaxDataLabels { get => GetValue(MaxDataLabelsProperty); set => SetValue(MaxDataLabelsProperty, value); }
    protected bool UsesDataLabelTemplate => DataLabelTemplate is not null;
    protected bool UsesLegendItemTemplate => LegendItemTemplate is not null;
    protected virtual bool IsSharedLegendVisible => true;
    protected virtual bool HasFamilyLegendArea => false;
    protected virtual Rect GetSharedLegendArea(Rect plot, Size size) => new(plot.Left, Math.Min(size.Height, plot.Bottom + 4), plot.Width, Math.Max(0, size.Height - Padding.Bottom - 8 - plot.Bottom - 4));

    private ChartDataLegacyTooltipOverlay? _professionalLegacyTooltip;
    private void RefreshProfessionalLegacyTooltip()
    {
        if (!_attached || HasProfessionalTrackball || !IsEffectivelyVisible || !IsEffectivelyEnabled || !IsTooltipEnabled || TooltipContentTemplate is not null || _active is null || (!UsesDataLabelTemplate && !UsesLegendItemTemplate && !HasProfessionalPlotAreaContentPresentation))
        { ReleaseProfessionalLegacyTooltip(); return; }
        var overlay = _professionalLegacyTooltip ??= new ChartDataLegacyTooltipOverlay(DrawProfessionalLegacyTooltip);
        AttachDataTooltipOverlay(overlay, () => ReferenceEquals(_professionalLegacyTooltip, overlay));
        overlay.InvalidateVisual(); InvalidateArrange();
    }
    private void ReleaseProfessionalLegacyTooltip()
    {
        var overlay = _professionalLegacyTooltip; _professionalLegacyTooltip = null;
        if (overlay is not null) DetachDataTooltipOverlay(overlay);
    }
    private void ArrangeProfessionalLegacyTooltip(Size size)
    { if (_professionalLegacyTooltip is { } overlay) { overlay.Measure(size); overlay.Arrange(new Rect(size)); } }
    private void DrawProfessionalLegacyTooltip(DrawingContext context)
    {
        if (HasProfessionalTrackball || _dataTooltipExportDepth != 0 || !_attached || !IsEffectivelyVisible || !IsEffectivelyEnabled || !IsTooltipEnabled || TooltipContentTemplate is not null || _dirty) return;
            if (_active is { } hit)
            {
                var mark = _marks.FirstOrDefault(mark => mark.IsVisible && mark.Data.SourceIndex == hit.Index);
                if (mark is not null) DrawLabel(context, mark.TooltipText ?? $"{mark.Data.Category}: {FormatValue(hit.Point.Y)}", new(Bounds.Width / 2, Math.Max(FontSize, Bounds.Height - FontSize)), Math.Max(0, Bounds.Width - 16));
            }
    }

    private ChartDataPresentationPresenter? _dataLegend, _dataLabels;
    private long _presentationContextsRevision = -1, _presentationContextGeneration;
    private object? _presentationContextFrame;
    private IReadOnlyList<ChartDataLegendItemContext> _presentationLegendItems = [];
    private IReadOnlyList<ChartDataLabelItemContext> _presentationLabelItems = [];
    private int _permanentExportDepth;
    private bool _permanentPresentationDeferred, _presentationContextsBuilding;
    private ChartDataPresentationPresenter DataLegend => _dataLegend ??= CreatePermanentPresenter(true);
    private ChartDataPresentationPresenter DataLabels => _dataLabels ??= CreatePermanentPresenter(false);
    private ChartDataPresentationPresenter CreatePermanentPresenter(bool legend)
    {
        var presenter = new ChartDataPresentationPresenter(this, legend, () => legend ? LegendItemTemplate : DataLabelTemplate,
            () => ReadPermanentPresentation(legend), RefreshPermanentPresentations);
        if (_attached) presenter.Attached();
        return presenter;
    }

    /// <summary>Current bounded original-data legend readings, including complete aggregate memberships. Existing family-specific LegendItems collections retain their original contract.</summary>
    public IReadOnlyList<ChartDataLegendItemContext> GetLegendItems() { VerifyAccess(); UpdatePresentationContexts(); return _presentationLegendItems; }
    /// <summary>Current bounded label readings in family order; no invented aggregate observation or series.</summary>
    public IReadOnlyList<ChartDataLabelItemContext> GetDataLabels() { VerifyAccess(); UpdatePresentationContexts(); return _presentationLabelItems; }

    private Rect ReserveSharedLegendArea(Rect plot)
    {
        if (!UsesLegendItemTemplate || !IsSharedLegendVisible || HasFamilyLegendArea || MaxLegendItems == 0) return plot;
        return new(plot.Left, plot.Top, plot.Width, Math.Max(0, plot.Height - Math.Min(64, plot.Height / 3)));
    }

    private void UpdatePresentationContexts()
    {
        // Family builders and business formatters may query this API themselves.
        // Reentrant reads see the previous complete batch, never a partial batch.
        if (_presentationContextsBuilding || _building || _rendering) return;
        _presentationContextsBuilding = true;
        try
        {
            var marks = GetMarks();
            if (_dirty || _building || _rendering) return;
            if (_presentationContextsRevision == _revision && ReferenceEquals(_presentationContextFrame, marks)) return;
            var revision = _revision; var businessContext = DataContext;
            IReadOnlyList<ChartDataLegendItemContext> legendItems = IsSharedLegendVisible ? Array.AsReadOnly(CreateLegendItems(marks).Take(MaxLegendItems).ToArray()) : [];
            IReadOnlyList<ChartDataLabelItemContext> labelItems = Array.AsReadOnly(CreateDataLabels(marks).Where(item => double.IsFinite(item.Anchor.X) && double.IsFinite(item.Anchor.Y) && new Rect(Bounds.Size).Contains(item.Anchor)).Take(MaxDataLabels).ToArray());
            if (revision == _revision && !_dirty && ReferenceEquals(marks, _marks) && ReferenceEquals(businessContext, DataContext))
            {
                _presentationLegendItems = legendItems; _presentationLabelItems = labelItems;
                _presentationContextsRevision = revision; _presentationContextFrame = marks; _presentationContextGeneration++;
            }
            else { _presentationLegendItems = []; _presentationLabelItems = []; _presentationContextsRevision = -1; _presentationContextFrame = null; }
        }
        catch
        {
            _presentationLegendItems = []; _presentationLabelItems = []; _presentationContextsRevision = -1; _presentationContextFrame = null;
            throw;
        }
        finally { _presentationContextsBuilding = false; if (_permanentPresentationDeferred) PermanentPresentationRenderFinished(); }
    }

    private ChartDataPresentationReading? ReadPermanentPresentation(bool legend)
    {
        if ((!_attached && _permanentExportDepth == 0) || !IsEffectivelyVisible || _dirty || _building || _rendering || _presentationContextsBuilding) return null;
        UpdatePresentationContexts();
        var revision = _revision; var attachment = _attachmentRevision; var businessContext = DataContext;
        var slots = legend
            ? _presentationLegendItems.Select(item => new ChartDataPresentationSlot(item, item.Anchor, item.Bounds)).ToArray()
            : _presentationLabelItems.Select(item => new ChartDataPresentationSlot(item, item.Anchor, item.Bounds, item.GeometryClip)).ToArray();
        var area = legend ? GetSharedLegendArea(_plot, Bounds.Size) : new Rect(Bounds.Size);
        if (!legend && UsesLegendItemTemplate && _presentationLegendItems.Count > 0)
        {
            var legendArea = GetSharedLegendArea(_plot, Bounds.Size);
            if (legendArea.Height > 0) area = new(area.Left, area.Top, area.Width, Math.Max(0, Math.Min(area.Bottom, legendArea.Top - 4) - area.Top));
        }
        return new(Array.AsReadOnly(slots), _presentationContextGeneration, area, () => revision == _revision && attachment == _attachmentRevision &&
            (_attached || _permanentExportDepth > 0) && IsEffectivelyVisible && !_dirty && ReferenceEquals(businessContext, DataContext));
    }

    private void RefreshPermanentPresentations()
    {
        if (!UsesLegendItemTemplate && !UsesDataLabelTemplate)
        { _dataLegend?.Invalidate(release: true, queue: false); _dataLabels?.Invalidate(release: true, queue: false); ReleaseProfessionalLegacyTooltip(); return; }
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        if ((!_attached && _permanentExportDepth == 0) || !IsEffectivelyVisible)
        { _dataLegend?.Invalidate(release: true, queue: false); _dataLabels?.Invalidate(release: true, queue: false); return; }
        if (_rendering || _building || _presentationContextsBuilding) { _permanentPresentationDeferred = true; return; }
        _ = GetMarks(); RefreshProfessionalLegacyTooltip();
        if (LegendItemTemplate is not null) DataLegend.Synchronize(); else _dataLegend?.Invalidate(release: true, queue: false);
        if (DataLabelTemplate is not null) DataLabels.Synchronize(); else _dataLabels?.Invalidate(release: true, queue: false);
    }

    private bool PermanentPresentationPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == LegendItemTemplateProperty) { DataLegend.TemplateChanged(); InvalidateAnalysis(); return true; }
        if (change.Property == DataLabelTemplateProperty) { DataLabels.TemplateChanged(); InvalidateAnalysis(); return true; }
        if (change.Property == MaxLegendItemsProperty || change.Property == MaxDataLabelsProperty) { InvalidateAnalysis(); return true; }
        return false;
    }

    private void InvalidatePermanentPresentations()
    {
        _presentationContextsRevision = -1; _presentationContextFrame = null; _presentationLegendItems = []; _presentationLabelItems = [];
        _dataLegend?.Invalidate(); _dataLabels?.Invalidate(); ReleaseProfessionalLegacyTooltip();
        if (LegendItemTemplate is not null || DataLabelTemplate is not null) InvalidateMeasure();
    }
    private void PermanentPresentationRenderFinished()
    {
        if (!_permanentPresentationDeferred) return;
        _permanentPresentationDeferred = false; _dataLegend?.RequestRefresh(); _dataLabels?.RequestRefresh();
    }
    private void MeasurePermanentPresentations(Size size)
    { if (_dataLegend is null && _dataLabels is null && !UsesLegendItemTemplate && !UsesDataLabelTemplate) return; RefreshPermanentPresentations(); _dataLegend?.Measure(size); _dataLabels?.Measure(size); }
    private void ArrangePermanentPresentations(Size size)
    { RefreshPermanentPresentations(); _dataLegend?.Measure(size); _dataLabels?.Measure(size); _dataLegend?.Arrange(size); _dataLabels?.Arrange(size); }

    private void PreparePermanentPresentationExport()
    {
        if (DataSelectionBusy || IsProfessionalPlotAreaContentBusy || ProfessionalTrackballBusy || _presentationContextsBuilding || _permanentExportDepth != 0 || _rendering || _building || _dataTooltip?.IsBusy == true || _dataLegend?.IsBusy == true || _dataLabels?.IsBusy == true)
            throw new InvalidOperationException("A chart cannot be exported while a presentation template, layout or data projection is updating.");
        _permanentExportDepth++;
        if (!_attached) { _dirty = true; _presentationContextsRevision = -1; }
        _ = GetMarks(); PrepareProfessionalPlotAreaContentExport(); ArrangePermanentPresentations(Bounds.Size); PrepareDataSelectionExport();
    }
    private void FinishPermanentPresentationExport()
    {
        if (_permanentExportDepth > 0) _permanentExportDepth--;
        FinishProfessionalPlotAreaContentExport();
        DataSelectionExportFinished();
        if (!_attached) { _dataLegend?.Detached(); _dataLabels?.Detached(); _presentationContextsRevision = -1; _presentationContextFrame = null; _presentationLegendItems = []; _presentationLabelItems = []; }
    }
    private void RenderPermanentPresentationExport(DrawingContext context)
    { _dataLabels?.RenderExport(context); _dataLegend?.RenderExport(context); }

    protected virtual IEnumerable<ChartDataLegendItemContext> CreateLegendItems(IReadOnlyList<AnalysisMarkInfo> marks)
    {
        foreach (var mark in marks.Where(mark => mark.IsVisible))
            yield return new(Array.AsReadOnly(new[] { mark.Data }), Convert.ToString(mark.Data.Category, CultureInfo.CurrentCulture) ?? $"Item {mark.Data.SourceIndex + 1}", mark.LabelPosition)
            { Bounds = mark.Bounds, Brush = mark.Fill, Value = mark.Data.Point.Y, Key = mark.Data.StableId, BusinessDataContext = DataContext, PointHit = Hit(mark) };
    }
    protected virtual IEnumerable<ChartDataLabelItemContext> CreateDataLabels(IReadOnlyList<AnalysisMarkInfo> marks)
    {
        if (!ShowLabels && !ShowValues) yield break;
        foreach (var mark in marks.Where(mark => mark.IsVisible && mark.Bounds.Height >= FontSize * 1.2))
        {
            var text = mark.Label ?? string.Join(" · ", new[] { ShowLabels ? Convert.ToString(mark.Data.Category, CultureInfo.CurrentCulture) : null, ShowValues ? FormatValue(mark.Data.Point.Y) : null }.Where(value => !string.IsNullOrEmpty(value)));
            yield return new(Array.AsReadOnly(new[] { mark.Data }), text, mark.LabelPosition)
            { Bounds = mark.Bounds, Brush = mark.LabelBrush ?? ChartLabelContrast.OnFill(mark.Fill, Foreground ?? Brushes.Gray), Value = mark.Data.Point.Y, BusinessDataContext = DataContext, PointHit = Hit(mark) };
        }
    }
}
