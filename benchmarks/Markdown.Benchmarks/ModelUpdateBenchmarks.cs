using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using NT.AvaloniaControl.Markdown;

internal static class ModelUpdateBenchmarks
{
    public static void Run(string[] args)
    {
        var debugBuild = false;
#if DEBUG
        debugBuild = true;
#endif
        if (debugBuild || Debugger.IsAttached) throw new InvalidOperationException("Model update benchmarks require Release without a debugger.");
        var outputIndex = Array.IndexOf(args, "--output");
        var roundIndex = Array.IndexOf(args, "--round");
        var roundNumber = roundIndex >= 0 && roundIndex + 1 < args.Length && int.TryParse(args[roundIndex + 1], out var parsedRound) ? parsedRound : 1;
        var path = outputIndex >= 0 ? args[outputIndex + 1] : "artifacts/benchmarks/markdown/model-updates.json";
        var parser = new MarkdownParser();
        var source = string.Concat(Enumerable.Range(0, 10_000).Select(index => $"段落 {index:D5} **强调**\n\n"));
        var previous = parser.Parse(source);
        var appended = parser.Parse(source + "尾部新增");
        var deleted = parser.Parse("段落 00000 **强调**\n\n" + string.Concat(Enumerable.Range(10_000, 10_000).Select(index => $"全新 {index:D5}\n\n")));
        var selection = new MarkdownSelection(previous.Text.Length - 20, previous.Text.Length);
        var groups = new List<object>();
        var matchedDeletion = MarkdownDocumentMatcher.Reconcile(previous, deleted);
        Measure("Parse含previous身份匹配", () => parser.Parse(source + "尾部新增", previous));
        Measure("预解析文档Reconcile", () => MarkdownDocumentMatcher.Reconcile(previous, appended));
        Measure("删除锚点MigrateSelection", () => MarkdownDocumentMatcher.MigrateSelection(previous, matchedDeletion, selection));
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, JsonSerializer.Serialize(new
        {
            创建时间UTC = DateTimeOffset.UtcNow, 模式 = "model-updates", 配置 = "Release",
            进程ID = Environment.ProcessId, 轮次 = roundNumber,
            操作系统 = System.Runtime.InteropServices.RuntimeInformation.OSDescription, 运行时 = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            功能程序集SHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(MarkdownParser).Assembly.Location))),
            语料SHA256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))),
            样本数 = 100, 全部通过 = true, 性能预算已验收 = false,
            说明 = "仅模型更新成本，未套用UI布局预算；没有运行官方参考。", 场景 = groups
        }, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        void Measure<T>(string name, Func<T> action)
        {
            for (var index = 0; index < 10; index++) action();
            var samples = new List<ModelSample>();
            for (var index = 0; index < 100; index++)
            {
                var allocation = GC.GetAllocatedBytesForCurrentThread();
                var started = Stopwatch.GetTimestamp();
                var value = action();
                var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                var allocated = GC.GetAllocatedBytesForCurrentThread() - allocation;
                var valid = value switch
                {
                    MarkdownDocumentSnapshot snapshot => snapshot.Text.Contains("尾部新增", StringComparison.Ordinal) && snapshot.Blocks.Length == previous.Blocks.Length + 1 && snapshot.Blocks[0].Id == previous.Blocks[0].Id && snapshot.Blocks[previous.Blocks.Length - 1].Id == previous.Blocks[^1].Id,
                    MarkdownSelection migrated => migrated.Anchor == matchedDeletion.Blocks[0].TextStart + matchedDeletion.Blocks[0].TextLength && migrated.Active == migrated.Anchor,
                    _ => false
                };
                if (!valid) throw new InvalidOperationException(name + "语义断言失败。");
                samples.Add(new(index, elapsed, allocated, valid));
            }
            var ordered = samples.Select(sample => sample.耗时毫秒).Order().ToArray();
            groups.Add(new { 名称 = name, 预热次数 = 10, 样本数 = 100, 耗时P50毫秒 = ordered[49], 耗时P95毫秒 = ordered[94], 样本 = samples, 是否通过 = true });
            Console.WriteLine(name + ": 100有效样本");
        }
    }
    private sealed record ModelSample(int 序号, double 耗时毫秒, long 累计分配字节数, bool 是否通过);
}
