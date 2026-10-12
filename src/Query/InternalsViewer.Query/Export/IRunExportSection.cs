namespace InternalsViewer.Query.Export;

public interface IQueryExportSection
{
    bool CanExport(RunExportContent content);

    void Collect(QueryExportCollector collector, RunExportContent content);

    void Write(RunExportWriter writer, RunExportContent content);

    IRunExtra? Read(RunExportReader reader);
}
