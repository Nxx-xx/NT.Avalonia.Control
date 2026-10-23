using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Avalonia;
using Avalonia.Controls;
using NT.AvaloniaControl.TreeDataGrid;
using IndexPath = NT.AvaloniaControl.TreeDataGrid.IndexPath;

if (args.Contains("--p6", StringComparer.Ordinal)) return P6Profile.Run(args);

const int seed = 1729;
Console.OutputEncoding = Encoding.UTF8;
var outputIndex = Array.IndexOf(args, "--output");
var outputPath = outputIndex >= 0 && outputIndex + 1 < args.Length ? args[outputIndex + 1] : "artifacts/benchmarks/p4-p5.json";
var measurements = new List<Measurement>();
var random = new Random(seed);
var data = Enumerable.Range(0, 100_000).Select(i => new Item(i, random.Next())).ToArray();
// Warm the JIT on the same paths, outside measurements.
using (var warmup = new FlatTreeDataGridSource<Item>(data.Take(100))) warmup.Refresh();
FlatTreeDataGridSource<Item> flat = null!;
Measure("创建10万行平面数据源", 3000, () => flat = new(data));
using (flat!)
{
    Measure("刷新10万行平面数据", 1500, () => flat.Refresh());
    var columns = Enumerable.Range(0, 100).Select(i => (TreeDataGridColumn)new TreeDataGridTextColumn
        { Header = $"C{i}", Width = new GridLength(120) }).ToArray();
    TreeDataGridLayout? geometry = null;
    Measure("10万行、100列、可变行高的布局计算", 500,
        () => geometry = new(flat.Rows, columns, 1200, 28, row => ((Item)row.Model).Number % 5 == 0 ? 48 : 28, 2));
    Measure("1000次滚动位置命中测试", 100, () =>
    {
        for (var i = 0; i < 1000; ++i)
            geometry!.HitTest(new Point(400, 200), new Vector(i % 2000, i * 2500), new Size(1200, 700), out _, out _);
    });
    var first = flat.Rows[0].Id;
    Measure("10万次行标识查找", 500, () =>
    {
        for (var i = 0; i < 100_000; ++i)
        {
            if (!flat.TryGetRow(first, out _) || flat.GetViewIndex(first) != 0)
                throw new InvalidOperationException("行标识映射发生了意外变化。");
        }
    });
    var sortColumn = columns[0].Id;
    Comparison<object> byValue = (left, right) => ((Item)left).Value.CompareTo(((Item)right).Value);
    Measure("10万行平面数据升序排序", 2000, () => flat.Sort(sortColumn, byValue, TreeDataGridSortDirection.Ascending));
    if (!flat.Rows.Select(row => ((Item)row.Model).Value).SequenceEqual(data.OrderBy(item => item.Value).Select(item => item.Value))
        || !flat.TryGetNodeId(new IndexPath(0), out var sourceFirst) || sourceFirst != first)
        throw new InvalidOperationException("排序结果必须有序，并保留源索引和稳定的行标识。");
    Measure("10万行平面数据降序排序", 2000, () => flat.Sort(sortColumn, byValue, TreeDataGridSortDirection.Descending));
    Measure("10万行已排序平面数据过滤一半", 2000, () => flat.SetFilter(model => ((Item)model).Number % 2 == 0));
    if (flat.Rows.Count != 50_000) throw new InvalidOperationException("过滤后的投影不完整。");
    Measure("10万行平面数据批量清除排序和过滤", 2500, () =>
    {
        using (flat.DeferRefresh()) { flat.ClearSort(); flat.SetFilter(null); }
    });
    if (flat.Rows.Count != data.Length || flat.GetViewIndex(first) != 0)
        throw new InvalidOperationException("清除投影设置后必须恢复源数据顺序。");
}
var wide = new Item(-1, 0) { Children = data };
HierarchicalTreeDataGridSource<Item> tree = null!;
Measure("创建100001节点宽树（初始折叠）", 4000, () => tree = new([wide], item => item.Children));
using (tree!)
{
    var rootId = tree.Rows[0].Id;
    Measure("宽树展开10万个子节点", 1500, () => tree.SetExpanded(rootId, true));
    if (tree.Rows.Count != 100_001) throw new InvalidOperationException("展开后的投影不完整。");
    Measure("宽树折叠10万个子节点", 1500, () => tree.SetExpanded(rootId, false));
    var projectionNotifications = 0;
    tree.ProjectionChanged += (_, _) => projectionNotifications++;
    Measure("宽树全部展开（单次投影通知）", 2000, () => tree.ExpandAll());
    if (tree.Rows.Count != 100_001 || projectionNotifications != 1)
        throw new InvalidOperationException("批量展开必须仅发布一次完整投影。");
    projectionNotifications = 0;
    Measure("宽树全部折叠（单次投影通知）", 2000, () => tree.CollapseAll());
    if (tree.Rows.Count != 1 || projectionNotifications != 1)
        throw new InvalidOperationException("批量折叠必须仅发布一次完整投影。");
    var treeColumn = new TreeDataGridTextColumn().Id;
    tree.ExpandAll();
    Measure("宽树10万个同级子节点排序", 2000, () => tree.Sort(treeColumn,
        (left, right) => ((Item)left).Value.CompareTo(((Item)right).Value), TreeDataGridSortDirection.Ascending));
    if (tree.Rows[0].Id != rootId || tree.Rows.Skip(1).Any(row => row.Depth != 1))
        throw new InvalidOperationException("排序必须保留树的层级边界。");
    Measure("宽树10万个子节点过滤一半", 2000, () => tree.SetFilter(model => ReferenceEquals(model, wide) || ((Item)model).Number % 2 == 0));
    if (tree.Rows.Count != 50_001) throw new InvalidOperationException("树过滤后的节点数量不正确，应保留父节点和一半子节点。");
}
// 1,000 chains of 100 nodes: measures depth cost separately from visible count.
var deepRoots = new Item[1000];
for (var chain = 0; chain < deepRoots.Length; ++chain)
{
    var current = new Item(chain * 100, 0);
    deepRoots[chain] = current;
    for (var depth = 1; depth < 100; ++depth)
    {
        var next = new Item(chain * 100 + depth, 0);
        current.Children = [next];
        current = next;
    }
}
HierarchicalTreeDataGridSource<Item> deep = null!;
Measure("创建10万节点、深度100层的树（初始折叠）", 8000, () =>
{
    deep = new HierarchicalTreeDataGridSource<Item>(deepRoots, item => item.Children);
    if (!deep.TryGetNodeId(new IndexPath(Enumerable.Repeat(0, 100).ToArray()), out _))
        throw new InvalidOperationException("未找到深层节点的源路径。");
});
using (deep)
{
    Measure("10万节点深树全部展开", 5000, () => deep.ExpandAll());
    if (deep.Rows.Count != 100_000) throw new InvalidOperationException("深树展开后未显示全部节点。");
}
var passed = measurements.All(x => x.Passed);
var report = new
{
    基准说明 = "TreeDataGrid 树形表格控件性能基准测试：使用10万行平面数据和约10万节点的树形数据，测量数据源创建与刷新、排序与过滤、树节点展开与折叠、布局几何计算、滚动位置命中测试和行标识查找的耗时及累计内存分配。每项操作执行一次并与预设耗时上限比较，全部通过表示所有操作均未超时；这些上限用于发现明显的性能问题，不代表界面流畅度或产品性能承诺。累计分配字节数包含临时对象，不是最终占用或峰值内存；测试不包含完整界面绘制。比较性能时，请在同一台机器上使用 Release（发布）构建、不附加调试器，多次运行后比较结果。",
    测试时间UTC = DateTimeOffset.UtcNow,
    运行时 = RuntimeInformation.FrameworkDescription,
    操作系统 = RuntimeInformation.OSDescription,
    进程架构 = RuntimeInformation.ProcessArchitecture.ToString(),
    逻辑处理器数量 = Environment.ProcessorCount,
    随机种子 = seed,
    布局视口 = new { 宽度 = 1200, 高度 = 700 },
    测试结果 = measurements.Select(x => new
    {
        测试场景 = x.Scenario,
        耗时毫秒 = x.Milliseconds,
        累计分配字节数 = x.AllocatedBytes,
        耗时上限毫秒 = x.BudgetMilliseconds,
        是否通过 = x.Passed
    }),
    全部通过 = passed
};
var fullOutput = Path.GetFullPath(outputPath);
Directory.CreateDirectory(Path.GetDirectoryName(fullOutput)!);
var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.CjkUnifiedIdeographs,
        UnicodeRanges.CjkSymbolsandPunctuation, UnicodeRanges.HalfwidthandFullwidthForms)
});
File.WriteAllText(fullOutput, json);
Console.WriteLine(json);
return passed ? 0 : 1;

void Measure(string name, double budgetMilliseconds, Action action)
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    var allocated = GC.GetTotalAllocatedBytes(true);
    var timer = Stopwatch.StartNew();
    action();
    timer.Stop();
    measurements.Add(new(name, timer.Elapsed.TotalMilliseconds,
        GC.GetTotalAllocatedBytes(true) - allocated, budgetMilliseconds, timer.Elapsed.TotalMilliseconds <= budgetMilliseconds));
}

sealed class Item(int number, int value)
{
    public int Number { get; } = number;
    public int Value { get; } = value;
    public IReadOnlyList<Item> Children { get; set; } = [];
}
sealed record Measurement(string Scenario, double Milliseconds, long AllocatedBytes, double BudgetMilliseconds, bool Passed);
