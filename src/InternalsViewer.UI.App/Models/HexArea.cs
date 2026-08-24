namespace InternalsViewer.UI.App.Models;

/// <summary>
/// A named stretch of the data the hex view is showing, running until the next one starts
/// </summary>
public sealed record HexArea(string Name, int Start);
