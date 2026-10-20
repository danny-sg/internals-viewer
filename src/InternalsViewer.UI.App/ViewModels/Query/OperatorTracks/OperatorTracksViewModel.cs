using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Services.Query;
using Windows.Storage.Pickers;

namespace InternalsViewer.UI.App.ViewModels.Query.OperatorTracks;

public sealed partial class OperatorTracksViewModel : ObservableObject, IDisposable
{
    private const int NameWidth = 220;

    private const int FunctionWidth = 320;

    private const int ConditionsWidth = 360;

    private const int ColourWidth = 90;

    private const int FactorWidth = 70;

    private const int CountWidth = 90;

    private const int ActionsWidth = 120;

    private const string FileType = "Operator Tracks";

    private const string Extension = ".json";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTracks))]
    private QueryResultSet? _tracksTable;

    [ObservableProperty]
    private ResultRow<long>? _selectedTrackRow;

    public OperatorTracksViewModel(TraceTickTrackStore store, Func<TraceTickDefinition, int?> getTickCount, Action<string> addMessage)
    {
        Store = store;

        GetTickCount = getTickCount;

        AddMessage = addMessage;

        Store.PropertyChanged += OnStorePropertyChanged;

        Refresh();
    }

    public event Action<TraceTickDefinition>? EditRequested;

    public bool HasTracks => Store.Definitions.Count > 0;

    private TraceTickTrackStore Store { get; }

    private Func<TraceTickDefinition, int?> GetTickCount { get; }

    private Action<string> AddMessage { get; }

    public void Refresh() => TracksTable = BuildTracksTable();

    public void Dispose() => Store.PropertyChanged -= OnStorePropertyChanged;

    private static string Describe(IReadOnlyList<TraceCallCondition> conditions)
        => conditions.Count == 0 ? "Every Call" : string.Join(", ", conditions.Select(Describe));

    private static string Describe(TraceCallCondition condition)
    {
        var target = condition.Target switch
        {
            TraceConditionTarget.Return => "Return",
            TraceConditionTarget.CalledBy => "Called By",
            TraceConditionTarget.CalledDirectlyBy => "Called Directly By",
            TraceConditionTarget.Operator => "Operator",
            _ => condition.Argument?.Name ?? "Argument"
        };

        var comparison = condition.Operator switch
        {
            TraceConditionOperator.NotEqual => "!=",
            TraceConditionOperator.LessThan => "<",
            TraceConditionOperator.LessThanOrEqual => "<=",
            TraceConditionOperator.GreaterThan => ">",
            TraceConditionOperator.GreaterThanOrEqual => ">=",
            _ => "="
        };

        return $"{target} {comparison} {condition.Value}";
    }

    [RelayCommand]
    private void Clear() => Store.Clear();

    [RelayCommand]
    private async Task Export()
    {
        var picker = new FileSavePicker();

        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));

        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        picker.SuggestedFileName = FileType;

        picker.FileTypeChoices.Add(FileType, [Extension]);

        if (await picker.PickSaveFileAsync() is not { } file)
        {
            return;
        }

        try
        {
            await using var stream = File.Create(file.Path);

            await TraceTickDefinition.WriteAsync(stream, Store.Definitions, CancellationToken.None);

            AddMessage($"{Store.Definitions.Count:N0} operator track(s) exported to {file.Path}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            AddMessage($"The operator tracks could not be exported: {exception.Message}");
        }
    }

    [RelayCommand]
    private async Task Import()
    {
        var picker = new FileOpenPicker();

        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));

        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        picker.FileTypeFilter.Add(Extension);

        if (await picker.PickSingleFileAsync() is not { } file)
        {
            return;
        }

        try
        {
            await using var stream = File.OpenRead(file.Path);

            var definitions = await TraceTickDefinition.ReadAsync(stream, CancellationToken.None);

            Store.AddRange(definitions);

            AddMessage($"{definitions.Count:N0} operator track(s) imported from {file.Path}");
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            AddMessage($"The operator tracks could not be imported: {exception.Message}");
        }
    }

    private void OnStorePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TraceTickTrackStore.Definitions))
        {
            Refresh();
        }
    }

    private QueryResultSet BuildTracksTable()
    {
        var columns = new List<ResultColumn>
        {
            new(0, "Name", typeof(string), false) { Width = NameWidth },
            new(1, "Function", typeof(string), false) { Width = FunctionWidth },
            new(2, "Conditions", typeof(string), false) { Width = ConditionsWidth },
            new(3, "Colour", typeof(string), false) { Width = ColourWidth },
            new(4, "Height", typeof(double), false) { Width = FactorWidth, Alignment = ResultAlignment.Right },
            new(5, "Width", typeof(double), false) { Width = FactorWidth, Alignment = ResultAlignment.Right },
            new(6, "Ticks", typeof(int), false) { Width = CountWidth, Alignment = ResultAlignment.Right },
            new(7, string.Empty, typeof(string), false) { Width = ActionsWidth }
        };

        var rows = Store.Definitions.Select((d, index) => new ResultRow<long>([d.Name,
                                                                              $"{d.Module}!{d.Function}",
                                                                              Describe(d.Conditions),
                                                                              d.Colour,
                                                                              d.HeightFactor,
                                                                              d.WidthFactor,
                                                                              GetTickCount(d),
                                                                              GetActions(d)])
                                                             {
                                                                 Id = index
                                                             })
                                    .ToArray();

        return new QueryResultSet { Columns = columns, Rows = rows };
    }

    private ActionLinks GetActions(TraceTickDefinition definition)
        => new([new ActionLink("Edit", () => EditRequested?.Invoke(definition)), new ActionLink("Delete", () => Store.Remove(definition))]);
}
