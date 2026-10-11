namespace InternalsViewer.Query.Plans.Model;

/// <summary>
/// What a TOP operator limits its input to
/// </summary>
public class TopInfo
{
    /// <summary>
    /// Rows the operator asks for, or null when the count is not a constant it could be read from
    /// </summary>
    public long? RowCount { get; init; }

    public bool IsPercent { get; init; }

    /// <summary>
    /// Rows equal to the last one are returned as well, so the count is a lower bound rather than the number returned
    /// </summary>
    public bool WithTies { get; init; }
}
