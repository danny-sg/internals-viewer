using System;
using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.UI.App.ViewModels.Index;

public sealed class PageNavigatedEventArgs(PageAddress pageAddress, bool isReset) : EventArgs
{
    public PageAddress PageAddress { get; } = pageAddress;

    public bool IsReset { get; } = isReset;
}
