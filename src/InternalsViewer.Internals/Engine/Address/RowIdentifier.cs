using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace InternalsViewer.Internals.Engine.Address;

/// <summary>
/// Row Identifier (RID)
/// </summary>
public sealed record RowIdentifier
{
    public static readonly RowIdentifier Empty = new(PageAddress.Empty, 0);

    public const int Size = sizeof(short) + sizeof(short) + sizeof(int);

    public PageAddress PageAddress { get; set; }

    public ushort SlotId { get; set; }

    public RowIdentifier(byte[] address)
        : this(address.AsSpan())
    {
    }

    public RowIdentifier(ReadOnlySpan<byte> address)
    {
        PageAddress = new PageAddress(BinaryPrimitives.ReadInt16LittleEndian(address[4..]),
                                      BinaryPrimitives.ReadInt32LittleEndian(address));

        SlotId = BinaryPrimitives.ReadUInt16LittleEndian(address[6..]);
    }

    public RowIdentifier(PageAddress page, ushort slot)
    {
        PageAddress = page;
        SlotId = slot;
    }

    public RowIdentifier(short fileId, int pageId, ushort slot)
    {
        PageAddress = new PageAddress(fileId, pageId);
        SlotId = slot;
    }

    public static RowIdentifier Parse(string? address)
    {
        ushort slot = 0;

        var sb = new StringBuilder(address);

        sb.Replace("(", string.Empty);
        sb.Replace(")", string.Empty);
        sb.Replace(",", ":");

        var splitAddress = sb.ToString().Split(@":".ToCharArray());

        if (splitAddress.Length < 2)
        {
            throw new ArgumentException("Invalid format");
        }

        var parsed = true & short.TryParse(splitAddress[0], out var fileId);
        parsed &= int.TryParse(splitAddress[1], out var pageId);

        if (splitAddress.Length > 2)
        {
            parsed &= ushort.TryParse(splitAddress[2], out slot);
        }

        if (parsed)
        {
            return new RowIdentifier(new PageAddress(fileId, pageId), slot);
        }

        throw new ArgumentException("Invalid format");
    }

    public override string ToString()
    {
        return string.Format(CultureInfo.CurrentCulture,
                             "({0}:{1}:{2})",
                             PageAddress.FileId,
                             PageAddress.PageId,
                             SlotId);
    }
}