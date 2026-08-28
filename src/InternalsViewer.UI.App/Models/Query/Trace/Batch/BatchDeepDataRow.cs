namespace InternalsViewer.UI.App.Models.Query.Trace.Batch;

/// <summary>
/// One value in the batch deep data arena
/// </summary>
public sealed class BatchDeepDataRow
{
    public required int Index { get; set; }

    public required long Address { get; set; }

    public required int Length { get; set; }

    public required string Data { get; set; }

    public string AddressText => $"0x{Address:X16}";
}
