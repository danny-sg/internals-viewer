using InternalsViewer.Execution.Interfaces.AccessPaths.Binding;
using InternalsViewer.Execution.Interfaces.Pages;
using System.Collections.Immutable;
using System.Data;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Values;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Records;
using InternalsViewer.Internals.Interfaces.Annotations;
using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Tests.UnitTests.AccessPaths;

/// <summary>
/// An in memory index page built from integer keys, used to drive access path tests
/// </summary>
internal sealed class TestIndexPage(PageAddress pageAddress,
                                    IReadOnlyList<int> keys,
                                    ISet<int>? ghostSlots = null,
                                    byte level = 0)
    : IIndexPageAccessor
{
    private IReadOnlyList<int> Keys { get; } = keys;

    private ISet<int> GhostSlots { get; } = ghostSlots ?? new HashSet<int>();

    public PageAddress PageAddress { get; } = pageAddress;

    public byte Level { get; } = level;

    public bool IsRoot { get; set; }

    public bool IsLeaf => Level == 0;

    public int SlotCount => Keys.Count;

    public PageAddress NextPage { get; set; } = PageAddress.Empty;

    public PageAddress PreviousPage { get; set; } = PageAddress.Empty;

    /// <summary>
    /// Number of times a key comparison was requested, used to verify counter totals
    /// </summary>
    public int CompareCount { get; private set; }

    public static TestIndexPage Create(params int[] keys)
    {
        return new TestIndexPage(new PageAddress(1, 100), keys);
    }

    public AccessKey GetKey(int slot)
    {
        return AccessKey.Create(AccessValue.FromInteger(SqlDbType.Int, Keys[slot]));
    }

    public int CompareKeyPrefix(int slot, in AccessKey target, int width)
    {
        CompareCount++;

        return GetKey(slot).ComparePrefix(target, width);
    }

    public PageAddress GetChildPage(int slot)
    {
        if (IsLeaf)
        {
            throw new NotSupportedException("The page is a leaf");
        }

        return new PageAddress(1, Keys[slot]);
    }

    public IRecord GetRecord(int slot)
    {
        return new TestRecord(slot, GhostSlots.Contains(slot), Keys[slot]);
    }

    public IRowValueSource BindRow(int slot)
    {
        return new TestRowValueSource(Keys[slot]);
    }
}

/// <summary>
/// A record exposing a single integer column
/// </summary>
internal sealed class TestRecord(int slot, bool isGhost, int key) : IRecord
{
    public int Slot { get; } = slot;

    public ushort Offset => (ushort)(96 + (Slot * 8));

    public List<RecordField> Fields { get; } = [];

    public short ColumnCount => 1;

    public bool IsGhost { get; } = isGhost;

    public int Key { get; } = key;

    public List<DataStructureItem> MarkItems { get; } = [];
}

internal sealed class TestRowValueSource(int key) : IRowValueSource
{
    private int Key { get; } = key;

    public AccessValue GetValue(int ordinal, string? columnName = null)
    {
        if (ordinal == 0 || (ordinal < 0 && columnName is not null))
        {
            return AccessValue.FromInteger(SqlDbType.Int, Key);
        }

        return AccessValue.Null;
    }
}
