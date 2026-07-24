using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Connection.BackupFile.Index;

/// <summary>
/// Builds page index runs for a SQL Server backup file
/// </summary>
/// <remarks>
/// RLE - Run Length Encoding - compression based on the fact that the data profile is often a series of consecutive pages, so the index
/// can be stored as information about the run rather than each individual page.
/// 
/// A run is where pages between the start and end (start + page count - 1) are consecutive and therefore the offsets are consecutive.
/// </remarks>
internal sealed class BackupPageIndexBuilder
{
    private readonly List<PageRun> _runs = [];

    private bool _hasCurrentRun;

    private short _currentFileId;

    private int _currentStartPageId;

    private int _currentPageCount;

    private long _currentStartOffset;

    public void AddPage(short fileId, int pageId, long offset)
    {
        if (_hasCurrentRun
            && fileId == _currentFileId
            && pageId == _currentStartPageId + _currentPageCount
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
        _currentStartOffset = offset;
    }

    public bool TryAddUnidentifiedPage(long offset)
    {
        if (!_hasCurrentRun || offset != _currentStartOffset + (long)_currentPageCount * PageData.Size)
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

        _runs.Add(new PageRun(_currentFileId, _currentStartPageId, _currentPageCount, _currentStartOffset));

        _hasCurrentRun = false;
    }

    public BackupPageLocator Build()
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

        return new BackupPageLocator(result);
    }

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

        return new PageRun(run.FileId, startPageId, endPageId - startPageId + 1, offset);
    }
}
