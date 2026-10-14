using System.Collections.Generic;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public static class SourcePath
{
    public static IReadOnlyList<SourceStep> Build(CallSignaturePart value,
                                                  PlayheadCallRow call,
                                                  IReadOnlyList<PlayheadCallRow> callers,
                                                  CallStackNode? originNode,
                                                  int originCall = -1)
    {
        if (value.Value is not { } address)
        {
            return [];
        }

        var origin = value.Origin;

        var creation = origin is { Operation: not MemoryMapOperation.Visited } ? origin : null;

        if (creation is not null && creation.Call == call.Id)
        {
            return [];
        }

        List<SourceStep> steps = [];

        foreach (var caller in callers)
        {
            if (!Receives(caller, address))
            {
                continue;
            }

            if (creation is not null && caller.Id < creation.Call)
            {
                break;
            }

            var isCreation = creation is not null && caller.Id == creation.Call;

            steps.Add(new SourceStep(caller.Node,
                                     caller.Id,
                                     steps.Count,
                                     isCreation ? creation!.Operation : null,
                                     isOnStack: true,
                                     caller.Call));

            if (isCreation)
            {
                return steps;
            }
        }

        var latest = steps.Count > 0 ? steps[^1].Id : call.Id;

        if (origin is not null && originNode is not null && origin.Call < latest)
        {
            steps.Add(new SourceStep(originNode, origin.Call, steps.Count, creation?.Operation, call: originCall));
        }

        return steps;
    }

    private static bool Receives(PlayheadCallRow row, ulong value)
        => row.This?.Value == value || row.Signature.Any(p => p.Kind == CallSignaturePartKind.Value && p.Value == value);
}
