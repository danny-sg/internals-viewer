using InternalsViewer.Connection.BackupFile.Reader;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Services.Pages.Parsers;

namespace InternalsViewer.Connection.BackupFile.Tests;

public class BackupPageReaderTests
{
    private const string FullBackupPath = @"C:\Temp\TestBackups\TestDatabase_Full.bak";

    private const string DiffBackupPath = @"C:\Temp\TestBackups\TestDatabase_Diff.bak";

    [RequiresFileFact(FullBackupPath)]
    public async Task Can_Read_Pages_From_Full_Backup()
    {
        await using var reader = new BackupPageReader(FullBackupPath);

        await reader.Initialize(CancellationToken.None);

        var fileHeaderPage = await reader.Read("TestDatabase", new PageAddress(1, 0), CancellationToken.None);

        var fileHeader = PageHeaderParser.Parse(fileHeaderPage);

        Assert.Equal(PageType.FileHeader, fileHeader.PageType);
        Assert.Equal(new PageAddress(1, 0), fileHeader.PageAddress);

        var bootPage = await reader.Read("TestDatabase", new PageAddress(1, 9), CancellationToken.None);

        var bootHeader = PageHeaderParser.Parse(bootPage);

        Assert.Equal(PageType.Boot, bootHeader.PageType);
        Assert.Equal(new PageAddress(1, 9), bootHeader.PageAddress);
    }

    [RequiresFileFact(FullBackupPath)]
    public async Task Every_Page_In_Full_Backup_Reads_Back_With_Its_Own_Address()
    {
        await using var reader = new BackupPageReader(FullBackupPath);

        await reader.Initialize(CancellationToken.None);

        var checkedPages = 0;

        foreach (var pageId in Enumerable.Range(0, 2240))
        {
            var pageAddress = new PageAddress(1, pageId);

            var page = await reader.Read("TestDatabase", pageAddress, CancellationToken.None);

            if (page.All(b => b == 0))
            {
                continue;
            }

            var header = PageHeaderParser.Parse(page);

            Assert.Equal(pageAddress, header.PageAddress);

            checkedPages++;
        }

        Assert.True(checkedPages > 1000, $"Only {checkedPages} non-empty pages checked");
    }

    [RequiresFileFact(FullBackupPath)]
    public async Task System_Pages_Come_From_The_Final_Data_Block()
    {
        await using var reader = new BackupPageReader(FullBackupPath);

        await reader.Initialize(CancellationToken.None);

        var pageZeroRead = await reader.Read("TestDatabase", new PageAddress(1, 0), CancellationToken.None);

        Assert.Equal(PageType.FileHeader, PageHeaderParser.Parse(pageZeroRead).PageType);
    }

    [RequiresFileFact(FullBackupPath)]
    public async Task Page_Outside_The_Backup_Throws_PageNotInBackupException()
    {
        await using var reader = new BackupPageReader(FullBackupPath);

        await reader.Initialize(CancellationToken.None);

        await Assert.ThrowsAsync<PageNotInBackupException>(
            () => reader.Read("TestDatabase", new PageAddress(1, 1000000), CancellationToken.None));

        await Assert.ThrowsAsync<PageNotInBackupException>(
            () => reader.Read("TestDatabase", new PageAddress(9, 0), CancellationToken.None));
    }

    [RequiresFileFact(FullBackupPath)]
    public async Task Read_Before_Initialize_Throws_InvalidOperationException()
    {
        await using var reader = new BackupPageReader(FullBackupPath);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.Read("TestDatabase", new PageAddress(1, 0), CancellationToken.None));
    }

    [RequiresFileFact(DiffBackupPath)]
    public async Task Differential_Backup_Has_Gaps_Between_Changed_Extents()
    {
        await using var reader = new BackupPageReader(DiffBackupPath);

        await reader.Initialize(CancellationToken.None);

        var fileHeaderPage = await reader.Read("TestDatabase", new PageAddress(1, 0), CancellationToken.None);

        Assert.Equal(PageType.FileHeader, PageHeaderParser.Parse(fileHeaderPage).PageType);

        var changedPage = await reader.Read("TestDatabase", new PageAddress(1, 64), CancellationToken.None);

        Assert.Equal(new PageAddress(1, 64), PageHeaderParser.Parse(changedPage).PageAddress);

        await Assert.ThrowsAsync<PageNotInBackupException>(
            () => reader.Read("TestDatabase", new PageAddress(1, 40), CancellationToken.None));
    }
}
