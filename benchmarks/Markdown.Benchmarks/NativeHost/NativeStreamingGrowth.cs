namespace MarkdownNativeBenchmarks;

internal sealed record NativeStreamingReceipt(int 会话序号, int 序号, long SourceVersion,
    int 累计UTF16结束位置, int 累计UTF8结束位置, int ChunkUTF16长度, int ChunkUTF8字节数,
    long 接收Ticks);

internal sealed record NativeStreamingCommit(int 会话序号, int 序号, long SourceVersion,
    int SourceLength, int 源UTF8字节数, double Parse毫秒, double UI应用毫秒,
    double 布局毫秒, int ActiveParseCount, int PendingRequestCount,
    int ExecutingResourceRequestCount, int PendingResourceRequestCount, bool 接收版本对应长度);

internal sealed record NativeStreamingBatch(
    int 会话序号, int 序号, long 最早Chunk接收Ticks, long 绘制完成Ticks,
    int 累计已绘制UTF16, int 累计已绘制UTF8, int 最终源UTF8字节数,
    int 覆盖Chunk数, int 覆盖ChunkUTF8字节数, long SourceVersion, int SourceLength,
    bool 接收版本对应长度, double 延迟毫秒, double Parse毫秒, double UI应用毫秒,
    double 布局毫秒, long 累计分配字节数, long 托管堆估算字节数, bool 是否通过)
{
    public double 最终大小进度 => (double)累计已绘制UTF8 / 最终源UTF8字节数;
    public double 累计分配每已绘制源字节 => (double)累计分配字节数 / 累计已绘制UTF8;
}

internal sealed record NativeStreamQuarter(int 样本数, double? ParseP95毫秒,
    double? UI批P50毫秒, double? UI批P95毫秒, double? 累计分配每已绘制源字节P95,
    double? 托管堆估算P50字节数, double? 托管堆估算P95字节数)
{
    public static NativeStreamQuarter From(IEnumerable<NativeStreamingBatch> values)
    {
        var samples = values.ToArray();
        if (samples.Length == 0) return new(0, null, null, null, null, null, null);
        return new(samples.Length,
            NativeGroup.Percentile(samples.Select(sample => sample.Parse毫秒), .95),
            NativeGroup.Percentile(samples.Select(sample => sample.UI应用毫秒 + sample.布局毫秒), .5),
            NativeGroup.Percentile(samples.Select(sample => sample.UI应用毫秒 + sample.布局毫秒), .95),
            NativeGroup.Percentile(samples.Select(sample => sample.累计分配每已绘制源字节), .95),
            NativeGroup.Percentile(samples.Select(sample => (double)sample.托管堆估算字节数), .5),
            NativeGroup.Percentile(samples.Select(sample => (double)sample.托管堆估算字节数), .95));
    }
}

internal sealed record NativeStreamGrowthSummary(int 会话序号, int 最终源UTF8字节数,
    long 会话累计分配字节数, long 开始托管堆估算字节数, long 最终托管堆估算字节数,
    NativeStreamQuarter 首四分之一, NativeStreamQuarter 第二四分之一,
    NativeStreamQuarter 第三四分之一, NativeStreamQuarter 末四分之一)
{
    public long 托管堆估算变化字节数 => 最终托管堆估算字节数 - 开始托管堆估算字节数;
    public double 会话累计分配每最终源字节 => (double)会话累计分配字节数 / 最终源UTF8字节数;
    public bool 比例可计算 => 首四分之一.样本数 > 0 && 末四分之一.样本数 > 0 &&
        首四分之一.UI批P95毫秒 > 0 && 首四分之一.累计分配每已绘制源字节P95 > 0;
    public double? UI批P95末首比例 => 比例可计算 ? 末四分之一.UI批P95毫秒 / 首四分之一.UI批P95毫秒 : null;
    public double? 归一累计分配P95末首比例 => 比例可计算 ? 末四分之一.累计分配每已绘制源字节P95 / 首四分之一.累计分配每已绘制源字节P95 : null;
    public bool 退化预算已冻结 => false;

    public static NativeStreamGrowthSummary Create(int session, int bytes, long allocated,
        long startHeap, long finalHeap, IReadOnlyList<NativeStreamingBatch> samples) =>
        new(session, bytes, allocated, startHeap, finalHeap,
            NativeStreamQuarter.From(samples.Where(sample => sample.累计已绘制UTF8 <= bytes / 4)),
            NativeStreamQuarter.From(samples.Where(sample => sample.累计已绘制UTF8 > bytes / 4 && sample.累计已绘制UTF8 <= bytes / 2)),
            NativeStreamQuarter.From(samples.Where(sample => sample.累计已绘制UTF8 > bytes / 2 && sample.累计已绘制UTF8 <= bytes * 3L / 4)),
            NativeStreamQuarter.From(samples.Where(sample => sample.累计已绘制UTF8 > bytes * 3L / 4)));
}
