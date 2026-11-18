using System;
using System.Buffers.Binary;
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

    private const string VtableField = "Vtable";

    private const string MemoryObjectField = "Memory Object";

    private const string InterfacePrefix = "Interface: ";

    private const string NextField = "Next";

    private const string ItemField = "Item";

    private const int MaximumBytesShown = 16;

    private const string SelfName = "this";

    private const string SelfSuffix = " (Self)";

    private static readonly Colour InfrastructureColour = Colour.FromArgb(0x70, 0x70, 0x70);

    private const int PointerSize = sizeof(ulong);

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

    private List<EmbeddedObject> Embedded { get; set; } = [];

    private bool IsCategoryShown { get; set; } = true;

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

        Embedded = [];

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

        var embedded = owner.History.GetEmbeddedObjects(owner.Block, owner.Position);

        Embedded = embedded;

        var names = GetPointerNames(owner);

        var root = Definition is { } definition
            ? LayoutObjectReader.Read(memory, definition, Address, Size, addresses, embedded, names)
            : LayoutObjectReader.Read(memory, Header, RowBufferFields, Address, Size, addresses, embedded, names);

        IsAvailable = Definition is not null || Scan is not null || root.Fields.Count > 0;

        var objects = Flatten(root).ToList();

        ApplyVtableNames(objects, names);

        var shape = string.Join(";", objects.Select(o => $"{o.Name}:{o.Rows.Count}:{GetFieldShape(o.Fields)}"));

        if (shape != Shape)
        {
            Shape = shape;

            Colours = [.. objects.Select(o => GetColours(o.Fields))];

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

        var isCategoryChanged = owner.IsCategoryVisible != IsCategoryShown;

        IsCategoryShown = owner.IsCategoryVisible;

        if (GetStructure(tabs) == GetStructure(Tabs))
        {
            Update(Tabs, tabs, isCategoryChanged);
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
                    if (item.Fields[field] is { Structure: null, Length: > 0 } found)
                    {
                        var marker = CreateMarker(found.Name,
                                                  start + element * item.Stride + found.Offset,
                                                  found.Length,
                                                  colours[index][field]);

                        if (IsInfrastructure(found))
                        {
                            marker.ForeColour = Colors.White;
                        }

                        yield return marker;
                    }
                }
            }
        }
    }

    private List<ulong> GetPointerAddresses(PlayheadMemoryViewModel owner, List<MemoryHistoryPointer> pointers)
        => [.. pointers.Select(p => Address + (ulong)p.Offset), .. GetUntrackedPointers(owner)];

    private IEnumerable<ulong> GetUntrackedPointers(PlayheadMemoryViewModel owner)
        => GetSlots(owner).Where(s => owner.History.GetUntrackedLabel(s.Value, owner.Position) is not null).Select(s => s.Slot);

    private Dictionary<ulong, string> GetPointerNames(PlayheadMemoryViewModel owner)
    {
        var names = new Dictionary<ulong, string>();

        if (owner.History.GetListNode(owner.Block) is { } node)
        {
            names[Address + (ulong)node.NextOffset] = NextField;

            if (node.ItemOffset is { } item)
            {
                names[Address + (ulong)item] = ItemField;
            }
        }

        foreach (var (slot, value) in GetSlots(owner))
        {
            if (owner.History.IsVtable(value))
            {
                names[slot] = owner.History.GetVtableBase(value) is { } interfaceName ? $"{InterfacePrefix}{interfaceName}" : VtableField;
            }
            else if (owner.History.IsMemoryObject(value, owner.Position))
            {
                names[slot] = MemoryObjectField;
            }
        }

        return names;
    }

    private static void ApplyVtableNames(List<LayoutObject> objects, Dictionary<ulong, string> names)
    {
        foreach (var item in objects.Where(o => o.Layout == MemoryTabLayout.Column))
        {
            for (var index = 0; index < item.Fields.Count; index++)
            {
                var field = item.Fields[index];

                if (field is { Structure: null, Length: PointerSize }
                    && names.TryGetValue(item.Address + (ulong)field.Offset, out var name)
                    && IsVtableName(name)
                    && field.Name != name)
                {
                    item.Fields[index] = field with { Name = name };
                }
            }
        }
    }

    private static bool IsVtableName(string name) => name == VtableField || name.StartsWith(InterfacePrefix, StringComparison.Ordinal);

    private static List<Colour> GetColours(List<MemoryField> fields)
    {
        var colours = CreateColours(fields.Count);

        for (var index = 0; index < fields.Count; index++)
        {
            if (IsInfrastructure(fields[index]))
            {
                colours[index] = InfrastructureColour;
            }
        }

        return colours;
    }

    private static QueryResultSet PutInfrastructureLast(QueryResultSet table, List<MemoryField> fields) => new()
    {
        Columns = table.Columns,
        Rows = [.. table.Rows.OrderBy(r => IsInfrastructure(fields[(int)r.Id]))],
        Source = table.Source
    };

    private static bool IsInfrastructure(MemoryField field) => field.Name == MemoryObjectField || IsVtableName(field.Name);

    private static string GetFieldShape(List<MemoryField> fields) => string.Join(",", fields.Select(f => $"{f.Offset}{f.Name}"));

    private IEnumerable<(ulong Slot, ulong Value)> GetSlots(PlayheadMemoryViewModel owner)
    {
        if (owner.Data is not { } data)
        {
            yield break;
        }

        for (var offset = (int)((PointerSize - Address % PointerSize) % PointerSize);
             offset + PointerSize <= data.Length;
             offset += PointerSize)
        {
            yield return (Address + (ulong)offset, BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset)));
        }
    }

    private string? GetOwnFieldName(int offset)
        => Definition is { } definition
            ? Owner?.History.Store.Layout.GetFieldPath(definition.Type, offset)
            : RowBufferFields.Find(f => f.Offset == offset)?.Name;

    private object GetPointerCell(ulong? slot, ulong pointer)
    {
        if (Owner is not { } owner)
        {
            return string.Empty;
        }

        if (GetSelfLabel(owner, slot, pointer) is { } self)
        {
            return owner.GetPointerCell(pointer, self);
        }

        return owner.History.GetPointerDetail(pointer) is { } detail
            ? owner.GetPointerCell(pointer, detail)
            : owner.GetPointerCell(pointer);
    }

    private string? GetSelfLabel(PlayheadMemoryViewModel owner, ulong? slot, ulong pointer)
    {
        if (pointer < Address || pointer >= Address + (ulong)Size)
        {
            return null;
        }

        var offset = (int)(pointer - Address);

        var label = offset == 0 ? SelfName : TextLabel.GetFieldText(SelfName, GetSelfFieldName(owner, pointer, offset), offset);

        return slot == pointer ? label + SelfSuffix : label;
    }

    private string? GetSelfFieldName(PlayheadMemoryViewModel owner, ulong pointer, int offset)
        => owner.History.GetInterfaceName(pointer, owner.Position)
           ?? Embedded.Find(e => e.Address == pointer)?.Type
           ?? GetOwnFieldName(offset);

    private string GetBytes(ulong address, int length)
    {
        if (Owner?.Data is not { } data || address < Address || (int)(address - Address) + length > data.Length)
        {
            return string.Empty;
        }

        var bytes = data.AsSpan((int)(address - Address), length);

        return bytes.ContainsAnyExcept((byte)0) ? $"0x{Convert.ToHexString(bytes[..Math.Min(length, MaximumBytesShown)])}" : string.Empty;
    }

    private List<MemoryField> GetRowBufferFields(MemoryHistoryReader memory)
        => Scan is { } scan ? AccessorBindingLayout.GetRowBufferFields(AccessorBindingLayout.ReadScanColumns(memory, scan.Iterator)) : [];

    private MemoryObjectTab CreateObjectTab(PlayheadMemoryViewModel owner, LayoutObject item, int index)
    {
        var table = item.Layout == MemoryTabLayout.Row
            ? CreateRowTable(owner.History.Store.Layout, GetPointerCell, item, Colours[index], GetBytes)
            : PutInfrastructureLast(CreateFieldTable(owner.History.Store.Layout,
                                                     GetPointerCell,
                                                     item.Address,
                                                     item.Fields,
                                                     item.Rows[0],
                                                     Colours[index],
                                                     GetBytes),
                                    item.Fields);

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
            tab.SelectedRow = tab.Table?.Rows.FirstOrDefault(r => r.Id == found.Row);
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
