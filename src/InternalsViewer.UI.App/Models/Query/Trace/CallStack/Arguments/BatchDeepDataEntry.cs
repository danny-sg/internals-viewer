namespace InternalsViewer.UI.App.Models.Query.Trace.CallStack.Arguments;

public sealed class BatchDeepDataEntry
{
    public required int VectorId { get; set; }

    public required int RowIndex { get; set; }

    public required ulong Address { get; set; }

    public required int Length { get; set; }

    public required string Data { get; set; }

    public required string Text { get; set; }

    public string AddressText => $"0x{Address:X16}";
}
