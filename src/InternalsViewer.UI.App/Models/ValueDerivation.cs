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