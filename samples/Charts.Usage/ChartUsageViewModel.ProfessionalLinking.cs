using System.Windows.Input;
using Avalonia;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private IReadOnlyList<object>? _professionalLinkAnalysisKeys, _professionalLinkEngineeringKeys;
    private Rect? _professionalLinkRange;
    private ChartRange _professionalLinkSelectorRange = new(0, 1);
    private EngineeringObserverCollection<ProfessionalTooltipRecord> _professionalLinkRows = NewLinkRows();
    public IReadOnlyList<object>? ProfessionalLinkAnalysisKeys { get => _professionalLinkAnalysisKeys; set { if (ReferenceEquals(_professionalLinkAnalysisKeys, value)) return; _professionalLinkAnalysisKeys = value; Notify(); } }
    public IReadOnlyList<object>? ProfessionalLinkEngineeringKeys { get => _professionalLinkEngineeringKeys; set { if (ReferenceEquals(_professionalLinkEngineeringKeys, value)) return; _professionalLinkEngineeringKeys = value; Notify(); } }
    public Rect? ProfessionalLinkRange { get => _professionalLinkRange; set { if (_professionalLinkRange == value) return; _professionalLinkRange = value; Notify(); } }
    public ChartRange ProfessionalLinkSelectorRange { get => _professionalLinkSelectorRange; set { if (_professionalLinkSelectorRange == value) return; _professionalLinkSelectorRange = value; Notify(); } }
    public EngineeringObserverCollection<ProfessionalTooltipRecord> ProfessionalLinkRows { get => _professionalLinkRows; set { if (ReferenceEquals(_professionalLinkRows, value)) return; _professionalLinkRows = value; Notify(); } }
    public ICommand ProfessionalLinkSelectCommand => new ActionCommand(() => ProfessionalLinkAnalysisKeys = ["linked-a"]);
    public ICommand ProfessionalLinkClearCommand => new ActionCommand(() => { ProfessionalLinkRange = null; ProfessionalLinkAnalysisKeys = []; ProfessionalLinkEngineeringKeys = []; });
    public ICommand ProfessionalLinkReplaceCommand => new ActionCommand(() => ProfessionalLinkRows = NewLinkRows());
    private static EngineeringObserverCollection<ProfessionalTooltipRecord> NewLinkRows() =>
    [new("linked-a", "北区 A", "北区", "线上", 20), new("linked-b", "南区", "南区", "线上", 40), new("linked-c", "北区 B", "北区", "线上", 30), new("linked-d", "门店", "北区", "门店", 10)];
}
