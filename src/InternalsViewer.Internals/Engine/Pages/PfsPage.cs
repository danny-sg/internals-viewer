using System.Text;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Engine.Pages;

/// <summary>
/// PFS (Page Free Space) page
/// </summary>
/// <remarks>
/// Information about page allocation and free space available on pages.
/// </remarks>
public sealed class PfsPage : Page
{
    /// <summary>
    /// Interval between PFS pages = 8088 bytes/pages (1 byte = 1 pfs entry)
    /// </summary>
    public const int PfsInterval = 8088;

    /// <summary>
    /// The first PFS page in a file is always page 1
    /// </summary>
    public const int FirstPfsPage = 1;

    /// <summary>
    /// PFS bytes collection (as raw byte value)
    /// </summary>
    public byte[] PfsBytes { get; set; } = [];

    public override string ToString()
    {
        var sb = new StringBuilder();

        for (var i = 0; i <= PfsBytes.Length - 1; i++)
        {
            sb.AppendFormat("{0,-14}{1}", new PageAddress(1, i), PfsBytes[i]);

            sb.Append(Environment.NewLine);
        }

        return sb.ToString();
    }
}