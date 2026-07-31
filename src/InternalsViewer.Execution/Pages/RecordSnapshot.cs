using InternalsViewer.Internals.Interfaces.Engine;

namespace InternalsViewer.Execution.Pages;

internal static class RecordSnapshot
{
    public static IRecord Detach(IRecord record)
    {
        foreach (var field in record.Fields)
        {
            field.Data = field.Data.ToArray();
        }

        return record;
    }
}
