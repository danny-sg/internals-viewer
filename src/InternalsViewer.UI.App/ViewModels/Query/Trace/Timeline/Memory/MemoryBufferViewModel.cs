using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Query.FullTrace.Memory.BufferPool;
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

    private static readonly List<Colour> FieldColours =
    [
        .. BufferDescriptor.Fields.Select((_, i) => ColourHelpers.HsvToColor(i * HueWheel / BufferDescriptor.Fields.Count,
                                                                             FieldSaturation,
                                                                             FieldValue))
    ];

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

        Markers = IsAvailable ? GetMarkers() : null;
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

        Fields = data is { Length: BufferDescriptor.Size } ? GetFields(owner, data) : [];
    }

    private static List<MemoryBufferField> GetFields(PlayheadMemoryViewModel owner, byte[] data)
    {
        var history = owner.History;

        var address = history.Store.Blocks[owner.Block].Address;

        var state = Read(data, BufferDescriptor.LatchStateOffset);

        return
        [
            GetPageField(owner, address),
            new("Frame", $"0x{Read(data, BufferDescriptor.FrameOffset):X16}"),
            new("Latch", GetLatchText(state)),
            new("Latch Owner", GetPointerText(Read(data, BufferDescriptor.LatchOwnerOffset))),
            new("Latch Waiters", GetPointerText(Read(data, BufferDescriptor.LatchWaitersOffset))),
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

    private static ulong Read(byte[] data, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset));

    private static ObservableCollection<Marker> GetMarkers()
        => new(BufferDescriptor.Fields.Select((f, i) => new Marker
        {
            Name = f.Name,
            StartPosition = f.Offset,
            EndPosition = f.Offset + f.Length - 1,
            BackColour = FieldColours[i].ToWindowsColor(),
            MapColour = FieldColours[i].ToWindowsColor(),
            ForeColour = Colors.Black
        }));
}
