namespace InternalsViewer.Query.Export;

public interface IQueryExportSection
{
    bool CanExport(QueryExportContent content);

    void Write(QueryExportWriter writer, QueryExportContent content);

    IRunExtra? Read(QueryExportReader reader);
}
