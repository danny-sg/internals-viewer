using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.Internals.Columnstore.Services;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.ViewModels.Tabs;
using Microsoft.Extensions.Logging;

namespace InternalsViewer.UI.App.ViewModels.Columnstore;

public sealed class ColumnstoreTabViewModelFactory(ILogger<ColumnstoreTabViewModel> logger,
                                                   ColumnstoreService columnstoreService)
{
    public ColumnstoreTabViewModel Create(DatabaseSource database, long allocationUnitId)
        => new(logger, columnstoreService, database, allocationUnitId);
}

public sealed partial class ColumnstoreTabViewModel : TabViewModel
{
    public ColumnstoreTabViewModel(ILogger<ColumnstoreTabViewModel> logger,
                                   ColumnstoreService columnstoreService,
                                   DatabaseSource database,
                                   long allocationUnitId)
    {
        Logger = logger;
        ColumnstoreService = columnstoreService;
        Database = database;
        AllocationUnitId = allocationUnitId;

        Dock = BuildDock();
    }

    private ILogger<ColumnstoreTabViewModel> Logger { get; }

    private ColumnstoreService ColumnstoreService { get; }

    public DatabaseSource Database { get; }

    public long AllocationUnitId { get; }

    [ObservableProperty]
    private ColumnStoreIndex? _index;

    [ObservableProperty]
    private bool _isInitialized;

    [ObservableProperty]
    private string _loadingText = "Loading columnstore index...";

    [ObservableProperty]
    private string _indexDescription = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RowGroupCountDescription))]
    private int _rowGroupCount;

    public string RowGroupCountDescription => RowGroupCount == 1 ? "1 row group" : $"{RowGroupCount:N0} row groups";

    public ObservableCollection<RowGroupSummary> RowGroups { get; } = [];

    /// <summary>
    /// Every segment across every row group, which is the grain the row groups table shows
    /// </summary>
    public ObservableCollection<SegmentSummary> Segments { get; } = [];

    public async Task Load()
    {
        IsLoading = true;
        IsInitialized = false;

        try
        {
            var allocationUnit = Database.AllocationUnits.GetValueOrDefault(AllocationUnitId)
                                 ?? Database.AllocationUnits
                                            .Values
                                            .FirstOrDefault(a => a.AllocationUnitId == AllocationUnitId);

            if (allocationUnit is null)
            {
                LoadingText = $"Allocation unit {AllocationUnitId} was not found";

                return;
            }

            Name = string.IsNullOrEmpty(allocationUnit.IndexName)
                ? allocationUnit.TableName
                : allocationUnit.IndexName;

            var index = await ColumnstoreService.GetIndex(allocationUnit, Database, CancellationToken);

            Index = index;

            RowGroups.Clear();
            Segments.Clear();

            foreach (var summary in RowGroupSummary.Build(index))
            {
                RowGroups.Add(summary);

                foreach (var segment in summary.Segments)
                {
                    Segments.Add(segment);
                }
            }

            IndexDescription = string.IsNullOrEmpty(index.IndexName)
                ? $"{index.SchemaName}.{index.TableName}"
                : $"{index.SchemaName}.{index.TableName}.{index.IndexName}";

            RowGroupCount = RowGroups.Count;

            IsInitialized = true;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to load columnstore index {AllocationUnitId}", AllocationUnitId);

            LoadingText = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Refresh() => await Load();
}
