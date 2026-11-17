using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace.Iterators;
using InternalsViewer.Query.FullTrace.Memory.History;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Bindings;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs;
using InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs.Enums;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Logging;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using Microsoft.UI;
using static InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory.MemoryFieldTableBuilder;
using Colour = System.Drawing.Color;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed partial class MemoryGeneratedTabViewModel : ObservableObject
{
    private const string RowBufferHeader = "Row Buffer";

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    private string _header = string.Empty;

    [ObservableProperty]
    private List<MemoryObjectTab> _tabs = [];

    public ObservableCollection<Marker>? Markers { get; private set; }

    private PlayheadMemoryViewModel? Owner { get; set; }

    private MemoryTab? Definition { get; set; }

    private ScanBindingReference? Scan { get; set; }

    private IteratorTarget? Target { get; set; }

    private string? ClassName { get; set; }

    private MemoryIteratorTabBuilder IteratorTabs { get; } = new();

    private IReadOnlyList<string>? RequestedTab { get; set; }

    private string Shape { get; set; } = string.Empty;

    private List<MemoryField> RowBufferFields { get; set; } = [];

    private ulong Address { get; set; }

    private int Size { get; set; }

    private LayoutObject? Root { get; set; }

    private List<LayoutObject> Objects { get; set; } = [];

    private List<List<Colour>> Colours { get; set; } = [];

    private ulong? RequestedAddress { get; set; }

    private bool IsFieldRequested { get; set; }

    public void Load(PlayheadMemoryViewModel owner)
    {
        Clear();

        var history = owner.History;

        var block = history.Store.Blocks[owner.Block];

        var type = history.GetLayoutType(owner.Block);

        var definition = MemoryTabTable.Default.Find(history.Store.Layout, type);

        var scan = definition is null ? history.FindScan(owner.Block, owner.Position) : null;

        var target = history.GetIteratorTarget(owner.Block);

        Owner = owner;

        Definition = definition is null ? null : definition with { Type = type!, Header = target?.Label ?? definition.Header };

        Scan = scan?.RowBuffer == block.Address ? scan : null;

        Target = target;

        ClassName = history.AddressMap.GetClassName(block.Address, block.FromPosition, block.ToPosition);

        Address = block.Address;

        Size = block.Size;

        Header = target?.TypeName ?? definition?.Header ?? (Scan is null ? history.GetTypeName(owner.Block) : RowBufferHeader);

        IsAvailable = true;
    }

    public void Clear()
    {
        Owner = null;

        Definition = null;

        Scan = null;

        RowBufferFields = [];

        Target = null;

        ClassName = null;

        IteratorTabs.Clear();

        RequestedTab = null;

        Shape = string.Empty;

        Address = 0;

        Size = 0;

        Root = null;

        Objects = [];

        Colours = [];

        RequestedAddress = null;

        IsFieldRequested = false;

        Markers = null;

        IsAvailable = false;

        Header = string.Empty;

        Tabs = [];
    }

    public void Refresh(List<MemoryHistoryPointer> pointers)
    {
        if (Owner is not { } owner)
        {
            return;
        }

        var memory = new MemoryHistoryReader(owner.History.Store, owner.Position);

        RowBufferFields = GetRowBufferFields(memory);

        var addresses = GetPointerAddresses(owner, pointers);

        var root = Definition is { } definition
            ? LayoutObjectReader.Read(memory, definition, Address, Size, addresses)
            : LayoutObjectReader.Read(memory, Header, RowBufferFields, Address, Size, addresses);

        IsAvailable = Definition is not null || Scan is not null || root.Fields.Count > 0;

        var objects = Flatten(root).ToList();

        var shape = string.Join(";", objects.Select(o => $"{o.Name}:{o.Rows.Count}:{string.Join(",", o.Fields.Select(f => f.Offset))}"));

        if (shape != Shape)
        {
            Shape = shape;

            Colours = [.. objects.Select(o => CreateColours(o.Fields.Count))];

            Markers = new ObservableCollection<Marker>(GetMarkers(objects, Colours, Address));
        }

        Root = root;

        Objects = objects;

        var tabs = objects.Select((o, i) => CreateObjectTab(owner, o, i)).ToList();

        if (Target is { } target)
        {
            tabs.AddRange(IteratorTabs.CreateTabs(owner, target, ClassName, root, memory));

            IteratorTabs.Refresh(owner.History, memory);
        }

        if (GetStructure(tabs) == GetStructure(Tabs))
        {
            Update(Tabs, tabs);
        }
        else
        {
            Tabs = tabs;
        }

        SelectRequested();

        SelectRequestedTab();
    }

    public void RequestTab(IReadOnlyList<string> path)
    {
        RequestedTab = path;

        SelectRequestedTab();
    }

    public bool Select(ulong address, bool isFieldSelected)
    {
        if (!IsAvailable || address < Address || address >= Address + (ulong)Size)
        {
            return false;
        }

        RequestedAddress = address;

        IsFieldRequested = isFieldSelected;

        SelectRequested();

        return true;
    }

    private static IEnumerable<LayoutObject> Flatten(LayoutObject item) => item.Children.SelectMany(Flatten).Prepend(item);

    private static IEnumerable<Marker> GetMarkers(List<LayoutObject> objects, List<List<Colour>> colours, ulong address)
    {
        for (var index = 0; index < objects.Count; index++)
        {
            var item = objects[index];

            var start = (int)(item.Address - address);

            var elements = item.Layout == MemoryTabLayout.Row ? item.Rows.Count : 1;

            for (var element = 0; element < elements; element++)
            {
                for (var field = 0; field < item.Fields.Count; field++)
                {
                    if (item.Fields[field] is { Structure: null } found)
                    {
                        yield return CreateMarker(found.Name,
                                                  start + element * item.Stride + found.Offset,
                                                  found.Length,
                                                  colours[index][field]);
                    }
                }
            }
        }
    }

    private List<ulong> GetPointerAddresses(PlayheadMemoryViewModel owner, List<MemoryHistoryPointer> pointers)
        => [.. pointers.Where(p => p.Block != owner.Block || GetOwnFieldName((int)(p.Value - Address)) is not null)
                       .Select(p => Address + (ulong)p.Offset)];

    private string? GetOwnFieldName(int offset)
        => Definition is { } definition
            ? Owner?.History.Store.Layout.GetFieldPath(definition.Type, offset)
            : RowBufferFields.Find(f => f.Offset == offset)?.Name;

    private object GetPointerCell(ulong pointer)
    {
        if (Owner is not { } owner)
        {
            return string.Empty;
        }

        if (owner.History.GetPointerDetail(pointer) is { } detail)
        {
            return owner.GetPointerCell(pointer, detail);
        }

        if (Definition is null
            && pointer >= Address
            && pointer < Address + (ulong)Size
            && GetOwnFieldName((int)(pointer - Address)) is { } field)
        {
            return owner.GetPointerCell(pointer, $"{owner.History.GetPointerTarget(Address, owner.Position)}.{field}");
        }

        return owner.GetPointerCell(pointer);
    }

    private List<MemoryField> GetRowBufferFields(MemoryHistoryReader memory)
        => Scan is { } scan ? AccessorBindingLayout.GetRowBufferFields(AccessorBindingLayout.ReadScanColumns(memory, scan.Iterator)) : [];

    private MemoryObjectTab CreateObjectTab(PlayheadMemoryViewModel owner, LayoutObject item, int index)
    {
        var table = item.Layout == MemoryTabLayout.Row
            ? CreateRowTable(owner.History.Store.Layout, GetPointerCell, item, Colours[index])
            : CreateFieldTable(owner.History.Store.Layout, GetPointerCell, item.Fields, item.Rows[0], Colours[index]);

        var key = string.Join(";", item.Rows.Select(r => string.Join(",", r)));

        return CreateTab(item.Name, table, key, r => SelectField(index, r));
    }

    private void SelectRequested()
    {
        if (RequestedAddress is not { } address || Root?.Find(address) is not { } found)
        {
            return;
        }

        var index = Objects.IndexOf(found.Object);

        if (index < 0 || index >= Tabs.Count)
        {
            return;
        }

        RequestedAddress = null;

        var tab = Tabs[index];

        tab.IsSelected = true;

        if (IsFieldRequested)
        {
            tab.SelectedRow = tab.Table?.Rows.ElementAtOrDefault(found.Row);
        }
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

    private void SelectField(int index, ResultRow<long>? row)
    {
        if (Owner is not { } owner || row is not { Id: var id } || index >= Objects.Count)
        {
            return;
        }

        var item = Objects[index];

        var start = (int)(item.Address - Address);

        if (item.Layout == MemoryTabLayout.Row)
        {
            owner.SelectedMarker = null;

            owner.SelectedChangeSpan = new LogRecordAnnotation
            {
                Offset = start + (int)id * item.Stride,
                Length = item.Stride,
                Description = $"{item.Name} {id}",
                OutlineColour = Colors.Red
            };

            return;
        }

        var field = item.Fields[(int)id];

        owner.SelectedChangeSpan = null;

        owner.SelectedMarker = Markers?.FirstOrDefault(m => m.StartPosition == start + field.Offset && m.Name == field.Name);
    }
}
