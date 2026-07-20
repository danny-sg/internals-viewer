using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.Interfaces.Events;

namespace InternalsViewer.Query.Events.Splits;

/// <summary>
/// Page split event parser
/// </summary>
internal class PageSplitEventParser : IEventParser<PageSplitEvent>
{
    public static PageSplitEvent Map(DatabaseSource? databaseSource, EventResult e)
    {
        var newPageId = e.GetLong("new_page_page_id") ?? 0;

        return new PageSplitEvent
        {
            Name = e.Name,
            Timestamp = e.Timestamp,
            DatabaseId = e.GetDatabaseId(),
            PageAddress = new PageAddress((short)(e.GetInt("file_id") ?? 0), (int)(e.GetLong("page_id") ?? 0)),
            NewPage = newPageId > 0
                ? new PageAddress((short)(e.GetInt("new_page_file_id") ?? 0), (int)newPageId)
                : null,
            RowsetId = e.GetLong("rowset_id") ?? 0,
            SplitOperation = (PageSplitOperation)(e.GetInt("splitOperation") ?? 0)
        };
    }
}
