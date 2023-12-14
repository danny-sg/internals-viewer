using System;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.UI;

/// <summary>
/// Event data for page related events
/// </summary>
public class PageEventArgs : EventArgs
{
    public RowIdentifier RowId { get; } = new();

    public PageEventArgs(RowIdentifier address, bool openInNewWindow)
    {
        RowId = address;
        OpenInNewWindow = openInNewWindow;
    }

    public PageEventArgs(PageAddress address, bool openInNewWindow)
    {
        RowId = new RowIdentifier { PageAddress = address };
        OpenInNewWindow = openInNewWindow;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the event triggers a new window to be opened
    /// </summary>
    /// <value><c>true</c> if [open in new window]; otherwise, <c>false</c>.</value>
    public bool OpenInNewWindow { get; set; }

    /// <summary>
    /// Gets or sets the page address.
    /// </summary>
    public PageAddress Address => RowId.PageAddress;
}