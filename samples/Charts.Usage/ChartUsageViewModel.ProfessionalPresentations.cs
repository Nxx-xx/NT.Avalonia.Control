using System.Windows.Input;
using Avalonia.Controls.Templates;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private IDataTemplate? _professionalLegend, _professionalLabel, _defaultProfessionalLegend, _defaultProfessionalLabel;
    private int _professionalLegendLimit = 4, _professionalLabelLimit = 4, _professionalSelection = -1;
    private string _professionalHeading = "共享图例与标签 · 原始业务";
    private EngineeringObserverCollection<ProfessionalTooltipRecord> _professionalRows = NewPresentationRows();
    public IDataTemplate? ProfessionalLegend { get => _professionalLegend; set { if (ReferenceEquals(_professionalLegend, value)) return; _professionalLegend = value; Notify(); } }
    public IDataTemplate? ProfessionalLabel { get => _professionalLabel; set { if (ReferenceEquals(_professionalLabel, value)) return; _professionalLabel = value; Notify(); } }
    public int ProfessionalLegendLimit { get => _professionalLegendLimit; set { if (_professionalLegendLimit == value) return; _professionalLegendLimit = value; Notify(); } }
    public int ProfessionalLabelLimit { get => _professionalLabelLimit; set { if (_professionalLabelLimit == value) return; _professionalLabelLimit = value; Notify(); } }
    public int ProfessionalSelection { get => _professionalSelection; set { if (_professionalSelection == value) return; _professionalSelection = value; Notify(); } }
    public string ProfessionalHeading { get => _professionalHeading; set { if (_professionalHeading == value) return; _professionalHeading = value; Notify(); } }
    public EngineeringObserverCollection<ProfessionalTooltipRecord> ProfessionalRows { get => _professionalRows; set { if (ReferenceEquals(_professionalRows, value)) return; _professionalRows = value; Notify(); } }
    internal void ConfigureProfessionalPresentations(IDataTemplate legend, IDataTemplate label)
    { _defaultProfessionalLegend = legend; _defaultProfessionalLabel = label; ProfessionalLegend = legend; ProfessionalLabel = label; }
    public ICommand ProfessionalToggleLegendCommand => new ActionCommand(() => ProfessionalLegend = ProfessionalLegend is null ? _defaultProfessionalLegend : null);
    public ICommand ProfessionalToggleLabelCommand => new ActionCommand(() => ProfessionalLabel = ProfessionalLabel is null ? _defaultProfessionalLabel : null);
    public ICommand ProfessionalUpdateCommand => new ActionCommand(() => { if (ProfessionalRows.Count > 0) ProfessionalRows[0].Value += 1; ProfessionalHeading = "共享模板 · 业务已更新"; });
    public ICommand ProfessionalReplaceCommand => new ActionCommand(() => ProfessionalRows = NewPresentationRows());
    private static EngineeringObserverCollection<ProfessionalTooltipRecord> NewPresentationRows() =>
    [new("shared-a", "北区 A", "北区", "线上", 20), new("shared-b", "南区", "南区", "线上", 40),
     new("shared-c", "北区 B", "北区", "线上", 30), new("shared-d", "门店", "北区", "门店", 10)];
}
