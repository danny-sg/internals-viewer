namespace InternalsViewer.Query.Export;

public interface IRunExportSection
{
    bool CanExport(RunExportContent content);

    void Collect(RunExportCollector collector, RunExportContent content);

    void Write(RunExportWriter writer, RunExportContent content);

    IRunExtra? Read(RunExportReader reader);
}
