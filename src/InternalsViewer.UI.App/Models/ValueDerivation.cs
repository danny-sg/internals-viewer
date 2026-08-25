using System.Collections.Generic;

namespace InternalsViewer.UI.App.Models;

/// <summary>
/// Steps to describe how a result was derived
/// </summary>
public sealed class ValueDerivation
{
    public IReadOnlyList<DerivationStep> Steps { get; init; } = [];

    public string Result { get; init; } = string.Empty;

    public object? Target { get; init; }

    public bool IsNavigable => Target is not null;
}