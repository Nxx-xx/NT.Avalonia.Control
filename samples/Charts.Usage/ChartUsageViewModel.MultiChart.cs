using System.Windows.Input;

namespace Charts.Usage;

public sealed partial class ChartUsageViewModel
{
    private bool _multiChartBackground = true;
    private string _multiChartStatus = "展开后刷新两图；后台仅处理数字布局。";
    private int _multiChartRevision;
    private LevelOfDetailUsageRecord[] _multiChartRecords = MultiChartRows(0);
    public bool MultiChartBackground { get => _multiChartBackground; set { if (_multiChartBackground == value) return; _multiChartBackground = value; Notify(); } }
    public string MultiChartStatus { get => _multiChartStatus; internal set { _multiChartStatus = value; Notify(); } }
    public LevelOfDetailUsageRecord[] MultiChartRecords => _multiChartRecords;
    public int MultiChartRevision => _multiChartRevision;
    public ICommand MultiChartRetargetCommand => new ActionCommand(() => { _multiChartRevision++; _multiChartRecords = MultiChartRows(_multiChartRevision); Notify(nameof(MultiChartRecords)); Notify(nameof(MultiChartRevision)); });
    private static LevelOfDetailUsageRecord[] MultiChartRows(int revision) => Enumerable.Range(0, 256).Select(q => new LevelOfDetailUsageRecord($"usage-multi-v{revision}-{q}", q, 20 + q % 31 + revision * 3)).ToArray();
}
