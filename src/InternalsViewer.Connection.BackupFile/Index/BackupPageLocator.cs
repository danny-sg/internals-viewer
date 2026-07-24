using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Connection.BackupFile.Index;

/// <summary>
/// Locates a page address offset in a backup file using a page index map
/// </summary>
internal sealed class BackupPageLocator(IReadOnlyDictionary<short, IReadOnlyList<PageRun>> runs)
{
    public IReadOnlyDictionary<short, IReadOnlyList<PageRun>> Runs { get; } = runs;

    public bool HasFile(short fileId) => Runs.ContainsKey(fileId);

    /// <summary>
    /// Tries to get the offset of a page address in the backup file using the page index map
    /// </summary>
    /// <remarks>
    /// Checks file id then uses a binary search to search the page runs
    /// </remarks>
    public bool TryGetOffset(PageAddress pageAddress, out long offset)
    {
        offset = 0;

        if (!Runs.TryGetValue(pageAddress.FileId, out var fileRuns))
        {
            return false;
        }

        var low = 0;

        var high = fileRuns.Count - 1;

        while (low <= high)
        {
            var middle = low + (high - low) / 2;

            var run = fileRuns[middle];

            if (pageAddress.PageId < run.StartPageId)
            {
                high = middle - 1;
            }
            else if (pageAddress.PageId > run.EndPageId)
            {
                low = middle + 1;
            }
            else
            {
                offset = run.StartOffset + (long)(pageAddress.PageId - run.StartPageId) * PageData.Size;

                return true;
            }
        }

        return false;
    }
}
