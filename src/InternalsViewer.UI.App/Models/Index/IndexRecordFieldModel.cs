using System.Data;

namespace InternalsViewer.UI.App.Models.Index;

public class IndexRecordFieldModel
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public SqlDbType DataType { get; set; }
}