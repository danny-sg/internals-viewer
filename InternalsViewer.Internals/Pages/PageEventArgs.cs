using System;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Internals.Pages;

/// <summary>
/// Event data for page related events
/// </summary>
public class PageEventArgs : EventArgs
{
    private RowIdentifier rowId;

    public PageEventArgs(RowIdentifier address, bool openInNewWindow)
    {
        RowId = address;
        OpenInNewWindow = openInNewWindow;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the event triggers a new window to be opened
    /// </summary>
    /// <value><c>true</c> if [open in new window]; otherwise, <c>false</c>.</value>
    public bool OpenInNewWindow { get; set; }

    /// <summary>
    /// Gets or sets the row id.
    /// </summary>
    public RowIdentifier RowId
    {
        get => rowId;
        set => rowId = value;
    }

    /// <summary>
    /// Gets or sets the page address.
    /// </summary>
    public PageAddress Address
    {
        get => rowId.PageAddress;
        set => rowId.PageAddress = value;
    }
}