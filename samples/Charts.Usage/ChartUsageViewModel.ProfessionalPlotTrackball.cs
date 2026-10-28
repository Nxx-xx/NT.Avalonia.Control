using System.Windows.Input;
using Avalonia.Controls.Templates;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private IDataTemplate? _professionalPlotTemplate, _professionalTrackTemplate, _defaultProfessionalPlotTemplate, _defaultProfessionalTrackTemplate;
    private object? _professionalPlotContent = "永久业务绘图区";
    private ChartTrackballMode _professionalTrackMode = ChartTrackballMode.Vertical;
    private ChartTrackballDisplayMode _professionalTrackDisplay = ChartTrackballDisplayMode.GroupAllPoints;
    private int _professionalTrackLimit = 4, _professionalOverlaySelection = -1;
    private string _professionalOverlayHeading = "绘图区与 Trackball · 业务 VM";
    private EngineeringObserverCollection<ProfessionalTooltipRecord> _professionalOverlayRows = NewOverlayRows();
    public IDataTemplate? ProfessionalPlotTemplate { get => _professionalPlotTemplate; set { if (ReferenceEquals(_professionalPlotTemplate, value)) return; _professionalPlotTemplate = value; Notify(); } }
    public IDataTemplate? ProfessionalTrackTemplate { get => _professionalTrackTemplate; set { if (ReferenceEquals(_professionalTrackTemplate, value)) return; _professionalTrackTemplate = value; Notify(); } }
    public object? ProfessionalPlotContent { get => _professionalPlotContent; set { if (Equals(_professionalPlotContent, value)) return; _professionalPlotContent = value; Notify(); } }
    public ChartTrackballMode ProfessionalTrackMode { get => _professionalTrackMode; set { if (_professionalTrackMode == value) return; _professionalTrackMode = value; Notify(); } }
    public ChartTrackballDisplayMode ProfessionalTrackDisplay { get => _professionalTrackDisplay; set { if (_professionalTrackDisplay == value) return; _professionalTrackDisplay = value; Notify(); } }
    public int ProfessionalTrackLimit { get => _professionalTrackLimit; set { if (_professionalTrackLimit == value) return; _professionalTrackLimit = value; Notify(); } }
    public int ProfessionalOverlaySelection { get => _professionalOverlaySelection; set { if (_professionalOverlaySelection == value) return; _professionalOverlaySelection = value; Notify(); } }
    public string ProfessionalOverlayHeading { get => _professionalOverlayHeading; set { if (_professionalOverlayHeading == value) return; _professionalOverlayHeading = value; Notify(); } }
    public EngineeringObserverCollection<ProfessionalTooltipRecord> ProfessionalOverlayRows { get => _professionalOverlayRows; set { if (ReferenceEquals(_professionalOverlayRows, value)) return; _professionalOverlayRows = value; Notify(); } }
    internal void ConfigureProfessionalPlotTrackball(IDataTemplate plot, IDataTemplate track)
    { _defaultProfessionalPlotTemplate = plot; _defaultProfessionalTrackTemplate = track; ProfessionalPlotTemplate = plot; ProfessionalTrackTemplate = track; }
    public ICommand ProfessionalPlotToggleCommand => new ActionCommand(() => { ProfessionalPlotTemplate = ProfessionalPlotTemplate is null ? _defaultProfessionalPlotTemplate : null; ProfessionalPlotContent = ProfessionalPlotTemplate is null ? null : "永久业务绘图区"; });
    public ICommand ProfessionalTrackToggleCommand => new ActionCommand(() => ProfessionalTrackMode = ProfessionalTrackMode == ChartTrackballMode.None ? ChartTrackballMode.Vertical : ChartTrackballMode.None);
    public ICommand ProfessionalTrackDisplayCommand => new ActionCommand(() => ProfessionalTrackDisplay = ProfessionalTrackDisplay == ChartTrackballDisplayMode.GroupAllPoints ? ChartTrackballDisplayMode.FloatAllPoints : ChartTrackballDisplayMode.GroupAllPoints);
    public ICommand ProfessionalOverlayUpdateCommand => new ActionCommand(() => { ProfessionalOverlayRows[0].Value += 1; ProfessionalOverlayHeading = "绘图区业务已更新"; });
    public ICommand ProfessionalOverlayReplaceCommand => new ActionCommand(() => ProfessionalOverlayRows = NewOverlayRows());
    private static EngineeringObserverCollection<ProfessionalTooltipRecord> NewOverlayRows() =>
    [new("overlay-a", "北区 A", "北区", "线上", 20), new("overlay-b", "南区", "南区", "线上", 40), new("overlay-c", "北区 B", "北区", "线上", 30), new("overlay-d", "门店", "北区", "门店", 10)];
}
