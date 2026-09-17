using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Query;
using InternalsViewer.UI.App.Services;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InternalsViewer.UI.App.Tests.ViewModels.Query;

[Trait("Category", "Unit")]
public sealed class QueryHistoryViewModelTests : IDisposable
{
    private const string SeededDatabase = "InternalsViewerDemo";

    private static readonly string Root =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InternalsViewerTests");

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task Seed_Loads_Into_History_And_Persists()
    {
        var settings = CreateSettings();

        var first = new QueryHistoryViewModel(settings, SeededDatabase);

        await first.LoadAsync();

        Assert.NotEmpty(first.Entries);

        var reloaded = new QueryHistoryViewModel(settings, SeededDatabase);

        await reloaded.LoadAsync();

        Assert.Equal(first.Entries.Count, reloaded.Entries.Count);
        Assert.Equal(first.Entries[0].Sql, reloaded.Entries[0].Sql);
    }

    [Fact]
    public async Task Running_A_Query_Adds_To_The_Top_And_Keeps_The_Seed()
    {
        var settings = CreateSettings();

        var viewModel = new QueryHistoryViewModel(settings, SeededDatabase);

        await viewModel.LoadAsync();

        var topSeed = viewModel.Entries[0].Sql;

        viewModel.Add("SELECT 1");

        Assert.Equal("SELECT 1", viewModel.Entries[0].Sql);
        Assert.Equal(topSeed, viewModel.Entries[1].Sql);

        var reloaded = new QueryHistoryViewModel(settings, SeededDatabase);

        await reloaded.LoadAsync();

        Assert.Equal("SELECT 1", reloaded.Entries[0].Sql);
        Assert.Equal(topSeed, reloaded.Entries[1].Sql);
    }

    [Fact]
    public async Task Clearing_Persists_An_Empty_History_And_Does_Not_Reseed()
    {
        var settings = CreateSettings();

        var first = new QueryHistoryViewModel(settings, SeededDatabase);

        await first.LoadAsync();

        first.ClearAllCommand.Execute(null);

        Assert.Empty(first.Entries);

        var reloaded = new QueryHistoryViewModel(settings, SeededDatabase);

        await reloaded.LoadAsync();

        Assert.Empty(reloaded.Entries);
    }

    [Fact]
    public async Task History_Is_Cropped_To_Stay_Within_The_Settings_Limit()
    {
        var settings = CreateSettings();

        var viewModel = new QueryHistoryViewModel(settings, "SizeCropUnseededDatabase");

        await viewModel.LoadAsync();

        var padding = new string('x', 500);

        for (var index = 0; index < 100; index++)
        {
            viewModel.Add($"SELECT {index} -- {padding}");
        }

        var bytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(viewModel.Entries.ToList()));

        Assert.True(bytes <= 8 * 1024, $"History serialized to {bytes} bytes");

        Assert.True(viewModel.Entries.Count < 100);

        Assert.Equal($"SELECT 99 -- {padding}", viewModel.Entries[0].Sql);

        Assert.DoesNotContain(viewModel.Entries, e => e.Sql == $"SELECT 0 -- {padding}");
    }

    private static SettingsService CreateSettings()
        => new(NullLogger<SettingsService>.Instance,
               Options.Create(new SettingsOptions { ApplicationDataFolder = $"InternalsViewerTests/{Guid.NewGuid():N}" }));
}
