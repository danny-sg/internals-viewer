using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;

namespace InternalsViewer.Internals.Pages;

/// <summary>
/// PFS (Page Free Space) page
/// </summary>
public class PfsPage : Page
{
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