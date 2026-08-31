using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.UI.App.Models.Query;
using InternalsViewer.UI.App.Services;
using InternalsViewer.UI.App.Services.Query;

namespace InternalsViewer.UI.App.ViewModels.Query;

/// <summary>
/// The queries run against a database, kept in the settings so they outlive the session
/// </summary>
/// <remarks>
/// The whole history is held in <see cref="AllEntries"/> and <see cref="Entries"/> is the filtered projection the
/// history pane binds to, so searching hides entries rather than dropping them.
/// </remarks>
public sealed partial class QueryHistoryViewModel(SettingsService settingsService, string databaseName) : ObservableObject
{
    private const int MaxEntries = 200;

    private string _searchText = string.Empty;

    public ObservableCollection<QueryHistoryEntry> Entries { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    private SettingsService SettingsService { get; } = settingsService;

    private string DatabaseName { get; } = databaseName;

    private string SettingKey { get; } = $"QueryHistory:{databaseName}";

    private List<QueryHistoryEntry> AllEntries { get; } = [];

    /// <summary>
    /// Reads the saved history, seeding it the first time a database is opened
    /// </summary>
    /// <remarks>
    /// Nothing saved means the database has never had a history, which is not the same as one that has been cleared -
    /// clearing writes an empty list. Seeding only the first case is what lets the queries be thrown away for good.
    /// </remarks>
    public async Task LoadAsync()
    {
        var saved = await SettingsService.ReadSettingAsync<List<QueryHistoryEntry>>(SettingKey);

        if (saved is null)
        {
            Seed();

            return;
        }

        AllEntries.Clear();

        AllEntries.AddRange(saved);

        ApplyFilter();
    }

    /// <summary>
    /// Records a query that has just been executed
    /// </summary>
    /// <remarks>
    /// A query already in the history moves back to the top rather than being listed twice, so re-running the same
    /// statement does not push the rest of the history out.
    /// </remarks>
    public void Add(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return;
        }

        AllEntries.RemoveAll(e => string.Equals(e.Sql, sql, StringComparison.Ordinal));

        AllEntries.Insert(0, new QueryHistoryEntry(sql, DateTimeOffset.Now));

        if (AllEntries.Count > MaxEntries)
        {
            AllEntries.RemoveRange(MaxEntries, AllEntries.Count - MaxEntries);
        }

        ApplyFilter();

        Save();
    }

    /// <summary>
    /// Drops a single query from the history
    /// </summary>
    public void Remove(QueryHistoryEntry entry)
    {
        if (!AllEntries.Remove(entry))
        {
            return;
        }

        Entries.Remove(entry);

        Save();
    }

    [RelayCommand]
    private void ClearAll()
    {
        if (AllEntries.Count == 0)
        {
            return;
        }

        AllEntries.Clear();

        Entries.Clear();

        Save();
    }

    private void Seed()
    {
        var queries = QueryHistorySeed.Read(DatabaseName);

        if (queries.Count == 0)
        {
            return;
        }

        var seeded = DateTimeOffset.Now;

        AllEntries.Clear();

        AllEntries.AddRange(queries.Select(q => new QueryHistoryEntry(q, seeded)));

        ApplyFilter();

        Save();
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();

        var filtered = term.Length == 0
            ? AllEntries
            : [.. AllEntries.Where(e => e.Sql.Contains(term, StringComparison.OrdinalIgnoreCase))];

        Entries.Clear();

        foreach (var entry in filtered)
        {
            Entries.Add(entry);
        }
    }

    private void Save() => _ = SettingsService.SaveSettingAsync(SettingKey, AllEntries);
}
