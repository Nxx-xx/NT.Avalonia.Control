using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public abstract partial class AnalysisChartBase
{
    public static readonly StyledProperty<object?> PlotAreaContentProperty = CartesianChart.PlotAreaContentProperty.AddOwner<AnalysisChartBase>();
    public static readonly StyledProperty<IDataTemplate?> PlotAreaContentTemplateProperty = CartesianChart.PlotAreaContentTemplateProperty.AddOwner<AnalysisChartBase>();
    /// <summary>Passive content arranged and clipped in the actual plot, above painted glyphs and below permanent labels, legends and live readings.
    /// A Control without a template inherits the chart DataContext. Other content requires PlotAreaContentTemplate. Included in raster export.</summary>
    public object? PlotAreaContent { get => GetValue(PlotAreaContentProperty); set => SetValue(PlotAreaContentProperty, value); }
    /// <summary>Builds an unparented Control using ChartDataPlotAreaContext. Family layout fractions are visual coordinates, not invented data axes.
    /// The control is reused across data, view and layout changes. Null Build hides this reading; errors propagate after cleanup.</summary>
    public IDataTemplate? PlotAreaContentTemplate { get => GetValue(PlotAreaContentTemplateProperty); set => SetValue(PlotAreaContentTemplateProperty, value); }
    private ChartDataPlotAreaPresenter? _professionalPlotAreaContent;
    private bool _professionalPlotAreaDeferred;
    private ChartDataPlotAreaContext? _professionalPlotAreaContext;
    private object? _professionalPlotAreaFrame;
    private long _professionalPlotAreaContextRevision = -1;
    private ChartDataPlotAreaPresenter ProfessionalPlotAreaContent => _professionalPlotAreaContent ??= CreateProfessionalPlotAreaContent();
    private ChartDataPlotAreaPresenter CreateProfessionalPlotAreaContent()
    {
        var presenter = new ChartDataPlotAreaPresenter(this, () => PlotAreaContent, () => PlotAreaContentTemplate,
            ReadProfessionalPlotAreaContent, RefreshProfessionalPlotAreaContent);
        if (_attached) presenter.Attached();
        return presenter;
    }
    private bool HasProfessionalPlotAreaContentPresentation => PlotAreaContent is Control || PlotAreaContent is not null && PlotAreaContentTemplate is not null;
    private bool IsProfessionalPlotAreaContentBusy => _professionalPlotAreaContent?.IsBusy == true;
    private ChartDataPlotAreaReading? ReadProfessionalPlotAreaContent()
    {
        if ((!_attached && _permanentExportDepth == 0) || !IsEffectivelyVisible || _building || _rendering || PlotAreaContent is not { } content) return null;
        var marks = GetMarks();
        if (_dirty || _building || _rendering || _plot.Width <= 0 || _plot.Height <= 0) return null;
        var revision = _revision; var attachment = _attachmentRevision; var business = DataContext;
        if (_professionalPlotAreaContextRevision != revision || !ReferenceEquals(_professionalPlotAreaFrame, marks) ||
            _professionalPlotAreaContext is not { } current || !ReferenceEquals(current.Content, content) || !ReferenceEquals(current.DataContext, business))
        {
            _professionalPlotAreaContext = new(content, DataContext, _plot, 1, 0, 0, [], marks);
            _professionalPlotAreaFrame = marks; _professionalPlotAreaContextRevision = revision;
        }
        return new(_professionalPlotAreaContext, revision, () => revision == _revision && attachment == _attachmentRevision &&
            (_attached || _permanentExportDepth > 0) && IsEffectivelyVisible && !_dirty &&
            ReferenceEquals(PlotAreaContent, content) && ReferenceEquals(DataContext, business) && ReferenceEquals(_marks, marks));
    }
    private void RefreshProfessionalPlotAreaContent()
    {
        if (!HasProfessionalPlotAreaContentPresentation || (!_attached && _permanentExportDepth == 0) || !IsEffectivelyVisible)
        { _professionalPlotAreaContent?.Invalidate(release: true, queue: false); return; }
        if (_building || _rendering) { _professionalPlotAreaDeferred = true; return; }
        ProfessionalPlotAreaContent.Synchronize();
    }
    private bool PlotAreaContentPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == PlotAreaContentProperty || change.Property == PlotAreaContentTemplateProperty)
        {
            _professionalPlotAreaContext = null; _professionalPlotAreaFrame = null; _professionalPlotAreaContextRevision = -1;
            ProfessionalPlotAreaContent.ContentChanged(); InvalidateAnalysis(); InvalidateMeasure(); InvalidateArrange(); return true;
        }
        // Release the old business snapshot before application collection accessors or binding callbacks run.
        if (change.Property == ItemsSourceProperty || change.Property == IdPathProperty || change.Property == CategoryPathProperty || change.Property == ValuePathProperty || change.Property == DataContextProperty)
            InvalidateProfessionalPlotAreaContent();
        if (change.Property == IsVisibleProperty && !IsVisible) _professionalPlotAreaContent?.Invalidate(release: true, queue: false);
        return false;
    }
    private void InvalidateProfessionalPlotAreaContent()
    {
        _professionalPlotAreaContext = null; _professionalPlotAreaFrame = null; _professionalPlotAreaContextRevision = -1;
        _professionalPlotAreaContent?.Invalidate();
        if (HasProfessionalPlotAreaContentPresentation) { InvalidateMeasure(); InvalidateArrange(); }
    }
    private void MeasureProfessionalPlotAreaContent(Size size)
    {
        if (_professionalPlotAreaContent is null && !HasProfessionalPlotAreaContentPresentation) return;
        RefreshProfessionalPlotAreaContent(); _professionalPlotAreaContent?.Measure(size);
    }
    private void ArrangeProfessionalPlotAreaContent(Size size)
    {
        RefreshProfessionalPlotAreaContent(); _professionalPlotAreaContent?.Measure(size); _professionalPlotAreaContent?.Arrange(size);
    }
    private void ProfessionalPlotAreaContentAttached() { _professionalPlotAreaContent?.Attached(); InvalidateProfessionalPlotAreaContent(); }
    private void ProfessionalPlotAreaContentDetached()
    {
        _professionalPlotAreaContent?.Detached(); _professionalPlotAreaContext = null; _professionalPlotAreaFrame = null;
        _professionalPlotAreaContextRevision = -1; _professionalPlotAreaDeferred = false;
    }
    private void ProfessionalPlotAreaContentRenderFinished()
    {
        if (!_professionalPlotAreaDeferred) return;
        _professionalPlotAreaDeferred = false; _professionalPlotAreaContent?.RequestRefresh();
    }
    // The root's permanent export depth must already be entered, so a detached chart keeps one prepared frame.
    private void PrepareProfessionalPlotAreaContentExport()
    {
        if (IsProfessionalPlotAreaContentBusy || _building || _rendering)
            throw new InvalidOperationException("A chart cannot be exported while plot content is updating or laying out.");
        ArrangeProfessionalPlotAreaContent(Bounds.Size);
    }
    private void FinishProfessionalPlotAreaContentExport()
    { if (!_attached) ProfessionalPlotAreaContentDetached(); }
    private void RenderProfessionalPlotAreaContentExport(DrawingContext context) => _professionalPlotAreaContent?.RenderExport(context);
}
