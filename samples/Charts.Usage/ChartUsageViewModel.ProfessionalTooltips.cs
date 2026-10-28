using System.Windows.Input;
using Avalonia.Controls.Templates;
using NT.AvaloniaControl.Charts;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private IDataTemplate? _c7ProfessionalTemplate, _c7DefaultProfessionalTemplate;
    private bool _c7ProfessionalTooltipsEnabled = true;
    private string _c7TooltipTitle = "业务来源 · 完整原始成员";
    private int _c7TooltipSelection = -1;
    private EngineeringObserverCollection<ProfessionalTooltipRecord> _c7TooltipRows = CreateProfessionalTooltipRows();
    public IDataTemplate? C7ProfessionalTemplate { get => _c7ProfessionalTemplate; set { if (ReferenceEquals(value, _c7ProfessionalTemplate)) return; _c7ProfessionalTemplate = value; Notify(); } }
    public bool C7ProfessionalTooltipsEnabled { get => _c7ProfessionalTooltipsEnabled; set { if (_c7ProfessionalTooltipsEnabled == value) return; _c7ProfessionalTooltipsEnabled = value; Notify(); } }
    public string C7TooltipTitle { get => _c7TooltipTitle; set { if (_c7TooltipTitle == value) return; _c7TooltipTitle = value; Notify(); } }
    public int C7TooltipSelection { get => _c7TooltipSelection; set { if (_c7TooltipSelection == value) return; _c7TooltipSelection = value; Notify(); } }
    public EngineeringObserverCollection<ProfessionalTooltipRecord> C7TooltipRows { get => _c7TooltipRows; set { if (ReferenceEquals(value, _c7TooltipRows)) return; _c7TooltipRows = value; Notify(); } }
    internal void ConfigureProfessionalTooltipTemplate(IDataTemplate template) { _c7DefaultProfessionalTemplate = template; C7ProfessionalTemplate = template; }
    public ICommand C7TooltipToggleTemplateCommand => new ActionCommand(() => C7ProfessionalTemplate = C7ProfessionalTemplate is null ? _c7DefaultProfessionalTemplate : null);
    public ICommand C7TooltipUpdateCommand => new ActionCommand(() => { if (C7TooltipRows.Count == 0) return; C7TooltipRows[0].Value += 1; C7TooltipTitle = $"业务数据已更新 · {C7TooltipRows[0].Value:0}"; });
    public ICommand C7TooltipReplaceCommand => new ActionCommand(() => C7TooltipRows = CreateProfessionalTooltipRows());
    private static EngineeringObserverCollection<ProfessionalTooltipRecord> CreateProfessionalTooltipRows() =>
    [new("tooltip-a", "North / first", "North", "Online", 20), new("tooltip-b", "South", "South", "Online", 40),
     new("tooltip-c", "North / duplicate", "North", "Online", 30), new("tooltip-d", "Store", "North", "Store", 10)];
}

public sealed class ProfessionalTooltipRecord(string id, string label, string column, string row, double value) : AdvancedBusinessRecord(id)
{
    private double _value = value;
    public string Label { get; } = label;
    public string Column { get; } = column;
    public string Row { get; } = row;
    public double Value { get => _value; set => Set(ref _value, value); }
}
