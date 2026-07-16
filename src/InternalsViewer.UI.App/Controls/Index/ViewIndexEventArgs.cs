using System;

namespace InternalsViewer.UI.App.Controls.Index;

public class ViewIndexEventArgs(long allocationUnitId) : EventArgs
{
    public long AllocationUnitId { get; } = allocationUnitId;
}