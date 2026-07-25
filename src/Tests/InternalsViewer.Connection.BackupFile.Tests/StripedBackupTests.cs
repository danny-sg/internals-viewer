using InternalsViewer.Connection.BackupFile.Media;
using InternalsViewer.Connection.BackupFile.Reader;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Services.Pages.Parsers;

namespace InternalsViewer.Connection.BackupFile.Tests;

public class StripedBackupTests
{
    private const string StripePathFormat = @"C:\Temp\TestBackups\TestDatabase_Full_MultiFile_{0}.bak";

    private const string FirstStripePath = @"C:\Temp\TestBackups\TestDatabase_Full_MultiFile_1.bak";

    private const string SingleFileBackupPath = @"C:\Temp\TestBackups\TestDatabase_Full.bak";

    private static string[] AllStripes => [..Enumerable.Range(1, 4).Select(n => string.Format(StripePathFormat, n))];

    [RequiresFileFact(FirstStripePath)]
    public async Task Can_Read_Pages_From_Striped_Backup()
    {
        await using var reader = new BackupPageReader(AllStripes);

        await reader.Initialize(CancellationToken.None);

        var fileHeaderPage = await reader.Read("TestDatabase", new PageAddress(1, 0), CancellationToken.None);

        Assert.Equal(PageType.FileHeader, PageHeaderParser.Parse(fileHeaderPage).PageType);

        var bootPage = await reader.Read("TestDatabase", new PageAddress(1, 9), CancellationToken.None);

        Assert.Equal(PageType.Boot, PageHeaderParser.Parse(bootPage).PageType);
    }

    [RequiresFileFact(FirstStripePath)]
    public async Task Every_Page_In_Striped_Backup_Reads_Back_With_Its_Own_Address()
    {
        await using var reader = new BackupPageReader(AllStripes);

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

            Assert.Equal(pageAddress, PageHeaderParser.Parse(page).PageAddress);

            checkedPages++;
        }

        Assert.True(checkedPages > 1000, $"Only {checkedPages} non-empty pages checked");
    }

    [RequiresFileFact(FirstStripePath)]
    public async Task Stripe_Order_Does_Not_Matter()
    {
        string[] shuffled = [AllStripes[2], AllStripes[0], AllStripes[3], AllStripes[1]];

        await using var reader = new BackupPageReader(shuffled);

        await reader.Initialize(CancellationToken.None);

        var bootPage = await reader.Read("TestDatabase", new PageAddress(1, 9), CancellationToken.None);

        Assert.Equal(PageType.Boot, PageHeaderParser.Parse(bootPage).PageType);
    }

    [RequiresFileFact(FirstStripePath)]
    public async Task Missing_Stripe_Throws_BackupMediaSetException()
    {
        await using var reader = new BackupPageReader([AllStripes[0], AllStripes[1], AllStripes[2]]);

        var exception = await Assert.ThrowsAsync<BackupMediaSetException>(
            () => reader.Initialize(CancellationToken.None));

        Assert.Contains("4", exception.Message);
    }

    [RequiresFileFact(FirstStripePath)]
    public async Task Single_Stripe_Of_A_Striped_Set_Throws_BackupMediaSetException()
    {
        await using var reader = new BackupPageReader(FirstStripePath);

        await Assert.ThrowsAsync<BackupMediaSetException>(() => reader.Initialize(CancellationToken.None));
    }

    [RequiresFileFact(FirstStripePath)]
    public async Task Mixed_Media_Sets_Throw_BackupMediaSetException()
    {
        await using var reader = new BackupPageReader([AllStripes[0], SingleFileBackupPath]);

        await Assert.ThrowsAsync<BackupMediaSetException>(() => reader.Initialize(CancellationToken.None));
    }

    [RequiresFileFact(FirstStripePath)]
    public async Task Mirrored_Copy_Of_A_Family_Throws_BackupMediaSetException()
    {
        var mirrorCopyPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bak");

        File.Copy(AllStripes[0], mirrorCopyPath);

        try
        {
            await using var reader = new BackupPageReader([..AllStripes, mirrorCopyPath]);

            var exception = await Assert.ThrowsAsync<BackupMediaSetException>(
                () => reader.Initialize(CancellationToken.None));

            Assert.Contains("mirrored", exception.Message);
            Assert.Contains("1", exception.Message);
        }
        finally
        {
            File.Delete(mirrorCopyPath);
        }
    }
}
