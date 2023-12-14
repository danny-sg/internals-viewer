using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Allocation;

namespace InternalsViewer.Internals.Pages;

/// <summary>
/// PFS (Page Free Space) page
/// </summary>
public class PfsPage : Page
{
    /// <summary>
    /// Interval between PFS pages = 8088 bytes/pages (1 byte = 1 pfs entry)
    /// </summary>
    public const int PfsInterval = 8088;

    /// <summary>
    /// Gets or sets the PFS bytes collection.
    /// </summary>
    public List<PfsByte> PfsBytes { get; set; } = new();

    public override string ToString()
    {
        var sb = new StringBuilder();

        for (var i = 0; i <= PfsBytes.Count - 1; i++)
        {
            sb.AppendFormat("{0,-14}{1}", new PageAddress(1, i), PfsBytes[i]);
            sb.Append(Environment.NewLine);
        }

        return sb.ToString();
    }
}