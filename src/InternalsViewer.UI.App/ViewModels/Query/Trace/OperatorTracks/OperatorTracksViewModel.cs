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
using InternalsViewer.Query.FullTrace.Timeline.Ticks.Enums;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Services.Query.Trace;
using Windows.Storage.Pickers;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.OperatorTracks;

public sealed partial class OperatorTracksViewModel : ObservableObject, IDisposable
{
    private const int NameWidth = 160;

    private const int LabelWidth = 160;

    private const int FunctionWidth = 320;

    private const int ConditionsWidth = 360;

    private const int DescriptionWidth = 360;

    private const int ColourWidth = 90;

    private const int FactorWidth = 70;

    private const int ActionsWidth = 120;

    private const string FileType = "Operator Tracks";

    private const string Extension = ".json";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAddedTracks))]
    private QueryResultSet? _tracksTable;

    [ObservableProperty]
    private ResultRow<long>? _selectedTrackRow;

    public OperatorTracksViewModel(TraceTickTrackStore store, Action<string> addMessage)
    {
        Store = store;

        AddMessage = addMessage;

        Store.PropertyChanged += OnStorePropertyChanged;

        Refresh();
    }

    public event Action<TraceTickDefinition>? EditRequested;

    public bool HasAddedTracks => Store.AddedDefinitions.Count > 0;

    private TraceTickTrackStore Store { get; }

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
            TraceConditionTarget.Locator => condition.Argument is { } slot ? $"{slot.Name} Locator" : "Locator",
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
    private void Reset() => Store.Reset();

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

            AddMessage($"{Store.Definitions.Count:N0} operator track rule(s) exported to {file.Path}");
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

            AddMessage($"{definitions.Count:N0} operator track rule(s) imported from {file.Path}");
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
            new(0, "Track", typeof(string), false) { Width = NameWidth },
            new(1, "Label", typeof(string), false) { Width = LabelWidth },
            new(2, "Title", typeof(string), false) { Width = LabelWidth },
            new(3, "Function", typeof(string), false) { Width = FunctionWidth },
            new(4, "Conditions", typeof(string), false) { Width = ConditionsWidth },
            new(5, "Description", typeof(string), false) { Width = DescriptionWidth },
            new(6, "Return Description", typeof(string), false) { Width = DescriptionWidth },
            new(7, "Colour", typeof(string), false) { Width = ColourWidth },
            new(8, "Background", typeof(string), false) { Width = ColourWidth },
            new(9, "Height", typeof(double), false) { Width = FactorWidth, Alignment = ResultAlignment.Right },
            new(10, "Width", typeof(double), false) { Width = FactorWidth, Alignment = ResultAlignment.Right },
            new(11, string.Empty, typeof(string), false) { Width = ActionsWidth }
        };

        var rows = Store.Definitions.Select((d, index) => new ResultRow<long>([d.Name,
                                                                              d.GetLabel(),
                                                                              d.Title ?? string.Empty,
                                                                              GetFunctionText(d),
                                                                              Describe(d.Conditions),
                                                                              d.Description ?? string.Empty,
                                                                              d.ReturnDescription ?? string.Empty,
                                                                              d.Colour,
                                                                              d.Background ?? string.Empty,
                                                                              d.HeightFactor,
                                                                              d.WidthFactor,
                                                                              GetActions(d)])
                                                             {
                                                                 Id = index
                                                             })
                                    .ToArray();

        return new QueryResultSet { Columns = columns, Rows = rows };
    }

    private static string GetFunctionText(TraceTickDefinition definition)
        => definition.Source == TraceTickSourceType.Call
            ? $"{definition.Module}!{definition.Function}"
            : TraceTickTracks.GetSourceText(definition.Source);

    private ActionLinks GetActions(TraceTickDefinition definition)
    {
        var edit = new ActionLink("Edit", () => EditRequested?.Invoke(definition));

        return new ActionLinks(Store.IsAdded(definition) ? [edit, new ActionLink("Delete", () => Store.Remove(definition))] : [edit]);
    }
}
