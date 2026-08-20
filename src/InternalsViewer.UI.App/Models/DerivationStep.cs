namespace InternalsViewer.UI.App.Models;

/// <summary>
/// One operand in a derivation, named after the variable it came from so the source of the number is clear
/// </summary>
public sealed class DerivationStep
{
    /// <summary>
    /// Symbol joining this operand to the one before it, which the first operand has nothing to carry
    /// </summary>
    public string Operator { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Where the operand came from, for a caller that can navigate to it
    /// </summary>
    /// <remarks>
    /// Untyped because a derivation says nothing about what it is describing, and whatever raised it knows how to
    /// read its own target back.
    /// </remarks>
    public object? Target { get; init; }

    public bool HasOperator => Operator.Length > 0;

    public bool IsNavigable => Target is not null;
}