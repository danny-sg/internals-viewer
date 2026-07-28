using System.Collections.Immutable;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Text;
using InternalsViewer.Internals.Metadata.Structures;

namespace InternalsViewer.Internals.DataAccess.AccessPaths.Search;

public static class SeekStrategyBuilder
{
    public static SeekStrategy Build(IndexStructure indexStructure, SeekBounds bounds, ScanDirection direction, long? rowGoal)
    {
        var forward = direction == ScanDirection.Forward;

        var entryTarget = forward ? bounds.StartValue : bounds.EndValue;
        var entryInclusive = forward ? bounds.IsStartInclusive : bounds.IsEndInclusive;

        var exitTarget = forward ? bounds.EndValue : bounds.StartValue;
        var exitInclusive = forward ? bounds.IsEndInclusive : bounds.IsStartInclusive;

        var phases = ImmutableArray.CreateBuilder<SeekStrategyPhase>();

        phases.Add(BuildDescent(bounds, entryTarget, entryInclusive, forward));
        phases.Add(BuildPosition(bounds, entryTarget, entryInclusive, forward));
        phases.Add(BuildWalk(bounds, exitTarget, exitInclusive, forward));
        phases.Add(BuildComplete(exitTarget, rowGoal));

        var rowGoalReason = rowGoal == 1
            ? "The index is unique and the seek fixes every key column with an equality, so at most one row can match. " +
              "The walk stops after the first match instead of reading on to check."
            : null;

        return new SeekStrategy
        {
            Phases = phases.ToImmutable(),
            RowGoal = rowGoal,
            RowGoalReason = rowGoalReason,
            Bounds = bounds,
            Direction = direction
        };
    }

    private static SeekStrategyPhase BuildDescent(SeekBounds bounds, in AccessKey target, bool inclusive, bool forward)
    {
        if (target.IsUnbounded)
        {
            return new SeekStrategyPhase
            {
                Phase = SeekPhase.Descent,
                Title = "Descent",
                Lead = forward
                    ? "From the root, follow the first down page pointer on each level down to the leaf"
                    : "From the root, follow the last down page pointer on each level down to the leaf"
            };
        }

        var symbol = forward
            ? (inclusive ? "<" : "<=")
            : (inclusive ? "<=" : "<");

        return new SeekStrategyPhase
        {
            Phase = SeekPhase.Descent,
            Title = "Descent",
            Lead = "From the root, binary search for the child with the highest separator where ",
            Condition = Comparison(symbol, target, GetWidth(bounds, target)),
            Trail = " and follow its down page pointer, repeating on each level down to the leaf"
        };
    }

    private static SeekStrategyPhase BuildPosition(SeekBounds bounds, in AccessKey target, bool inclusive, bool forward)
    {
        if (target.IsUnbounded)
        {
            return new SeekStrategyPhase
            {
                Phase = SeekPhase.Position,
                Title = "Position",
                Lead = forward
                    ? "Start at the first slot on the leaf page"
                    : "Start at the last slot on the leaf page"
            };
        }

        var symbol = forward
            ? (inclusive ? ">=" : ">")
            : (inclusive ? "<=" : "<");

        return new SeekStrategyPhase
        {
            Phase = SeekPhase.Position,
            Title = "Position",
            Lead = forward
                ? "Binary search the leaf page for the lowest key where "
                : "Binary search the leaf page for the highest key where ",
            Condition = Comparison(symbol, target, GetWidth(bounds, target))
        };
    }

    private static SeekStrategyPhase BuildWalk(SeekBounds bounds, in AccessKey target, bool inclusive, bool forward)
    {
        if (target.IsUnbounded)
        {
            return new SeekStrategyPhase
            {
                Phase = SeekPhase.Walk,
                Title = "Walk",
                Lead = forward
                    ? "Read forward to the end of the index, outputting matching rows and following leaf page links"
                    : "Read backward to the start of the index, outputting matching rows"
            };
        }

        var symbol = forward
            ? (inclusive ? ">" : ">=")
            : (inclusive ? "<" : "<=");

        return new SeekStrategyPhase
        {
            Phase = SeekPhase.Walk,
            Title = "Walk",
            Lead = forward
                ? "Read forward, outputting matching rows, until a row with "
                : "Read backward, outputting matching rows, until a row with ",
            Condition = Comparison(symbol, target, GetWidth(bounds, target)),
            Trail = forward
                ? " ends the range, following leaf page links across pages"
                : " ends the range"
        };
    }

    private static SeekStrategyPhase BuildComplete(in AccessKey exitTarget, long? rowGoal)
    {
        var lead = rowGoal == 1
            ? "Stop after the first matching row (row goal 1)"
            : exitTarget.IsUnbounded
                ? "Stop at the end of the index"
                : "Stop when a key leaves the range";

        return new SeekStrategyPhase
        {
            Phase = SeekPhase.Complete,
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
            tokens.Add(new PredicateToken(PredicateTokenKind.Punctuation, "("));
        }

        for (var index = 0; index < length; index++)
        {
            if (index > 0)
            {
                tokens.Add(new PredicateToken(PredicateTokenKind.Punctuation, ","));
                tokens.Add(new PredicateToken(PredicateTokenKind.Space, " "));
            }

            tokens.Add(new PredicateToken(PredicateTokenKind.Column, target[index].ColumnName ?? $"Key{index + 1}"));
        }

        if (isComposite)
        {
            tokens.Add(new PredicateToken(PredicateTokenKind.Punctuation, ")"));
        }

        tokens.Add(new PredicateToken(PredicateTokenKind.Space, " "));
        tokens.Add(new PredicateToken(PredicateTokenKind.Operator, symbol));
        tokens.Add(new PredicateToken(PredicateTokenKind.Space, " "));

        PredicateWriter.WriteKeyValues(tokens, target, length);

        return tokens.ToImmutable();
    }

    private static int GetWidth(SeekBounds bounds, in AccessKey target)
    {
        return bounds.CompareWidth == int.MaxValue ? target.Count : bounds.CompareWidth;
    }
}
