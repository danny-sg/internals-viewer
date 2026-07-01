namespace InternalsViewer.Internals.Converters.Decoder;

public sealed record DecodeResult(string DataType, string Value)
{
    public string DataType { get; set; } = DataType;

    public string Value { get; set; } = Value;
}
