﻿using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Pages;

/// <inheritdoc />
/// <summary>
/// PFS (Page Free Space) page
/// </summary>
public class PfsPage : Page
{
    private const int PfsOffset = 100;
    private const int PfsSize = 8088;

    /// <summary>
    /// Initializes a new instance of the <see cref="PfsPage"/> class.
    /// </summary>
    public PfsPage(Database database, PageAddress address)
        : base(database, address)
    {
        LoadPage();
    }

    private void LoadPage()
    {
        if (Header.PageType != PageType.Pfs)
        {
            throw new InvalidOperationException("Page type is not PFS");
        }

        PfsBytes = new List<PfsByte>();

        LoadPfsBytes();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PfsPage"/> class.
    /// </summary>
    public PfsPage(string connectionString, string database, PageAddress pageAddress)
        : base(connectionString, database, pageAddress)
    {
        LoadPage();
    }

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

    /// <summary>
    /// Gets or sets the PFS bytes collection.
    /// </summary>
    public List<PfsByte> PfsBytes { get; set; }

    /// <summary>
    /// Loads the PFS bytes collection
    /// </summary>
    private void LoadPfsBytes()
    {
        var pfsData = new byte[PfsSize];

        Array.Copy(PageData, PfsOffset, pfsData, 0, PfsSize);

        foreach (var pfsByte in pfsData)
        {
            PfsBytes.Add(new PfsByte(pfsByte));
        }
    }
}