using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Connection.BackupFile.Mapping;

/// <summary>
/// Builds page mapping runs for a SQL Server backup file
/// </summary>
/// <remarks>
/// RLE - Run Length Encoding - compression based on the fact that the data profile is often a series of consecutive pages, so the index
/// can be stored as information about the run rather than each individual page.
/// 
/// A run is where pages between the start and end (start + page count - 1) are consecutive and therefore the offsets are consecutive.
/// </remarks>
internal sealed class PageMapRunBuilder
{
    private readonly List<PageRun> _runs = [];

    private bool _hasCurrentRun;

    private short _currentFileId;

    private int _currentStartPageId;

    private int _currentPageCount;

    private int _currentStripeIndex;

    private long _currentStartOffset;

    public void AddPage(short fileId, int pageId, int stripeIndex, long offset)
    {
        if (_hasCurrentRun
            && fileId == _currentFileId
            && pageId == _currentStartPageId + _currentPageCount
            && stripeIndex == _currentStripeIndex
            && offset == _currentStartOffset + (long)_currentPageCount * PageData.Size)
        {
            _currentPageCount++;

            return;
        }

        CloseRun();

        _hasCurrentRun = true;

        _currentFileId = fileId;
        _currentStartPageId = pageId;
        _currentPageCount = 1;
        _currentStripeIndex = stripeIndex;
        _currentStartOffset = offset;
    }

    public bool TryAddUnidentifiedPage(int stripeIndex, long offset)
    {
        if (!_hasCurrentRun
            || stripeIndex != _currentStripeIndex
            || offset != _currentStartOffset + (long)_currentPageCount * PageData.Size)
        {
            return false;
        }

        _currentPageCount++;

        return true;
    }

    public void CloseRun()
    {
        if (!_hasCurrentRun)
        {
            return;
        }

        _runs.Add(new PageRun(_currentFileId, _currentStartPageId, _currentPageCount, _currentStripeIndex, _currentStartOffset));

        _hasCurrentRun = false;
    }

    /// <summary>
    /// Builds the locator, applying last-wins to overlapping runs
    /// </summary>
    /// <remarks>
    /// The same page can appear more than once - a full backup ends with a small data block re-dumping the system pages (file header/PFS/
    /// GAM/DCM) updated during the backup, and the later image is the correct one.
    ///
    /// Runs are added in write order, so iterating in reverse and clipping each earlier run to the gaps left by later runs keeps the most
    /// recent image of every page.
    /// </remarks>
    public PageMap Build()
    {
        CloseRun();

        var result = new Dictionary<short, IReadOnlyList<PageRun>>();

        foreach (var fileRuns in _runs.GroupBy(r => r.FileId))
        {
            var accepted = new List<PageRun>();

            foreach (var run in fileRuns.Reverse())
            {
                AddClipped(accepted, run);
            }

            result.Add(fileRuns.Key, accepted);
        }

        return new PageMap(result);
    }

    /// <summary>
    /// Adds the parts of a run not already covered by more recent runs
    /// </summary>
    /// <remarks>
    /// Accepted runs are sorted and disjoint. The candidate is walked against them and only the uncovered ranges are added, with offsets
    /// rebased to each slice start.
    /// </remarks>
    private static void AddClipped(List<PageRun> accepted, PageRun candidate)
    {
        var pieces = new List<PageRun>();

        var nextPageId = candidate.StartPageId;

        foreach (var existing in accepted)
        {
            if (existing.EndPageId < nextPageId)
            {
                continue;
            }

            if (existing.StartPageId > candidate.EndPageId)
            {
                break;
            }

            if (existing.StartPageId > nextPageId)
            {
                pieces.Add(Slice(candidate, nextPageId, existing.StartPageId - 1));
            }

            nextPageId = existing.EndPageId + 1;

            if (nextPageId > candidate.EndPageId)
            {
                break;
            }
        }

        if (nextPageId <= candidate.EndPageId)
        {
            pieces.Add(Slice(candidate, nextPageId, candidate.EndPageId));
        }

        accepted.AddRange(pieces);

        accepted.Sort((a, b) => a.StartPageId.CompareTo(b.StartPageId));
    }

    private static PageRun Slice(PageRun run, int startPageId, int endPageId)
    {
        var offset = run.StartOffset + (long)(startPageId - run.StartPageId) * PageData.Size;

        return new PageRun(run.FileId, startPageId, endPageId - startPageId + 1, run.StripeIndex, offset);
    }
}
