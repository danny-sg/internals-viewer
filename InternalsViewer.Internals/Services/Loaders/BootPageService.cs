using System;
using System.Threading.Tasks;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.Internals.Interfaces.Services.Loaders;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.Services.Loaders;

public class BootPageService(IPageService pageService): IBootPageService
{
    private const int CheckpointLsnOffset = 444;

    public IPageService PageService { get; } = pageService;

    public async Task<BootPage> Load(Database database)
    {
        var page = await PageService.Load<BootPage>(database, BootPage.BootPageAddress);

        page.CheckpointLsn = GetCheckpointLsn(page.PageData);

        return page;
    }

    /// <summary>
    /// Loads the checkpoint LSN directly from the page data.
    /// </summary>
    private static LogSequenceNumber GetCheckpointLsn(byte[] pageData)
    {
        var checkpointLsnValue = new byte[LogSequenceNumber.Size];

        Array.Copy(pageData, CheckpointLsnOffset, checkpointLsnValue, 0, LogSequenceNumber.Size);
        
        return LogSequenceNumberParser.Parse(checkpointLsnValue);
    }
}