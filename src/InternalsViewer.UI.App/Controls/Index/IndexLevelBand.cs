namespace InternalsViewer.UI.App.Controls.Index;

/// <summary>
/// Band drawn behind one level of the index tree
/// </summary>
public sealed record IndexLevelBand(int Depth, byte Level, string Name);
