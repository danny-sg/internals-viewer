namespace InternalsViewer.UI.App.Models.Query.Trace.Batch;

/// <summary>
/// One line of the slot detail, being a name and what the slot decodes to under it
/// </summary>
public sealed class BatchDetailItem
{
    public required string Name { get; set; }

    public required string Value { get; set; }

    public bool IsMonospaced { get; set; }
}
