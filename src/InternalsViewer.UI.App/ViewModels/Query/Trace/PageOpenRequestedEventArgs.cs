using System;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed class PageOpenRequestedEventArgs(PageAddress pageAddress, ushort? slot = null) : EventArgs
{
    public PageAddress PageAddress { get; } = pageAddress;

    public ushort? Slot { get; } = slot;
}
