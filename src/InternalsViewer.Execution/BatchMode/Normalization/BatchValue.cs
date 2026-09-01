namespace InternalsViewer.Execution.BatchMode.Normalization;

/// <summary>
/// A single normalized 64 bit value in a batch mode vector
/// </summary>
public readonly record struct BatchValue(long Value)
{
    public bool IsNull => Value == 1;

    public bool IsDeepDataReference => Value != 1 && (Value & 1) == 1;
}
