using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Descriptions;

/// <summary>
/// What one operator does, in the terms a reader watching it run needs
/// </summary>
/// <remarks>
/// The phases are the same shape an access path uses, so a seek's descent and a hash match's build are described and lit the same way.
/// </remarks>
public sealed record OperatorDescription
{
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Rows leave the operator while its input is still being read
    /// </summary>
    public bool IsStreaming { get; init; }

    /// <summary>
    /// An input has to be read to its end before a row can leave the operator
    /// </summary>
    public bool IsBlocking { get; init; }

    public ImmutableArray<AccessStrategyPhase> Phases { get; init; } = [];
}
