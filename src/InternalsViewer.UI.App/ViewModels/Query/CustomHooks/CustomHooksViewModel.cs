using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.Query.FullTrace.CustomHooks;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Models;

namespace InternalsViewer.UI.App.ViewModels.Query.CustomHooks;

public sealed partial class CustomHooksViewModel : ObservableObject, IDisposable
{
    private const int NameWidth = 220;

    private const int FunctionWidth = 360;

    private const int ArgumentWidth = 140;

    private const int ReadAtWidth = 80;

    private const int NumberWidth = 90;

    private const int CallWidth = 80;

    private const int AddressWidth = 170;

    private const int CountWidth = 110;

    private const int ActionsWidth = 130;

    [ObservableProperty]
    private QueryResultSet? _hooksTable;

    [ObservableProperty]
    private ResultRow<long>? _selectedHookRow;

    [ObservableProperty]
    private QueryResultSet? _capturesTable;

    [ObservableProperty]
    private ResultRow<long>? _selectedCaptureRow;

    [ObservableProperty]
    private byte[]? _data;

    [ObservableProperty]
    private int _baseAddress;

    public CustomHooksViewModel(SettingsViewModel settings, Action<CallStackNode, int> openCall)
    {
        Settings = settings;

        OpenCall = openCall;

        Settings.PropertyChanged += OnSettingsPropertyChanged;

        Refresh();
    }

    private SettingsViewModel Settings { get; }

    private Action<CallStackNode, int> OpenCall { get; }

    private TraceResult? Trace { get; set; }

    private IReadOnlyList<CustomHookDefinition> Definitions { get; set; } = [];

    private IReadOnlyList<CustomHookCapture> Captures { get; set; } = [];

    private CustomHookDefinition? SelectedDefinition { get; set; }

    public void SetTrace(TraceResult? trace)
    {
        Trace = trace;

        Refresh();
    }

    public void ShowCall(CallValue value) => OpenCall(value.Node, value.Call);

    public void Dispose() => Settings.PropertyChanged -= OnSettingsPropertyChanged;

    partial void OnSelectedHookRowChanged(ResultRow<long>? value)
    {
        SelectedDefinition = value is { Id: var index } && index < Definitions.Count ? Definitions[(int)index] : null;

        ShowCaptures();
    }

    partial void OnSelectedCaptureRowChanged(ResultRow<long>? value)
    {
        var capture = value is { Id: var index } && index < Captures.Count ? Captures[(int)index] : null;

        BaseAddress = SelectedDefinition?.Offset ?? 0;

        Data = capture?.Data;
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.CustomHooks))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var selected = SelectedDefinition?.Name;

        Definitions = Settings.CustomHooks;

        HooksTable = BuildHooksTable();

        SelectedHookRow = HooksTable.Rows.FirstOrDefault(r => Definitions[(int)r.Id].Name == selected) ?? HooksTable.Rows.FirstOrDefault();

        if (SelectedHookRow is null)
        {
            SelectedDefinition = null;

            ShowCaptures();
        }
    }

    private QueryResultSet BuildHooksTable()
    {
        var columns = new List<ResultColumn>
        {
            new(0, "Name", typeof(string), false) { Width = NameWidth },
            new(1, "Function", typeof(string), false) { Width = FunctionWidth },
            new(2, "Argument", typeof(string), false) { Width = ArgumentWidth },
            new(3, "Read At", typeof(string), false) { Width = ReadAtWidth },
            new(4, "Offset", typeof(string), false) { Width = NumberWidth, Alignment = ResultAlignment.Right },
            new(5, "Length", typeof(int), false) { Width = NumberWidth, Alignment = ResultAlignment.Right },
            new(6, "Capture Count", typeof(int), false) { Width = CountWidth, Alignment = ResultAlignment.Right },
            new(7, string.Empty, typeof(string), false) { Width = ActionsWidth }
        };

        var rows = Definitions.Select((d, index) => new ResultRow<long>([d.Name,
                                                                        $"{d.Module}!{d.Function}",
                                                                        d.Argument,
                                                                        d.IsReadAtReturn ? "Return" : "Call",
                                                                        FormatOffset(d.Offset),
                                                                        d.Length,
                                                                        Trace?.CustomHooks.GetCaptures(d.Name).Count,
                                                                        GetActions(d)])
                                                       {
                                                           Id = index
                                                       })
                              .ToArray();

        return new QueryResultSet { Columns = columns, Rows = rows };
    }

    private void ShowCaptures()
    {
        Captures = SelectedDefinition is { } definition && Trace is { } trace ? trace.CustomHooks.GetCaptures(definition.Name) : [];

        var columns = new List<ResultColumn>
        {
            new(0, "Call", typeof(long), false) { Width = CallWidth, Alignment = ResultAlignment.Right },
            new(1, "Thread", typeof(uint), false) { Width = CallWidth, Alignment = ResultAlignment.Right },
            new(2, "Address", typeof(string), false) { Width = AddressWidth },
            new(3, "Bytes", typeof(int), false) { Width = NumberWidth, Alignment = ResultAlignment.Right }
        };

        var rows = new ResultRow<long>[Captures.Count];

        for (var index = 0; index < rows.Length; index++)
        {
            var capture = Captures[index];

            rows[index] = new ResultRow<long>([GetCallValue(capture), capture.Thread, $"0x{capture.Address:X16}", capture.Data.Length])
            {
                Id = index
            };
        }

        CapturesTable = new QueryResultSet { Columns = columns, Rows = rows };

        SelectedCaptureRow = rows.FirstOrDefault();
    }

    private ActionLinks GetActions(CustomHookDefinition definition)
        => new([new ActionLink(definition.IsPaused ? "Resume" : "Pause",
                               () => Settings.SetCustomHookPaused(definition, !definition.IsPaused)),
                new ActionLink("Delete", () => Settings.RemoveCustomHook(definition))]);

    private object GetCallValue(CustomHookCapture capture)
    {
        if (Trace?.CallStore is { } store
            && store.GetCallList(capture.Function, capture.Instance) is { } calls
            && capture.Call < calls.Count
            && store.GetNode(calls[capture.Call]) is { } node)
        {
            return new CallValue(node, capture.Call);
        }

        return (long)capture.Call + 1;
    }

    private static string FormatOffset(int offset)
        => offset < 0 ? $"-0x{-offset:X}" : $"0x{offset.ToString("X", CultureInfo.InvariantCulture)}";
}
