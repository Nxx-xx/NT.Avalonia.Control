using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace NT.AvaloniaControl.Charts;

public abstract partial class EngineeringChartBase
{
    public static readonly StyledProperty<IDataTemplate?> TooltipContentTemplateProperty =
        AvaloniaProperty.Register<EngineeringChartBase, IDataTemplate?>(nameof(TooltipContentTemplate));
    public static readonly StyledProperty<IBrush?> TooltipBackgroundProperty = AvaloniaProperty.Register<EngineeringChartBase, IBrush?>(nameof(TooltipBackground));
    public static readonly StyledProperty<IBrush?> TooltipForegroundProperty = AvaloniaProperty.Register<EngineeringChartBase, IBrush?>(nameof(TooltipForeground));
    public static readonly StyledProperty<IBrush?> TooltipBorderBrushProperty = AvaloniaProperty.Register<EngineeringChartBase, IBrush?>(nameof(TooltipBorderBrush));

    /// <summary>Optional passive live tooltip. DataContext is ChartDataTooltipContext, including every original aggregate member and BusinessDataContext. Null Build suppresses the tooltip. Build errors propagate after cleanup; replace the template to recover. Hiding or disabling releases content; restoring the owner may recover a still-current reading. Transient tooltips are excluded from synchronous and asynchronous raster export.</summary>
    public IDataTemplate? TooltipContentTemplate { get => GetValue(TooltipContentTemplateProperty); set => SetValue(TooltipContentTemplateProperty, value); }
    /// <summary>Live template tooltip chrome; null follows the current light/dark theme.</summary>
    public IBrush? TooltipBackground { get => GetValue(TooltipBackgroundProperty); set => SetValue(TooltipBackgroundProperty, value); }
    public IBrush? TooltipForeground { get => GetValue(TooltipForegroundProperty); set => SetValue(TooltipForegroundProperty, value); }
    public IBrush? TooltipBorderBrush { get => GetValue(TooltipBorderBrushProperty); set => SetValue(TooltipBorderBrushProperty, value); }

    private ChartDataTooltipPresenter? _dataTooltip;
    private bool _dataTooltipDeferred;
    private int _dataTooltipExportDepth;
    private ChartDataTooltipPresenter DataTooltip => _dataTooltip ??= new(this, () => TooltipContentTemplate, ReadDataTooltip,
        RefreshDataTooltip, () => (TooltipBackground, TooltipForeground, TooltipBorderBrush));
    private bool SuppressTransientTooltip => HasProfessionalTrackball || _dataTooltipExportDepth != 0 || !IsEffectivelyVisible || !IsEffectivelyEnabled || _dataTooltip?.SuppressLegacy == true || _professionalLegacyTooltip is not null;

    private ChartDataTooltipReading? ReadDataTooltip()
    {
        if (!_attached || HasProfessionalTrackball || !IsEffectivelyVisible || !IsEffectivelyEnabled || _dirty || _rendering || _building || !IsTooltipEnabled || _active is not { } active) return null;
        var revision = _revision; var attachment = _attachmentRevision; var businessContext = DataContext;
        var context = new ChartDataTooltipContext(active.Members,
            active.Mark.TooltipText ?? $"{active.Members.Count} observations: {F(active.Mark.Value)}", active.Position)
        { EngineeringHit = active, BusinessDataContext = businessContext };
        return new(context, active, revision, () => revision == _revision && attachment == _attachmentRevision && _attached && !_dirty &&
            IsEffectivelyVisible && IsEffectivelyEnabled && IsTooltipEnabled && ReferenceEquals(_active, active) && ReferenceEquals(DataContext, businessContext));
    }

    private void RefreshDataTooltip()
    {
        if (!_attached) { _dataTooltip?.Detached(); return; }
        if (HasProfessionalTrackball || !IsEffectivelyVisible || !IsEffectivelyEnabled) { _dataTooltip?.InvalidateReading(queue: false); return; }
        if (_rendering || _building) { _dataTooltipDeferred = true; return; }
        if (TooltipContentTemplate is null) { _dataTooltip?.InvalidateReading(queue: false); return; }
        _ = GetMarks(); DataTooltip.Synchronize();
    }

    private bool DataTooltipPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ActivePointProperty) { _dataTooltip?.Synchronize(); return false; }
        if (change.Property == TooltipContentTemplateProperty) { DataTooltip.TemplateChanged(); InvalidateVisual(); return true; }
        if (change.Property == TooltipBackgroundProperty || change.Property == TooltipForegroundProperty || change.Property == TooltipBorderBrushProperty)
        { _dataTooltip?.UpdateAppearance(); return true; }
        if (change.Property == IsTooltipEnabledProperty) { RefreshDataTooltip(); InvalidateVisual(); return true; }
        if (change.Property == IsEffectivelyEnabledProperty) { RefreshDataTooltip(); InvalidateVisual(); return true; }
        // Release obsolete reading contexts before application source/binding callbacks run.
        if (change.Property == ItemsSourceProperty || change.Property == IdPathProperty || change.Property == LabelPathProperty)
        {
            if (_dataTooltip is { } presentation) { _dirty = true; _revision++; presentation.InvalidateReading(); }
        }
        return false;
    }

    private void DataTooltipRenderFinished()
    {
        if (!_dataTooltipDeferred) return;
        _dataTooltipDeferred = false; _dataTooltip?.RequestRefresh();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    { base.OnApplyTemplate(e); _dataTooltip?.EnsureMembership(); _dataLegend?.EnsureMembership(); _dataLabels?.EnsureMembership(); _professionalPlotAreaContent?.EnsureMembership(); EnsureProfessionalTrackballMembership(); ApplyTemplateDataSelection(); }

    private void RenderWithoutTransientTooltip(DrawingContext context)
    {
        if (DataSelectionBusy || IsProfessionalPlotAreaContentBusy || ProfessionalTrackballBusy || _presentationContextsBuilding || _dataTooltipExportDepth != 0 || _rendering || _building || _dataTooltip?.IsBusy == true || _dataLegend?.IsBusy == true || _dataLabels?.IsBusy == true)
            throw new InvalidOperationException("A chart cannot be exported while data is building, rendering, or its tooltip template is updating or laying out.");
        var revision = _revision; var selectionVersion = _dataSelectionVersion;
        _dataTooltipExportDepth++;
        try { Render(context); RenderProfessionalPlotAreaContentExport(context); RenderPermanentPresentationExport(context); RenderDataSelection(context, includeRangeBrush: false); if (revision != _revision || selectionVersion != _dataSelectionVersion) throw new InvalidOperationException("The chart changed during export rendering."); }
        finally { _dataTooltipExportDepth--; }
    }
}
