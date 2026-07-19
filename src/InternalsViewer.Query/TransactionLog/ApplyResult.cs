namespace InternalsViewer.Query.TransactionLog;

/// <summary>
/// Outcome of applying a log record to a page image
/// </summary>
/// <remarks>
/// Any status other than Applied means the page image was left untouched by that record - a failed apply never
/// partially mutates the page, so a replay can stop at the first failure with the image still valid for every
/// record applied before it.
/// </remarks>
public sealed record ApplyResult(ApplyStatus Status, string Message = "")
{
    public static readonly ApplyResult Success = new(ApplyStatus.Applied);

    public bool IsApplied => Status == ApplyStatus.Applied;
}
