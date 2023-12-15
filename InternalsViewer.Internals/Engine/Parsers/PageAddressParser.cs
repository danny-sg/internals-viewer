using System;
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
    /// 
    /// </remarks>
    public static PageAddress Parse(byte[] address)
    {
        var pageId = BitConverter.ToInt32(address, 0);
        var fileId = BitConverter.ToInt16(address, 4);

        return new PageAddress(fileId, pageId);
    }
}
