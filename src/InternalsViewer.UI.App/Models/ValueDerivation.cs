using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models;

/// <summary>
/// How a stored value becomes the value it stands for, as a source, the steps applied and the result
/// </summary>
/// <remarks>
/// Compression rarely stores a value as it is read back, and the metadata it is reconstructed from usually sits
/// somewhere else entirely. Carrying the derivation lets a grid show the working rather than only the answer.
///
/// The members are settable rather than required because the type backs a dependency property, and the generated
/// XAML type info constructs one without arguments.
/// </remarks>
public sealed class ValueDerivation
{
    public IReadOnlyList<DerivationStep> Steps { get; init; } = [];

    public string Result { get; init; } = string.Empty;
}

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
