using System;
using System.Globalization;
using System.Text;

namespace InternalsViewer.Internals.Engine.Address;

/// <summary>
/// Row Identifier (RID)
/// </summary>
public struct RowIdentifier
{
    public const int Size = sizeof(short) + sizeof(short) + sizeof(int);

    public PageAddress PageAddress { get; set; }

    public int SlotId { get; set; }

    public RowIdentifier(byte[] address)
    {
        PageAddress = new PageAddress(BitConverter.ToInt16(address, 4), BitConverter.ToInt32(address, 0));
        SlotId = BitConverter.ToInt16(address, 6);
    }

    public RowIdentifier(PageAddress page, int slot)
    {
        PageAddress = page;
        SlotId = slot;
    }

    public RowIdentifier(int fileId, int pageId, int slot)
    {
        PageAddress = new PageAddress(fileId, pageId);
        SlotId = slot;
    }

    public static RowIdentifier Parse(string address)
    {
        short slot = 0;

        var sb = new StringBuilder(address);

        sb.Replace("(", string.Empty);
        sb.Replace(")", string.Empty);
        sb.Replace(",", ":");

        var splitAddress = sb.ToString().Split(@":".ToCharArray());

        if (splitAddress.Length < 2)
        {
            throw new ArgumentException("Invalid format");
        }

        var parsed = true & int.TryParse(splitAddress[0], out var fileId);
        parsed &= int.TryParse(splitAddress[1], out var pageId);

        if (splitAddress.Length > 2)
        {
            parsed &= short.TryParse(splitAddress[2], out slot);
        }

        if (parsed)
        {
            return new RowIdentifier(new PageAddress(fileId, pageId), slot);
        }

        throw new ArgumentException("Invalid format");
    }

    public override string ToString()
    {
        return string.Format(CultureInfo.CurrentCulture, "({0}:{1}:{2})",
            PageAddress.FileId,
            PageAddress.PageId,
            SlotId);
    }
}