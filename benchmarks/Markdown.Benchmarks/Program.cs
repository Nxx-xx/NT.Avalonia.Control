using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using NT.AvaloniaControl.Markdown;

Console.OutputEncoding = Encoding.UTF8;
if (args.Contains("--model-updates", StringComparer.Ordinal)) { ModelUpdateBenchmarks.Run(args); return; }
var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
string? Argument(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
var output = Argument("--output") ?? "artifacts/benchmarks/markdown/round.json";
if (Argument("--summarize") is { } directory)
{
    var rounds = Directory.GetFiles(directory, "round-*.json").Order(StringComparer.Ordinal)
        .Select(path => JsonSerializer.Deserialize<RoundReport>(File.ReadAllText(path), options)
            ?? throw new InvalidOperationException($"报告为空：{path}")).ToArray();
    if (rounds.Length != 5 || rounds.Any(round => !round.全部通过 || round.场景.Any(scene => scene.样本数 < 100)))
        throw new InvalidOperationException("基线须有五个独立运行报告，且每场景至少100个有效样本。");
    var groups = rounds.SelectMany(round => round.场景).GroupBy(scene => scene.名称).ToArray();
    foreach (var group in groups)
        if (group.Count() != 5 || group.Select(scene => scene.语料SHA256).Distinct(StringComparer.Ordinal).Count() != 1)
            throw new InvalidOperationException("不同轮次的语料或场景不一致。");
    Save(output, new
    {
        创建时间UTC = DateTimeOffset.UtcNow,
        测量边界 = "后台完整Markdig解析和本库归一化快照/复制投影；不包含UI提交、布局、首屏、图片或高亮。",
        独立进程数 = rounds.Length,
        环境 = rounds.Select(round => round.环境).ToArray(),
        全部通过 = true,
        性能预算已验收 = false,
        通过含义 = "固定语料结构/文本一致且采样条件满足，不表示首屏/流式/布局预算通过。",
        场景 = groups.Select(group => new
        {
            名称 = group.Key,
            样本数 = group.Sum(scene => scene.样本数),
            语料SHA256 = group.First().语料SHA256,
            UTF8字节数 = group.First().UTF8字节数,
            顶层块数 = group.First().顶层块数,
            代码块数 = group.First().代码块数,
            五轮P50中位数毫秒 = Percentile(group.Select(scene => scene.P50毫秒), .5),
            五轮P95中位数毫秒 = Percentile(group.Select(scene => scene.P95毫秒), .5),
            五轮累计分配P50中位数字节 = Percentile(group.Select(scene => (double)scene.分配P50字节), .5),
            最大样本毫秒 = group.Max(scene => scene.样本.Max(sample => sample.耗时毫秒)),
            是否通过 = group.All(scene => scene.是否通过)
        }).ToArray(),
        原始报告 = Directory.GetFiles(directory, "round-*.json").Order(StringComparer.Ordinal).ToArray()
    });
    return;
}

#if DEBUG
throw new InvalidOperationException("Markdown基准须使用Release构建。");
#else
var sampleCount = int.TryParse(Argument("--samples"), out var requestedSamples) ? requestedSamples : 100;
var warmupCount = int.TryParse(Argument("--warmup"), out var requestedWarmup) ? requestedWarmup : 10;
if (sampleCount < 100 || warmupCount < 5)
    throw new ArgumentOutOfRangeException(nameof(args), "至少100个采样和5次预热。");
var roundNumber = int.TryParse(Argument("--round"), out var requestedRound) ? requestedRound : 1;
var parser = new MarkdownParser();
var inputs = new[]
{
    new Corpus("100KiB混合文档", Mixed(100 * 1024), 100),
    new Corpus("1MiB混合文档固定100代码块", Mixed(1024 * 1024), 100),
    new Corpus("100KiB单代码块单长行", SingleCode(100 * 1024), 1),
    new Corpus("100KiB单段落", SingleParagraph(100 * 1024), 0),
    new Corpus("10000普通段落块", string.Concat(Enumerable.Range(0, 10_000)
        .Select(index => $"段落 {index:D5} 普通 **强调** 和逻辑选择。\n\n")), 0, 10_000)
};
var environment = new BenchmarkEnvironment(
    ReadSdk(), RuntimeInformation.FrameworkDescription, RuntimeInformation.OSDescription,
    RuntimeInformation.ProcessArchitecture.ToString(), Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),
    Environment.ProcessorCount, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
    typeof(MarkdownParser).Assembly.GetName().Version?.ToString() ?? "unknown",
    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(MarkdownParser).Assembly.Location))),
    "Release", "不使用显示后端/字体/缩放/视口；图片与高亮关闭。");
var scenes = new List<SceneReport>();
foreach (var corpus in inputs)
{
    // Corpus creation, correctness baselines, hashing and warmup are outside all samples.
    var expected = parser.Parse(corpus.Source);
    var codeCount = expected.Descendants().Count(block => block.Kind == MarkdownBlockKind.Code);
    if (codeCount != corpus.ExpectedCodeBlocks ||
        corpus.ExpectedBlocks is { } count && expected.Blocks.Length != count)
        throw new InvalidOperationException($"语料结构错误：{corpus.Name}");
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(corpus.Source)));
    for (var index = 0; index < warmupCount; index++) parser.Parse(corpus.Source);
    var samples = new List<Sample>(sampleCount);
    var collectionsBefore = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
    for (var index = 0; index < sampleCount; index++)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        var snapshot = parser.Parse(corpus.Source);
        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var valid = snapshot.Blocks.Length == expected.Blocks.Length && snapshot.Text == expected.Text &&
            snapshot.Source == corpus.Source && snapshot.Diagnostics.Length == expected.Diagnostics.Length;
        samples.Add(new Sample(index + 1, elapsed, allocated, valid));
        if (!valid) throw new InvalidOperationException($"采样的文档/投影不同：{corpus.Name}, {index}");
    }
    var scene = new SceneReport(corpus.Name, hash, Encoding.UTF8.GetByteCount(corpus.Source), corpus.Source.Length,
        expected.Blocks.Length, expected.Descendants().Count(), codeCount, expected.Text.Length,
        warmupCount, sampleCount, Percentile(samples.Select(sample => sample.耗时毫秒), .5),
        Percentile(samples.Select(sample => sample.耗时毫秒), .95),
        (long)Percentile(samples.Select(sample => (double)sample.累计分配字节数), .5),
        (long)Percentile(samples.Select(sample => (double)sample.累计分配字节数), .95),
        new[] { GC.CollectionCount(0) - collectionsBefore[0], GC.CollectionCount(1) - collectionsBefore[1], GC.CollectionCount(2) - collectionsBefore[2] },
        samples.All(sample => sample.是否通过), samples);
    scenes.Add(scene);
    Console.WriteLine($"{scene.名称}: {scene.样本数}样本，P50={scene.P50毫秒:F3}ms，P95={scene.P95毫秒:F3}ms，分配P50={scene.分配P50字节}字节");
}
using var process = Process.GetCurrentProcess();
Save(output, new RoundReport(DateTimeOffset.UtcNow, roundNumber, process.Id, environment,
    "Markdig完整解析 + 不可变归一化快照 + 逻辑复制投影；无previous身份匹配。",
    false, "通过仅表示采样语料结构/文本一致；所有UI/流式/布局预算保持未验收。",
    process.WorkingSet64, process.PeakWorkingSet64, scenes.All(scene => scene.是否通过), scenes));
#endif

void Save(string path, object report)
{
    var fullPath = Path.GetFullPath(path);
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    File.WriteAllText(fullPath, JsonSerializer.Serialize(report, options), new UTF8Encoding(false));
    Console.WriteLine($"报告：{fullPath}");
}
static double Percentile(IEnumerable<double> values, double percentile)
{
    var ordered = values.Order().ToArray();
    return ordered[Math.Clamp((int)Math.Ceiling(ordered.Length * percentile) - 1, 0, ordered.Length - 1)];
}
#if !DEBUG
static string Mixed(int bytes)
{
    var builder = new StringBuilder("# 固定混合语料\n\n");
    for (var index = 0; index < 100; index++)
        builder.Append($"```csharp title=block-{index:D3}\nvar value = {index}; // 中文\n```\n\n");
    const string unit = "## 标题\n\n普通 **粗体** *斜体* ~~删除~~ `code` 和 [链接](https://example.test/path)。\n\n- 项目甲\n- 项目乙\n\n> 引用文字\n\n| A | B |\n|---|:--:|\n| 中文 | data |\n\n";
    var used = Encoding.UTF8.GetByteCount(builder.ToString());
    var unitBytes = Encoding.UTF8.GetByteCount(unit);
    while (used + unitBytes <= bytes) { builder.Append(unit); used += unitBytes; }
    builder.Append('x', bytes - used);
    return builder.ToString();
}
static string SingleCode(int bytes)
{
    const string prefix = "```text\n";
    const string suffix = "\n```\n";
    return prefix + new string('x', bytes - prefix.Length - suffix.Length) + suffix;
}
static string SingleParagraph(int bytes)
{
    const string unit = "普通段落 text 😀 ";
    var builder = new StringBuilder();
    var used = 0;
    var unitBytes = Encoding.UTF8.GetByteCount(unit);
    while (used + unitBytes <= bytes) { builder.Append(unit); used += unitBytes; }
    builder.Append('x', bytes - used);
    return builder.ToString();
}
static string? ReadSdk()
{
    try
    {
        using var sdk = Process.Start(new ProcessStartInfo("dotnet", "--version")
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true });
        if (sdk is null) return null;
        var version = sdk.StandardOutput.ReadToEnd().Trim();
        sdk.WaitForExit();
        return sdk.ExitCode == 0 ? version : null;
    }
    catch { return null; }
}
#endif
internal sealed record Corpus(string Name, string Source, int ExpectedCodeBlocks, int? ExpectedBlocks = null);
internal sealed record BenchmarkEnvironment(string? SDK版本, string 运行时, string 操作系统, string 进程架构,
    string? CPU标识, int 逻辑处理器数, long GC可用内存字节数, string 功能程序集版本, string? 功能程序集SHA256, string 配置, string 呈现配置);
internal sealed record Sample(int 序号, double 耗时毫秒, long 累计分配字节数, bool 是否通过);
internal sealed record SceneReport(string 名称, string 语料SHA256, int UTF8字节数, int UTF16长度, int 顶层块数,
    int 递归块数, int 代码块数, int 复制投影长度, int 预热次数, int 样本数, double P50毫秒, double P95毫秒,
    long 分配P50字节, long 分配P95字节, int[] GC次数Gen0Gen1Gen2, bool 是否通过, List<Sample> 样本);
internal sealed record RoundReport(DateTimeOffset 创建时间UTC, int 轮次, int 进程ID, BenchmarkEnvironment 环境,
    string 测量边界, bool 性能预算已验收, string 通过含义, long 最终工作集字节数, long 峰值工作集字节数,
    bool 全部通过, List<SceneReport> 场景);
