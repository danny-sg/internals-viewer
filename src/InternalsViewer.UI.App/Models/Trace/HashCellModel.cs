namespace InternalsViewer.UI.App.Models.Trace;

/// <summary>
/// One cell of the hash table grid, taking its width from the column it sits under
/// </summary>
public sealed class HashCellModel
{
    public string Value { get; init; } = string.Empty;

    public HashColumnModel Column { get; init; } = new();
}
