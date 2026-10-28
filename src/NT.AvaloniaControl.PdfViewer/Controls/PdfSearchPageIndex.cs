namespace NT.AvaloniaControl.PdfViewer;

/// <summary>
/// Groups search results per page while keeping each result's global index, so drawing a
/// visible page walks only that page's hits instead of scanning the whole result list.
/// </summary>
internal static class PdfSearchPageIndex
{
    /// <summary>Builds the page buckets; entries keep the global order of <paramref name="results"/>.</summary>
    internal static Dictionary<int, (int Index, PdfSearchResult Result)[]> Build(IReadOnlyList<PdfSearchResult> results)
    {
        var byPage = new Dictionary<int, List<(int Index, PdfSearchResult Result)>>();
        for (var index = 0; index < results.Count; index++)
        {
            var result = results[index];
            if (!byPage.TryGetValue(result.PageIndex, out var list)) byPage[result.PageIndex] = list = [];
            list.Add((index, result));
        }
        var built = new Dictionary<int, (int Index, PdfSearchResult Result)[]>(byPage.Count);
        foreach (var (page, list) in byPage) built[page] = list.ToArray();
        return built;
    }
}
