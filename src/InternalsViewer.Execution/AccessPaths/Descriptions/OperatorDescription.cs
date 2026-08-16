using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Text;

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

    /// <summary>
    /// Compares what a description says rather than the arrays it says it in
    /// </summary>
    /// <remarks>
    /// A description is rebuilt whenever the strategy behind it is replaced, which a correlated seek does on every rebind. The record
    /// equality a phase array gives compares the array itself, so each rebuild would look like a change and the panel showing it would be
    /// built again. Two descriptions reading the same are the same as far as anything watching one is concerned.
    /// </remarks>
    public bool Equals(OperatorDescription? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Summary != other.Summary || IsStreaming != other.IsStreaming || IsBlocking != other.IsBlocking)
        {
            return false;
        }

        if (Phases.Length != other.Phases.Length)
        {
            return false;
        }

        for (var index = 0; index < Phases.Length; index++)
        {
            if (!SamePhase(Phases[index], other.Phases[index]))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode() => HashCode.Combine(Summary, IsStreaming, IsBlocking, Phases.Length);

    private static bool SamePhase(AccessStrategyPhase left, AccessStrategyPhase right)
        => left.Phase == right.Phase
           && left.Title == right.Title
           && left.Lead == right.Lead
           && left.Middle == right.Middle
           && left.Trail == right.Trail
           && SameTokens(left.LeadCondition, right.LeadCondition)
           && SameTokens(left.Condition, right.Condition);

    private static bool SameTokens(ImmutableArray<PredicateToken> left, ImmutableArray<PredicateToken> right)
    {
        if (left.IsDefaultOrEmpty && right.IsDefaultOrEmpty)
        {
            return true;
        }

        if (left.IsDefaultOrEmpty || right.IsDefaultOrEmpty || left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }
}
