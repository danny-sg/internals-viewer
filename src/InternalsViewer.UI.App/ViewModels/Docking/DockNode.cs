using System.Collections.Generic;

namespace InternalsViewer.UI.App.ViewModels.Docking;

/// <summary>
/// Serializable snapshot of a dock layout node (group or split), referencing documents by key
/// </summary>
public sealed class DockNode
{
    public bool IsSplit { get; set; }

    // Group
    public List<string> Documents { get; set; } = [];

    public string? Selected { get; set; }

    // Split
    public int Orientation { get; set; }

    public double FirstStar { get; set; } = 1;

    public double SecondStar { get; set; } = 1;

    public DockNode? First { get; set; }

    public DockNode? Second { get; set; }
}