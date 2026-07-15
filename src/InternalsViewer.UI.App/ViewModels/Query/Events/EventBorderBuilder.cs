using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.UI.App.Controls.Timeline;
using InternalsViewer.UI.App.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace InternalsViewer.UI.App.ViewModels.Query.Events;

internal static class EventBorderBuilder
{
    internal static IReadOnlyList<AllocationBorder> GetLockBorders(IReadOnlyList<LockEvent> locks, DatabaseSource databaseSource)
    {
        if (locks.Count == 0)
        {
            return [];
        }

        var borders = new List<AllocationBorder>();

        AddPageLockBorders(locks, borders);
        AddObjectLockBorders(locks, borders, databaseSource);

        return borders;
    }

    private static void AddPageLockBorders(IReadOnlyList<LockEvent> locks, List<AllocationBorder> borders)
    {
        var groups = locks.Where(l => l.PageAddress is { FileId: > 0 } p)
                          .GroupBy(l => (l.PageAddress!.Value.FileId,
                                         Category: LockModeClassifier.Categorise(l.LockMode),
                                         l.ObjectId,
                                         Intent: IsIntentLock(l.LockMode)));

        foreach (var group in groups)
        {
            var cells = group.Select(l => TimedRangeFor(l.PageAddress!.Value.PageId, l.PageAddress!.Value.PageId, l))
                             .ToList();

            borders.Add(new AllocationBorder(AllocationBorderScope.Page,
                                             group.Key.FileId,
                                             LockCategoryColour(group.First().LockMode, group.Key.Intent),
                                             cells));
        }
    }

    private static void AddObjectLockBorders(IReadOnlyList<LockEvent> locks,
                                             List<AllocationBorder> borders,
                                             DatabaseSource databaseSource)
    {
        var groups = locks.Where(l => l.Resource.ResourceType is LockResourceType.Object or LockResourceType.Hobt
                                      && l.TableName.Length > 0)
                          .GroupBy(l => (l.SchemaName, l.TableName,
                                         Category: LockModeClassifier.Categorise(l.LockMode),
                                         Intent: IsIntentLock(l.LockMode)));

        foreach (var group in groups)
        {
            var objectName = $"{group.Key.SchemaName}.{group.Key.TableName}";

            var chains = databaseSource.AllocationUnits
                                       .Where(au => string.Equals($"{au.Value.SchemaName}.{au.Value.TableName}",
                                                                   objectName,
                                                                   StringComparison.OrdinalIgnoreCase))
                                     .Select(layer => layer.Value.IamChain)
                                     .ToList();

            if (chains.Count == 0)
            {
                continue;
            }

            var colour = LockCategoryColour(group.First().LockMode, group.Key.Intent);

            foreach (var file in databaseSource.Files)
            {
                var ranges = chains.SelectMany(c => c.GetAllocatedPageRanges(file.FileId)).ToList();

                if (ranges.Count == 0)
                {
                    continue;
                }

                var cells = group.SelectMany(l => ranges.Select(r => TimedRangeFor(r.From, r.To, l))).ToList();

                borders.Add(new AllocationBorder(AllocationBorderScope.Page, file.FileId, colour, cells));
            }
        }
    }

    private const long MinLockBorderDurationUs = 1000;

    private static bool IsIntentLock(LockMode mode) => LockModeClassifier.IsIntent(mode);

    // How far an intent lock's colour is blended towards white — enough to read as the same hue, a shade lighter.
    private const double IntentLightenAmount = 0.5;

    // The category colour, lightened for intent modes: an intent lock only flags finer locks below the resource, so it
    // reads as a paler shade of the real S/U/X lock's colour rather than a distinct one.
    private static Color LockCategoryColour(LockMode mode, bool isIntent)
    {
        var colour = TimelineColours.LockModeColour(mode);

        var category = Color.FromArgb(colour.Alpha, colour.Red, colour.Green, colour.Blue);

        return isIntent ? Lighten(category, IntentLightenAmount) : category;
    }

    private static Color Lighten(Color colour, double amount) =>
        Color.FromArgb(colour.A,
                       colour.R + (int)((255 - colour.R) * amount),
                       colour.G + (int)((255 - colour.G) * amount),
                       colour.B + (int)((255 - colour.B) * amount));

    private static TimedRange TimedRangeFor(int fromCell, int toCell, LockEvent lockEvent) =>
        new(fromCell, toCell, lockEvent.TimeUs, lockEvent.TimeUs + Math.Max(lockEvent.DurationUs, MinLockBorderDurationUs));
}