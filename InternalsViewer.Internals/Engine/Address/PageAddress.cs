using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace InternalsViewer.Internals.Engine.Address;

/// <summary>
/// Page Address that gives a unique address for a page in a database
/// </summary>
[Serializable]
public struct PageAddress : IEquatable<PageAddress>, IComparable<PageAddress>
{
    public static readonly PageAddress Empty = new();
    public const int Size = sizeof(int) + sizeof(short);

    /// <summary>
    /// Gets or sets the file id.
    /// </summary>
    /// <value>The file id.</value>
    public int FileId { get; }

    /// <summary>
    /// Gets or sets the page id.
    /// </summary>
    /// <value>The page id.</value>
    public int PageId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PageAddress"/> struct.
    /// </summary>
    /// <param name="address">The page address in a valid format.</param>
    public PageAddress(string address)
    {
        try
        {
            var pageAddress = Parse(address);

            FileId = pageAddress.FileId;
            PageId = pageAddress.PageId;
        }
        catch
        {
            FileId = 0;
            PageId = 0;
        }
    }

    public PageAddress(int fileId, int pageId)
    {
        FileId = fileId;
        PageId = pageId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PageAddress"/> struct.
    /// </summary>
    /// <param name="address">The page address in internal 6 byte form.</param>
    public PageAddress(byte[] address)
    {
        PageId = BitConverter.ToInt32(address, 0);
        FileId = BitConverter.ToInt16(address, 4);
    }

    /// <summary>
    /// Parses the specified page address.
    /// </summary>
    public static PageAddress Parse(string address)
    {
        var bytePattern = new Regex(@"[0-9a-fA-F]{4}[\x3A][0-9a-fA-F]{8}$");

        if (bytePattern.IsMatch(address))
        {
            return ParseBytes(address);
        }


        var sb = new StringBuilder(address);

        sb.Replace("(", string.Empty);
        sb.Replace(")", string.Empty);
        sb.Replace(",", ":");

        var splitAddress = sb.ToString().Split(@":".ToCharArray());

        if (splitAddress.Length != 2)
        {
            throw new ArgumentException("Invalid Format");
        }

        var parsed = true & int.TryParse(splitAddress[0], out var fileId);

        parsed &= int.TryParse(splitAddress[1], out var pageId);

        if (parsed)
        {
            return new PageAddress(fileId, pageId);
        }

        throw new ArgumentException("Invalid Format");
    }

    /// <summary>
    /// Parses an address from hex bytes.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns></returns>
    private static PageAddress ParseBytes(string address)
    {
        var bytes = address.Split(':');

        int.TryParse(bytes[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var fileId);
        int.TryParse(bytes[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var pageId);

        return new PageAddress(fileId, pageId);
    }

    public override string ToString()
    {
        return string.Format(CultureInfo.CurrentCulture, "({0}:{1})", FileId, PageId);
    }

    public override bool Equals(object obj)
    {
        return obj is PageAddress address && this == address;
    }

    public static bool operator ==(PageAddress address1, PageAddress address2)
    {
        return address1.PageId == address2.PageId && address1.FileId == address2.FileId;
    }

    public static bool operator !=(PageAddress x, PageAddress y)
    {
        return !(x == y);
    }

    public bool Equals(PageAddress pageAddress)
    {
        return FileId == pageAddress.FileId && PageId == pageAddress.PageId;
    }

    public override int GetHashCode() => FileId + 29 * PageId;

    public int CompareTo(PageAddress other) => (FileId.CompareTo(other.FileId) * 9999999) + PageId.CompareTo(other.PageId);
}