using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.Extensions;
using InternalsViewer.Query.FullTrace.DataPath;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Iterators.Enums;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Logging;
using InternalsViewer.UI.App.Models.Query.Trace.DataPath;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.MemoryHistory;
using Microsoft.UI;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output;
using InternalsViewer.Query.FullTrace.Iterators.PlanNodes.Output.Enums;
using Colour = System.Drawing.Color;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed partial class MemoryIteratorViewModel : ObservableObject
{
    private const int HueWheel = 256;

    private const int FieldSaturation = 60;

    private const int FieldValue = 250;

    private const int FieldWidth = 260;

    private const int OffsetWidth = 80;

    private const int ValueWidth = 320;

    private const int IndexWidth = 60;

    private const int MaximumEntries = 64;

    private const string InputsHeader = "Inputs";

    private const string RowBindingsHeader = "Row Bindings";

    public const string DataPathHeader = "Data Path";

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    private string _header = string.Empty;

    [ObservableProperty]
    private List<MemoryObjectTab> _tabs = [];

    [ObservableProperty]
    private bool _isPlanObjectAvailable;

    public ObservableCollection<Marker>? Markers { get; private set; }

    private PlayheadMemoryViewModel? Owner { get; set; }

    private IteratorTarget? Target { get; set; }

    private ulong Address { get; set; }

    private ulong PlanObject { get; set; }

    private string? ClassName { get; set; }

    private byte[]? Data { get; set; }

    private List<MemoryField> Layout { get; set; } = [];

    private List<Colour> Colours { get; set; } = [];

    private Dictionary<ulong, string>? ValueColumns { get; set; }

    private Dictionary<(int NodeId, ulong Reference), DataPathGraph?> Graphs { get; } = [];

    private IReadOnlyList<string>? RequestedTab { get; set; }

    public void Load(PlayheadMemoryViewModel owner)
    {
        Clear();

        if (owner.History.GetIteratorTarget(owner.Block) is not { } target)
        {
            return;
        }

        var block = owner.History.Store.Blocks[owner.Block];

        Owner = owner;

        Target = target;

        Address = block.Address;

        ClassName = owner.History.AddressMap.GetClassName(block.Address, block.FromPosition, block.ToPosition);

        PlanObject = GetPlanObject(owner.History, target);

        Header = target.TypeName;

        IsAvailable = true;
    }

    public void Clear()
    {
        Owner = null;

        Target = null;

        Address = 0;

        PlanObject = 0;

        IsPlanObjectAvailable = false;

        ClassName = null;

        Data = null;

        Layout = [];

        Colours = [];

        ValueColumns = null;

        Graphs.Clear();

        RequestedTab = null;

        Markers = null;

        IsAvailable = false;

        Header = string.Empty;

        Tabs = [];
    }

    public void SetData(byte[]? data)
    {
        if (data is null || ReferenceEquals(data, Data))
        {
            return;
        }

        Data = data;

        Refresh();
    }

    public void Refresh()
    {
        if (Owner is not { } owner || Target is not { } target || Data is not { } data)
        {
            return;
        }

        var history = owner.History;

        var layout = IteratorLayout.GetFields(history.Store, ClassName, target.Type, data, history.IteratorTargets);

        if (!layout.SequenceEqual(Layout))
        {
            Layout = layout;

            Colours = [.. layout.Select((_, i) => ColourHelpers.HsvToColor(i * HueWheel / layout.Count, FieldSaturation, FieldValue))];

            Markers = new ObservableCollection<Marker>(layout.Select((f, i) => CreateMarker(f, Colours[i])));
        }

        IsPlanObjectAvailable = PlanObject != 0 && history.GetBlock(PlanObject, owner.Position) >= 0;

        var memory = new MemoryHistoryReader(history.Store, owner.Position);

        var tabs = CreateTabs(owner, target, data, memory);

        foreach (var graph in Graphs.Values)
        {
            if (graph is not null)
            {
                DataPathGraphBuilder.Refresh(graph, history, memory);
            }
        }

        if (GetStructure(tabs) == GetStructure(Tabs))
        {
            Update(Tabs, tabs);
        }
        else
        {
            Tabs = tabs;
        }

        SelectRequestedTab();
    }

    public void RequestTab(IReadOnlyList<string> path)
    {
        RequestedTab = path;

        SelectRequestedTab();
    }

    public void OpenPlanObject()
    {
        if (Owner is { } owner && PlanObject != 0)
        {
            owner.RequestMemory(PlanObject);
        }
    }

    private static MemoryObjectTab CreateTab(string header, QueryResultSet table, string key, Action<ResultRow<long>?>? select = null)
        => new(header, [], select) { Table = table, ValueKey = key };

    private static string GetStructure(List<MemoryObjectTab> tabs)
        => string.Join(";", tabs.Select(t => $"{t.Header}[{GetStructure(t.Tabs)}]"));

    private static void Update(List<MemoryObjectTab> current, List<MemoryObjectTab> next)
    {
        for (var index = 0; index < current.Count; index++)
        {
            if (current[index].ValueKey != next[index].ValueKey)
            {
                current[index].Table = next[index].Table;

                current[index].ValueKey = next[index].ValueKey;
            }

            Update(current[index].Tabs, next[index].Tabs);
        }
    }

    private static MemoryObjectTab? CreateInputsTab(PlayheadMemoryViewModel owner, MemoryHistoryReader memory, ulong address)
    {
        var targets = owner.History.IteratorTargets;

        if (IteratorLayout.GetInputs(memory, address, targets) is not { Count: > 0 } inputs)
        {
            return null;
        }

        var columns = new List<ResultColumn>
        {
            new(0, "Input", typeof(string), false) { Width = IndexWidth, Alignment = ResultAlignment.Right },
            new(1, "Iterator", typeof(string), false) { Width = FieldWidth },
            new(2, "Address", typeof(string), true) { Width = ValueWidth }
        };

        var rows = inputs.Select((a, i) => new ResultRow<long>([$"{i}",
                                                                $"{targets[a].TypeName} › {targets[a].Label}",
                                                                owner.GetPointerCell(a)])
        {
            Id = i
        });

        return CreateTab(InputsHeader, new QueryResultSet { Columns = columns, Rows = [.. rows] }, string.Join(",", inputs));
    }

    private static MemoryObjectTab? CreateRowBindingsTab(PlayheadMemoryViewModel owner, MemoryHistoryReader memory, ulong address)
    {
        if (AccessorBindingLayout.ReadScan(memory, address) is not { Bindings: not 0 } scan
            || AccessorBindingLayout.ReadHeader(memory, scan.Bindings) is not { } header
            || AccessorBindingLayout.ReadColumns(memory, header.Columns, header.ColumnCount) is not { Count: > 0 } bound)
        {
            return null;
        }

        var table = MemoryBindingsViewModel.CreateColumnTable(owner, bound, owner.Position, _ => null);

        return CreateTab(RowBindingsHeader, table, $"{scan.Bindings:X}:{bound.Count}");
    }

    private static QueryResultSet CreateFieldTable(PlayheadMemoryViewModel owner,
                                                   List<MemoryField> layout,
                                                   List<ulong> values,
                                                   List<Colour> colours)
    {
        var columns = new List<ResultColumn>
        {
            new(0, "Field", typeof(string), false) { Width = FieldWidth },
            new(1, "Offset", typeof(string), false) { Width = OffsetWidth, Alignment = ResultAlignment.Right },
            new(2, "Value", typeof(string), true) { Width = ValueWidth }
        };

        var rows = layout.Select((f, i) => new ResultRow<long>([new SwatchText(f.Name, colours[i]),
                                                                 $"0x{f.Offset:X}",
                                                                 GetValueCell(owner, f, values[i])])
        {
            Id = i
        });

        return new QueryResultSet { Columns = columns, Rows = [.. rows] };
    }

    private static object GetValueCell(PlayheadMemoryViewModel owner, MemoryField field, ulong value)
    {
        if (field.IsHex)
        {
            return $"0x{value:X8}";
        }

        return field.IsPointer ? owner.GetPointerCell(value) : $"{value:N0}";
    }

    private static ulong GetPlanObject(MemoryHistoryViewModel history, IteratorTarget target)
    {
        if (target.Type == IteratorTargetType.PlanObject || target.Operator?.PlanNodeIdentifier is not { } identifier)
        {
            return 0;
        }

        return history.IteratorTargets
                      .FirstOrDefault(t => t.Value.Type == IteratorTargetType.PlanObject
                                           && t.Value.Operator?.PlanNodeIdentifier == identifier)
                      .Key;
    }

    private static Dictionary<ulong, string> GetValueColumns(MemoryHistoryViewModel history)
    {
        var columns = new Dictionary<ulong, string>();

        foreach (var (address, target) in history.IteratorTargets)
        {
            if (target is not { Type: IteratorTargetType.Iterator, Operator.PlanNodeIdentifier: { } identifier })
            {
                continue;
            }

            var outputs = history.GetOutputColumns(identifier.NodeId);

            var names = history.GetOutputColumnNames(address, outputs.Count);

            for (var index = 0; index < outputs.Count; index++)
            {
                foreach (var link in history.GetOutputPath(identifier.NodeId, outputs[index]).Links)
                {
                    if (link is { Depth: 0, Type: PlanOutputLinkType.Value or PlanOutputLinkType.BufferEntry })
                    {
                        columns.TryAdd(link.Address, $"{names[index]} · {target.Label}");
                    }
                }
            }
        }

        return columns;
    }

    private static string GetValueColumn(MemoryHistoryReader memory, Dictionary<ulong, string> columns, ulong value)
    {
        if (columns.TryGetValue(value, out var column))
        {
            return column;
        }

        return memory.TryRead(value + sizeof(ulong), out ulong entry) && columns.TryGetValue(entry, out var entryColumn)
            ? entryColumn
            : string.Empty;
    }

    private static ulong Read(byte[] data, MemoryField field)
    {
        var value = 0UL;

        for (var index = 0; index < field.Length; index++)
        {
            value |= (ulong)data[field.Offset + index] << (index * 8);
        }

        return value;
    }

    private static Marker CreateMarker(MemoryField field, Colour colour) => new()
    {
        Name = field.Name,
        StartPosition = field.Offset,
        EndPosition = field.Offset + field.Length - 1,
        BackColour = colour.ToWindowsColor(),
        MapColour = colour.ToWindowsColor(),
        ForeColour = Colors.Black
    };

    private List<MemoryObjectTab> CreateTabs(PlayheadMemoryViewModel owner, IteratorTarget target, byte[] data, MemoryHistoryReader memory)
    {
        var values = Layout.Select(f => Read(data, f)).ToList();

        List<MemoryObjectTab> tabs =
        [
            CreateTab(target.Label, CreateFieldTable(owner, Layout, values, Colours), string.Join(",", values), SelectField)
        ];

        if (target.Type == IteratorTargetType.PlanObject)
        {
            if (CreateDataPathTab(owner, target) is { } dataPath)
            {
                tabs.Add(dataPath);
            }

            return tabs;
        }

        if (CreateInputsTab(owner, memory, Address) is { } inputs)
        {
            tabs.Add(inputs);
        }

        if (target.Type == IteratorTargetType.ProfilingIterator)
        {
            return tabs;
        }

        if (IteratorLayout.IsRowset(ClassName) && CreateRowBindingsTab(owner, memory, Address) is { } bindings)
        {
            tabs.Add(bindings);
        }

        for (var index = 0; index < Layout.Count; index++)
        {
            if (memory.Layout.HashIterator.ValueArrayFields.Contains(Layout[index])
                && CreateValuesTab(owner, memory, Layout[index], values[index]) is { } array)
            {
                tabs.Add(array);
            }
        }

        return tabs;
    }

    private MemoryObjectTab? CreateValuesTab(PlayheadMemoryViewModel owner, MemoryHistoryReader memory, MemoryField field, ulong array)
    {
        var store = owner.History.Store;

        var block = array == 0 ? -1 : store.GetBlock(array, owner.Position);

        if (block < 0)
        {
            return null;
        }

        var end = store.Blocks[block].Address + (ulong)store.Blocks[block].Size;

        var count = (int)Math.Min((end - array) / sizeof(ulong), MaximumEntries);

        var entries = Enumerable.Range(0, count)
                                .Select(i => memory.TryRead(array + (ulong)(i * sizeof(ulong)), out ulong value) ? value : 0)
                                .ToList();

        var valueColumns = ValueColumns ??= GetValueColumns(owner.History);

        var columns = new List<ResultColumn>
        {
            new(0, "Entry", typeof(string), false) { Width = IndexWidth, Alignment = ResultAlignment.Right },
            new(1, "Value", typeof(string), false) { Width = ValueWidth },
            new(2, "Column", typeof(string), true) { Width = FieldWidth }
        };

        var rows = entries.Select((e, i) => new ResultRow<long>([$"{i}",
                                                                 owner.GetPointerCell(e),
                                                                 GetValueColumn(memory, valueColumns, e)])
        {
            Id = i
        });

        return CreateTab(field.Name, new QueryResultSet { Columns = columns, Rows = [.. rows] }, string.Join(",", entries));
    }

    private MemoryObjectTab? CreateDataPathTab(PlayheadMemoryViewModel owner, IteratorTarget target)
    {
        if (target.Operator?.PlanNodeIdentifier is not { } identifier)
        {
            return null;
        }

        var history = owner.History;

        var outputs = history.GetOutputColumns(identifier.NodeId);

        var names = history.GetOutputColumnNames(Address, outputs.Count);

        var tabs = new List<MemoryObjectTab>();

        DataPathMap? map = null;

        for (var index = 0; index < outputs.Count; index++)
        {
            var key = (identifier.NodeId, outputs[index].Reference);

            if (!Graphs.TryGetValue(key, out var graph))
            {
                map ??= history.GetDataPathMap();

                Graphs[key] = graph = map.GetOutput(identifier.NodeId, index) is { } field
                    ? DataPathGraphBuilder.Build(history, map, [field], a => Open(owner, a))
                    : null;
            }

            if (graph is not null)
            {
                tabs.Add(new MemoryObjectTab(names[index], [], graph: graph));
            }
        }

        return tabs.Count > 0 ? new MemoryObjectTab(DataPathHeader, tabs) : null;
    }

    private void SelectRequestedTab()
    {
        if (RequestedTab is not { Count: > 0 } path)
        {
            return;
        }

        var tabs = Tabs;

        foreach (var header in path)
        {
            if (tabs.Find(t => t.Header == header) is not { } tab)
            {
                return;
            }

            tab.IsSelected = true;

            tabs = tab.Tabs;
        }

        RequestedTab = null;
    }

    private static void Open(PlayheadMemoryViewModel owner, ulong address)
    {
        if (address != 0 && owner.History.GetBlock(address, owner.Position) >= 0)
        {
            owner.RequestMemory(address);
        }
    }

    private void SelectField(ResultRow<long>? row)
    {
        if (Owner is not { } owner || row is not { Id: var index } || index < 0 || index >= Layout.Count)
        {
            return;
        }

        var field = Layout[(int)index];

        owner.SelectedChangeSpan = new LogRecordAnnotation
        {
            Offset = field.Offset,
            Length = field.Length,
            Description = field.Name,
            OutlineColour = Colours[(int)index].ToWindowsColor()
        };
    }
}
