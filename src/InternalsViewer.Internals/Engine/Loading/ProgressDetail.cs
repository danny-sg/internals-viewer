namespace InternalsViewer.Internals.Engine.Loading;

/// <summary>
/// A stage of a long-running load, and percentage complete
/// </summary>
/// <remarks>
/// The message identifies the stage rather than the update, so it stays the same while the percentage moves. A consumer can therefore show
/// one line per stage instead of one per update, however often the percentage is reported.
///
/// Percentage is null where a stage has no measurable extent.
/// </remarks>
public readonly record struct ProgressDetail(string Message, double? Percentage = null)
{
    public bool IsIndeterminate => Percentage is null;

    public override string ToString() => Percentage is null
                                         ? Message
                                         : $"{Message} {Percentage:N0}%";
}
