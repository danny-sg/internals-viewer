using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
using InternalsViewer.Query.FullTrace.Memory.Layouts;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using Microsoft.UI;
using Colour = System.Drawing.Color;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace.Timeline.Memory;

public sealed partial class MemoryBufferViewModel : ObservableObject
{
    private const int HueWheel = 256;

    private const int FieldSaturation = 60;

    private const int FieldValue = 250;

    private const string NoneText = "None";

    private const string UnknownText = "Unknown";

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    private List<MemoryBufferField> _fields = [];

    public ObservableCollection<Marker>? Markers { get; private set; }

    private PlayheadMemoryViewModel? Owner { get; set; }

    private byte[]? Data { get; set; }

    public void Load(PlayheadMemoryViewModel owner)
    {
        Clear();

        Owner = owner;

        IsAvailable = owner.History.IsBuffer(owner.Block);

        Markers = IsAvailable ? GetMarkers(owner.History.Store.Layout.Buffer) : null;
    }

    public void Clear()
    {
        Owner = null;

        IsAvailable = false;

        Markers = null;

        Data = null;

        Fields = [];
    }

    public void SetData(byte[]? data)
    {
        if (!IsAvailable || Owner is not { } owner || ReferenceEquals(data, Data))
        {
            return;
        }

        Data = data;

        Fields = data?.Length == owner.History.Store.Layout.Buffer.Size ? GetFields(owner, data) : [];
    }

    private static List<MemoryBufferField> GetFields(PlayheadMemoryViewModel owner, byte[] data)
    {
        var history = owner.History;

        var address = history.Store.Blocks[owner.Block].Address;

        var layout = history.Store.Layout.Buffer;

        var state = Read(data, layout.LatchStateField);

        return
        [
            GetPageField(owner, address),
            new("Database Id", $"{ReadShort(data, layout.DatabaseIdField)}"),
            new("Frame", $"0x{Read(data, layout.FrameField):X16}"),
            new("Partition", $"{ReadShort(data, layout.PartitionField)}"),
            new("Status", $"0x{ReadInt(data, layout.StatusField):X}"),
            new("Error Code", $"{ReadInt(data, layout.ErrorCodeField)}"),
            new("State History", $"0x{ReadInt(data, layout.StateHistoryField):X8}"),
            new("Read \u00B5s", $"{Read(data, layout.ReadTimeField)}"),
            new("Last Used", $"{ReadShort(data, layout.LastUsedField)}"),
            new("Recovery Unit", GetPointerText(Read(data, layout.RecoveryUnitField))),
            new("Latch", GetLatchText(state)),
            new("Latch Owner", GetPointerText(Read(data, layout.LatchOwnerField))),
            new("Latch Waiters", GetPointerText(Read(data, layout.LatchWaitersField))),
            new("Latch State", $"0x{state:X16}")
        ];
    }

    private static MemoryBufferField GetPageField(PlayheadMemoryViewModel owner, ulong address)
    {
        var history = owner.History;

        if (history.GetBufferPage(address, owner.Position) is not { } reference)
        {
            return new("Page", UnknownText);
        }

        var name = history.GetBufferPageName(reference);

        return history.IsQueryDatabase(reference) ? new("Page", name, () => owner.Navigator?.OpenBufferPage(reference)) : new("Page", name);
    }

    private static string GetLatchText(ulong state)
    {
        var shared = BufferDescriptor.GetSharedCount(state);

        var mode = BufferDescriptor.IsExclusive(state)
            ? "Exclusive"
            : shared > 0
                ? $"Shared, {shared} {(shared == 1 ? "Holder" : "Holders")}"
                : "Released";

        return BufferDescriptor.HasWaiters(state) ? $"{mode}, Waiters" : mode;
    }

    private static string GetPointerText(ulong value) => value == 0 ? NoneText : $"0x{value:X16}";

    private static ulong Read(byte[] data, MemoryField field) => BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(field.Offset));

    private static uint ReadInt(byte[] data, MemoryField field) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(field.Offset));

    private static ushort ReadShort(byte[] data, MemoryField field) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(field.Offset));

    private static ObservableCollection<Marker> GetMarkers(EngineLayout.BufferLayout layout)
    {
        var fields = layout.Fields;

        return new(fields.Select((f, i) => CreateMarker(f, ColourHelpers.HsvToColor(i * HueWheel / fields.Count,
                                                                                    FieldSaturation,
                                                                                    FieldValue))));
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
}
