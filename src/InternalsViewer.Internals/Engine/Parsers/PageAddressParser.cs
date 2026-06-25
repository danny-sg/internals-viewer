using System.Buffers.Binary;
using System.Text.RegularExpressions;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Engine.Parsers;

public static partial class PageAddressParser
{
    /// <summary>
    /// Parses a page address from a string in number format
    /// </summary>
    public static PageAddress Parse(string address)
    {
        if (address.StartsWith("0x"))
        {
            return ParseBytes(address);
        }

        var decimalMatch = DecimalFormatRegEx().Match(address);

        if (decimalMatch.Success)
        {
            return new PageAddress(short.Parse(decimalMatch.Groups[1].Value), int.Parse(decimalMatch.Groups[2].Value));
        }

        var hexMatch = HexFormatRegEx().Match(address);

        if (hexMatch.Success)
        {
            return new PageAddress(
                Convert.ToInt16(hexMatch.Groups[1].Value, 16),
                Convert.ToInt32(hexMatch.Groups[2].Value, 16));
        }

        throw new ArgumentException("Invalid page address format", nameof(address));
    }

    public static bool TryParse(string address, out PageAddress pageAddress)
    {
        try
        {
            pageAddress = Parse(address);

            return true;
        }
        catch
        {
            pageAddress = PageAddress.Empty;

            return false;
        }
    }

    /// <summary>
    /// Parses a page address from a hex string
    /// </summary>
    /// <remarks>
    /// Assumes the hex string starts with 0x
    /// </remarks>
    public static PageAddress ParseBytes(string address)
    {
        var bytes = Convert.FromHexString(address.AsSpan(2));

        return Parse(bytes);
    }

    /// <summary>
    /// Parses a page address from a byte array
    /// </summary>
    public static PageAddress Parse(byte[] data) => Parse(data.AsSpan());

    /// <summary>
    /// Parses a page address from a byte array at the given offset
    /// </summary>
    public static PageAddress Parse(byte[] data, int startAddress) => Parse(data.AsSpan(startAddress));

    /// <summary>
    /// Parses a page address from a span at the given offset
    /// </summary>
    public static PageAddress Parse(ReadOnlySpan<byte> data, int startAddress) => Parse(data[startAddress..]);

    /// <summary>
    /// Parses a page address from a span
    /// </summary>
    /// <remarks>
    /// Page Address is stored in 6 bytes, 4 bytes for the page id and 2 bytes for the file id
    /// </remarks>
    public static PageAddress Parse(ReadOnlySpan<byte> data)
    {
        var pageId = BinaryPrimitives.ReadInt32LittleEndian(data);
        var fileId = BinaryPrimitives.ReadInt16LittleEndian(data[4..]);

        return new PageAddress(fileId, pageId);
    }

    public static PageAddress ToPageAddress(this byte[]? address)
    {
        if (address == null)
        {
            return PageAddress.Empty;
        }

        if (address.Length != PageAddress.Size)
        {
            throw new ArgumentException("Invalid page address format", nameof(address));
        }

        return Parse(address.AsSpan());
    }

    public static PageAddress ToPageAddress(this ReadOnlySpan<byte> address)
    {
        if (address.Length != PageAddress.Size)
        {
            throw new ArgumentException("Invalid page address format", nameof(address));
        }

        return Parse(address);
    }

    [GeneratedRegex(@"^\(?(\d+):(\d+)\)?$")]
    private static partial Regex DecimalFormatRegEx();

    [GeneratedRegex(@"^([0-9A-Fa-f]{4}):([0-9A-Fa-f]{8})$")]
    private static partial Regex HexFormatRegEx();
}