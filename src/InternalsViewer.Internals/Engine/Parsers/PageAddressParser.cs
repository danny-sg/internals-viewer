using System.Text.RegularExpressions;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Engine.Parsers;

public static class PageAddressParser
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

        var match = Regex.Match(address, @"(\d+):(\d+)");

        if (match.Success)
        {
            return new PageAddress(short.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
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
        var bytes = Convert.FromHexString(address[2..]);

        return Parse(bytes);
    }

    /// <summary>
    /// Parses a page address from a byte array
    /// </summary>
    /// <remarks>
    /// Page Address is stored in 6 bytes, 4 bytes for the page id and 2 bytes for the file id
    /// </remarks>
    public static PageAddress Parse(byte[] data)
    {
        return Parse(data, 0);
    }

    /// <summary>
    /// Parses a page address from a byte array
    /// </summary>
    /// <remarks>
    /// Page Address is stored in 6 bytes, 4 bytes for the page id and 2 bytes for the file id
    /// </remarks>
    public static PageAddress Parse(byte[] data, int startAddress)
    {
        var pageId = BitConverter.ToInt32(data, startAddress);
        var fileId = BitConverter.ToInt16(data, startAddress + 4);

        return new PageAddress(fileId, pageId);
    }

    public static PageAddress ToPageAddress(this byte[]? address)
    {
        if (address == null)
        {
            return PageAddress.Empty;
        }

        if(address.Length != PageAddress.Size)
        {
            throw new ArgumentException("Invalid page address format", nameof(address));
        }

        return Parse(address);
    }
}
