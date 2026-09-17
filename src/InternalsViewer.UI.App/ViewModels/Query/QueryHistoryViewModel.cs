using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.UI.App.Models.Query;
using InternalsViewer.UI.App.Services;
using InternalsViewer.UI.App.Services.Query;

namespace InternalsViewer.UI.App.ViewModels.Query;

public sealed partial class QueryHistoryViewModel(SettingsService settingsService, string databaseName) : ObservableObject
{
    private const int MaxEntries = 200;

    private const int MaxSettingBytes = 7168;

    public ObservableCollection<QueryHistoryEntry> Entries { get; } = [];

    public string SearchText
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                ApplyFilter();
            }
        }
    } = string.Empty;

    private SettingsService SettingsService { get; } = settingsService;

    private string DatabaseName { get; } = databaseName;

    private string SettingKey { get; } = $"QueryHistory:{databaseName}";

    private List<QueryHistoryEntry> AllEntries { get; } = [];

    /// <summary>
    /// Reads the saved history + seed load for pre-seeded known dataases
    /// </summary>
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

    private void Save()
    {
        if (TrimToSettingLimit())
        {
            ApplyFilter();
        }

        _ = SettingsService.SaveSettingAsync(SettingKey, AllEntries);
    }

    /// <summary>
    /// Trims history to available size
    /// </summary>
    private bool TrimToSettingLimit()
    {
        var trimmed = false;

        while (AllEntries.Count > 0
               && Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(AllEntries)) > MaxSettingBytes)
        {
            AllEntries.RemoveAt(AllEntries.Count - 1);

            trimmed = true;
        }

        return trimmed;
    }
}
