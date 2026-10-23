using System.Text.Encodings.Web;
using System.Text.Json;

namespace MarkdownNativeBenchmarks;

internal sealed record NativeSample(int 序号, double 总耗时毫秒, double Parse毫秒, double UI应用毫秒, double 布局毫秒, long 分配字节数, bool 是否通过, double? 高亮完成至绘制总毫秒 = null, double? 选择至绘制毫秒 = null, double? 滚动至绘制毫秒 = null);
internal sealed class NativeGroup
{
    public string 名称 { get; init; } = "";
    public string 语料SHA256 { get; init; } = "";
    public int UTF8字节数 { get; init; }
    public int 预热次数 { get; init; }
    public double? 总耗时P95预算毫秒 { get; init; }
    public double? UI批P95预算毫秒 { get; init; }
    public double? 布局P95预算毫秒 { get; init; }
    public double? 选择P95预算毫秒 { get; init; }
    public double? 滚动P95预算毫秒 { get; init; }
    public long? 单文档累计分配上限字节数 { get; init; }
    public List<NativeSample> 样本 { get; } = [];
    public bool 延迟预算适用 => 总耗时P95预算毫秒 is not null || UI批P95预算毫秒 is not null;
    public int 样本数 => 样本.Count;
    public double 总耗时P50毫秒 => Percentile(样本.Select(sample => sample.总耗时毫秒), .5);
    public double 总耗时P95毫秒 => Percentile(样本.Select(sample => sample.总耗时毫秒), .95);
    public double UI批P95毫秒 => Percentile(样本.Select(sample => sample.UI应用毫秒 + sample.布局毫秒), .95);
    public double 布局P95毫秒 => Percentile(样本.Select(sample => sample.布局毫秒), .95);
    public double? 选择P95毫秒 => 样本.All(sample => sample.选择至绘制毫秒 is not null) ? Percentile(样本.Select(sample => sample.选择至绘制毫秒!.Value), .95) : null;
    public double? 滚动P95毫秒 => 样本.All(sample => sample.滚动至绘制毫秒 is not null) ? Percentile(样本.Select(sample => sample.滚动至绘制毫秒!.Value), .95) : null;
    public long 单文档累计分配最大字节数 => 样本.Count == 0 ? 0 : 样本.Max(sample => sample.分配字节数);
    public bool 是否通过 => 样本.Count > 0 && 样本.All(sample => sample.是否通过) &&
        (总耗时P95预算毫秒 is null || 总耗时P95毫秒 <= 总耗时P95预算毫秒) && (UI批P95预算毫秒 is null || UI批P95毫秒 <= UI批P95预算毫秒) &&
        (布局P95预算毫秒 is null || 布局P95毫秒 <= 布局P95预算毫秒) &&
        (选择P95预算毫秒 is null || 选择P95毫秒 <= 选择P95预算毫秒) &&
        (滚动P95预算毫秒 is null || 滚动P95毫秒 <= 滚动P95预算毫秒) &&
        (单文档累计分配上限字节数 is null || 单文档累计分配最大字节数 <= 单文档累计分配上限字节数);
    public static double Percentile(IEnumerable<double> values, double percentile)
    {
        var ordered = values.Order().ToArray();
        return ordered.Length == 0 ? 0 : ordered[Math.Clamp((int)Math.Ceiling(ordered.Length * percentile) - 1, 0, ordered.Length - 1)];
    }
}
internal sealed class NativeReport
{
    public DateTimeOffset 创建时间UTC { get; } = DateTimeOffset.UtcNow;
    public string 模式 { get; init; } = "markdown-native-benchmark";
    public bool 正式采样 { get; init; }
    public int 轮次 { get; init; }
    public int 进程ID { get; } = Environment.ProcessId;
    public Dictionary<string, object?> 环境 { get; } = [];
    public Dictionary<string, object?> 内存与GC { get; } = [];
    public Dictionary<string, bool> 检查 { get; } = [];
    public List<NativeGroup> 场景 { get; } = [];
    public List<object> 流式批次 { get; } = [];
    public List<object> 流式完成 { get; } = [];
    public List<NativeStreamingReceipt> 流式接收 { get; } = [];
    public List<object> 长会话流式批次 { get; } = [];
    public List<object> 长会话流式完成 { get; } = [];
    public List<NativeStreamingReceipt> 长会话流式接收 { get; } = [];
    public List<NativeStreamGrowthSummary> 长会话退化 { get; } = [];
    public List<NativeStreamingCommit> 长会话提交 { get; } = [];
    public double 长会话提交UI批P95毫秒 => NativeGroup.Percentile(长会话提交.Select(commit => commit.UI应用毫秒 + commit.布局毫秒), .95);
    public List<object> 虚拟化 { get; } = [];
    public Dictionary<string, object> 资源 { get; } = [];
    public string? 错误 { get; set; }
    public bool 全部通过 => 错误 is null && 检查.Count > 0 && 检查.Values.All(value => value) && 场景.All(group => group.是否通过);
    public string 计时边界 { get; } = "输入调用前至原生CompositionBatch.Rendered；控件诊断分别记录后台parse、UI应用和布局；不认证scan-out。";
    public string 分配边界 { get; } = "GC.GetTotalAllocatedBytes进程累计分配差值，含并发解析/渲染/资源工作；不是峰值或存活内存，流式批次从最早覆盖chunk计起。";
    public string 长会话退化边界 { get; } = "按已绘制UTF8占最终文档大小的首25%和末25%分组；分配按当批已绘制源字节归一；托管堆为GC.GetTotalMemory(false)的非强制GC估算；尚未冻结退化比例预算。";
    public void Save(string path)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Console.WriteLine("报告：" + full);
    }
}
