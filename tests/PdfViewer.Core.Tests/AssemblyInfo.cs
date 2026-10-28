using Xunit;

// PdfPerformanceSession is a process-wide ambient flight recorder: any concurrent test that
// loads, renders or disposes a document records spans and marks into the open session. The
// performance-contract tests therefore need the suite to run one test at a time — the suite
// is sub-second, so the cost is negligible.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
