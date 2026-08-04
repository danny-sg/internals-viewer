using System.Collections.Immutable;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Execution.AccessPaths.Search;

public static class AccessStrategyBuilder
{
    public static AccessStrategy Build(IndexStructure indexStructure,
                                       SeekBounds bounds,
                                       ScanDirection direction,
                                       long? rowGoal,
                                       AccessPredicate? residual = null,
                                       string? rowGoalReason = null,
                                       IReadOnlyList<SeekBounds>? ranges = null)
    {
        var forward = direction == ScanDirection.Forward;

        var entryTarget = forward ? bounds.StartValue : bounds.EndValue;
        var entryInclusive = forward ? bounds.IsStartInclusive : bounds.IsEndInclusive;

        var exitTarget = forward ? bounds.EndValue : bounds.StartValue;
        var exitInclusive = forward ? bounds.IsEndInclusive : bounds.IsStartInclusive;

        var hasResidual = residual is not (null or AccessPredicate.True or AccessPredicate.NoTranslation);

        var rangeCount = ranges?.Count ?? 1;

        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        if (ranges is { Count: > 1 })
        {
            phases.Add(BuildRanges(ranges));
        }

        phases.Add(BuildDescent(bounds, entryTarget, entryInclusive, forward));
        phases.Add(BuildPosition(bounds, entryTarget, entryInclusive, forward));
        phases.Add(BuildWalk(bounds, exitTarget, exitInclusive, forward, hasResidual ? residual : null));
        phases.Add(BuildComplete(exitTarget, rowGoal, rangeCount));

        rowGoalReason ??= rowGoal == 1
            ? "The index is unique and the seek fixes every key column with an equality, so at most one row can match. " +
              "The walk stops after the first match instead of reading on to check."
            : null;

        return new AccessStrategy
        {
            Phases = phases.ToImmutable(),
            RowGoal = rowGoal,
            RowGoalReason = rowGoalReason,
            Bounds = bounds,
            Direction = direction,
            Residual = residual is AccessPredicate.True or AccessPredicate.NoTranslation ? null : residual,
            HasUntranslatedResidual = HasNoTranslation(residual),
            RangeCount = rangeCount,
            Ranges = ranges ?? [bounds],
            KeyColumns = GetKeyColumns(indexStructure),
            IsUnique = indexStructure.IsUnique
        };
    }

    public static AccessStrategy BuildAllocationScan(AccessPredicate? residual,
                                                     long? rowGoal,
                                                     string? rowGoalReason = null)
    {
        var hasResidual = residual is not (null or AccessPredicate.True or AccessPredicate.NoTranslation);

        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Allocation,
            Title = "IAM",
            Lead = "Read the first IAM page for the allocation unit. Each IAM page maps which extents in a 4GB interval of one file " +
                   "belong to the allocation unit, with eight single page slots for pages allocated from mixed extents"
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Allocation,
            Title = "Allocation",
            Lead = "Visit the single page slots, then each allocated extent in page number order. The PFS byte for a page is checked " +
                   "before the page is read, skipping pages that sit in an allocated extent but are not themselves in use. Index and " +
                   "IAM pages sharing the allocation unit's extents are identified by their page header and skipped"
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Walk,
            Title = "Walk",
            Lead = hasResidual
                ? "Read every row on each data page, emitting rows where "
                : "Read every row on each data page, emitting all rows",
            Condition = hasResidual ? PredicateWriter.Write(residual!) : []
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = rowGoal is { } goal
                ? $"The scan ends when {goal:N0} rows have been output or the end of the IAM chain is reached"
                : "The scan ends when the end of the IAM chain is reached, following the chain across intervals and files"
        });

        return new AccessStrategy
        {
            Phases = phases.ToImmutable(),
            RowGoal = rowGoal,
            RowGoalReason = rowGoalReason,
            Bounds = SeekBounds.All,
            Direction = ScanDirection.Forward,
            Residual = residual is AccessPredicate.True or AccessPredicate.NoTranslation ? null : residual,
            HasUntranslatedResidual = HasNoTranslation(residual),
            RangeCount = 1,
            Ranges = [SeekBounds.All]
        };
    }

    /// <summary>
    /// Builds the strategy for fetching one heap row from its row identifier
    /// </summary>
    public static AccessStrategy BuildHeapFetch(AccessPredicate? residual)
    {
        var hasResidual = residual is not (null or AccessPredicate.True or AccessPredicate.NoTranslation);

        var phases = ImmutableArray.CreateBuilder<AccessStrategyPhase>();

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Descent,
            Title = "Fetch",
            Lead = "A heap has no tree to descend. The row identifier names the file, page and slot outright, so the row is reached " +
                   "with a single page read"
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Walk,
            Title = "Row",
            Lead = hasResidual
                ? "Read the row at the slot, returning it where "
                : "Read the row at the slot and return it",
            Condition = hasResidual ? PredicateWriter.Write(residual!) : []
        });

        phases.Add(new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = "The fetch ends at that row. A slot holding a forwarding stub costs one further read, because the row has outgrown " +
                   "its page and moved, leaving the stub so the row identifier stays valid"
        });

        return new AccessStrategy
        {
            Phases = phases.ToImmutable(),
            Bounds = SeekBounds.All,
            Direction = ScanDirection.Forward,
            Residual = residual is AccessPredicate.True or AccessPredicate.NoTranslation ? null : residual,
            HasUntranslatedResidual = HasNoTranslation(residual),
            RangeCount = 1,
            Ranges = [SeekBounds.All]
        };
    }

    public static IReadOnlyList<string> GetKeyColumns(IndexStructure indexStructure)
    {
        return [.. indexStructure.IndexKeyColumns.Select(k => k.IsDescending ? $"{k.ColumnName} DESC" : k.ColumnName)];
    }

    private static AccessStrategyPhase BuildRanges(IReadOnlyList<SeekBounds> ranges)
    {
        var tokens = ImmutableArray.CreateBuilder<PredicateToken>();

        for (var index = 0; index < ranges.Count; index++)
        {
            if (index > 0)
            {
                tokens.Add(new PredicateToken(PredicateTokenType.Space, " "));
                tokens.Add(new PredicateToken(PredicateTokenType.Keyword, "OR"));
                tokens.Add(new PredicateToken(PredicateTokenType.Space, " "));
            }

            tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, "("));

            tokens.AddRange(PredicateWriter.Write(ranges[index]));

            tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, ")"));
        }

        return new AccessStrategyPhase
        {
            Phase = AccessPhase.Ranges,
            Title = "Ranges",
            Lead = $"The seek makes {ranges.Count} passes, one per range: ",
            Condition = tokens.ToImmutable(),
            Trail = ". Each pass repeats the steps below with its own range"
        };
    }

    private static AccessStrategyPhase BuildDescent(SeekBounds bounds, in AccessKey target, bool inclusive, bool forward)
    {
        if (target.IsUnbounded)
        {
            return new AccessStrategyPhase
            {
                Phase = AccessPhase.Descent,
                Title = "Descent",
                Lead = forward
                    ? "From the root, follow the first down page pointer on each level down to the leaf"
                    : "From the root, follow the last down page pointer on each level down to the leaf"
            };
        }

        var symbol = forward
            ? (inclusive ? "<" : "<=")
            : (inclusive ? "<=" : "<");

        return new AccessStrategyPhase
        {
            Phase = AccessPhase.Descent,
            Title = "Descent",
            Lead = "From the root, binary search for the child with the highest separator where ",
            Condition = Comparison(symbol, target, GetWidth(bounds, target)),
            Trail = " and follow its down page pointer, repeating on each level down to the leaf"
        };
    }

    private static AccessStrategyPhase BuildPosition(SeekBounds bounds, in AccessKey target, bool inclusive, bool forward)
    {
        if (target.IsUnbounded)
        {
            return new AccessStrategyPhase
            {
                Phase = AccessPhase.Position,
                Title = "Position",
                Lead = forward
                    ? "Start at the first slot on the leaf page"
                    : "Start at the last slot on the leaf page"
            };
        }

        var symbol = forward
            ? (inclusive ? ">=" : ">")
            : (inclusive ? "<=" : "<");

        return new AccessStrategyPhase
        {
            Phase = AccessPhase.Position,
            Title = "Position",
            Lead = forward
                ? "Binary search the leaf page for the lowest key where "
                : "Binary search the leaf page for the highest key where ",
            Condition = Comparison(symbol, target, GetWidth(bounds, target))
        };
    }

    private static AccessStrategyPhase BuildWalk(SeekBounds bounds, in AccessKey target, bool inclusive, bool forward, AccessPredicate? residual)
    {
        var output = residual is not null
            ? "outputting rows that pass the residual predicate "
            : "outputting matching rows";

        ImmutableArray<PredicateToken> residualTokens = residual is null ? [] : PredicateWriter.Write(residual);

        if (target.IsUnbounded)
        {
            return new AccessStrategyPhase
            {
                Phase = AccessPhase.Walk,
                Title = "Walk",
                Lead = forward
                    ? $"Read forward to the end of the index, {output}"
                    : $"Read backward to the start of the index, {output}",
                LeadCondition = residualTokens,
                Middle = forward ? " and following leaf page links" : string.Empty
            };
        }

        var symbol = forward
            ? (inclusive ? ">" : ">=")
            : (inclusive ? "<" : "<=");

        return new AccessStrategyPhase
        {
            Phase = AccessPhase.Walk,
            Title = "Walk",
            Lead = forward
                ? $"Read forward, {output}"
                : $"Read backward, {output}",
            LeadCondition = residualTokens,
            Middle = ", until a row with ",
            Condition = Comparison(symbol, target, GetWidth(bounds, target)),
            Trail = forward
                ? " ends the range, following leaf page links across pages"
                : " ends the range"
        };
    }

    private static AccessStrategyPhase BuildComplete(in AccessKey exitTarget, long? rowGoal, int rangeCount)
    {
        var lead = rowGoal switch
        {
            1 => "Stop after the first matching row (row goal 1)",
            not null => $"Stop after {rowGoal:N0} matching rows (row goal {rowGoal:N0})",
            _ => exitTarget.IsUnbounded
                ? "Stop at the end of the index"
                : rangeCount > 1
                    ? $"Stop when a key leaves the range, then seek again from the root for the next of the {rangeCount} ranges"
                    : "Stop when a key leaves the range"
        };

        return new AccessStrategyPhase
        {
            Phase = AccessPhase.Complete,
            Title = "Complete",
            Lead = lead
        };
    }

    private static ImmutableArray<PredicateToken> Comparison(string symbol, in AccessKey target, int width)
    {
        var length = Math.Min(width, target.Count);

        if (length == 0)
        {
            return [];
        }

        var tokens = ImmutableArray.CreateBuilder<PredicateToken>();

        var isComposite = length > 1;

        if (isComposite)
        {
            tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, "("));
        }

        for (var index = 0; index < length; index++)
        {
            if (index > 0)
            {
                tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, ","));
                tokens.Add(new PredicateToken(PredicateTokenType.Space, " "));
            }

            tokens.Add(new PredicateToken(PredicateTokenType.Column, target[index].ColumnName ?? $"Key{index + 1}"));
        }

        if (isComposite)
        {
            tokens.Add(new PredicateToken(PredicateTokenType.Punctuation, ")"));
        }

        tokens.Add(new PredicateToken(PredicateTokenType.Space, " "));
        tokens.Add(new PredicateToken(PredicateTokenType.Operator, symbol));
        tokens.Add(new PredicateToken(PredicateTokenType.Space, " "));

        PredicateWriter.WriteKeyValues(tokens, target, length);

        return tokens.ToImmutable();
    }

    private static int GetWidth(SeekBounds bounds, in AccessKey target)
    {
        return bounds.CompareWidth == int.MaxValue ? target.Count : bounds.CompareWidth;
    }

    private static bool HasNoTranslation(AccessPredicate? predicate)
        => predicate switch
        {
            null => false,
            AccessPredicate.NoTranslation => true,
            AccessPredicate.And and => and.Predicates.Any(HasNoTranslation),
            AccessPredicate.Or or => or.Predicates.Any(HasNoTranslation),
            AccessPredicate.Not not => HasNoTranslation(not.Predicate),
            _ => false
        };
}
