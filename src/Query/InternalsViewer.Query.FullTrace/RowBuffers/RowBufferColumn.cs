namespace InternalsViewer.Query.FullTrace.RowBuffers;

public sealed record RowBufferColumn(int Ordinal,
                                     byte Status,
                                     RowBufferColumnFlags Flags,
                                     int Length,
                                     int RecordOffset,
                                     uint BindingFlags,
                                     int FixedLength,
                                     byte[] Data)
{
    public RowBufferPage? Page { get; init; }

    public bool IsByReference => Flags.HasFlag(RowBufferColumnFlags.ByReference);

    public bool IsTruncated => Flags.HasFlag(RowBufferColumnFlags.Truncated);

    public bool IsUnreadable => Flags.HasFlag(RowBufferColumnFlags.Unreadable);

    public override string ToString()
    {
        if (Status != 0)
        {
            return $"Status {Status}";
        }

        if (Page is { } page)
        {
            return page.ToString();
        }

        if (IsUnreadable)
        {
            return Data.Length == 0
                ? $"Unreadable ({GetUnreadableReason()})"
                : $"{FormatHex(Data)} (Unreadable After {Data.Length:N0} Bytes)";
        }

        return IsTruncated ? $"{FormatHex(Data)} (Truncated, {Length:N0} Bytes)" : FormatHex(Data);
    }

    private string GetUnreadableReason()
    {
        if (Flags.HasFlag(RowBufferColumnFlags.AlternateBuffer))
        {
            return "Alternate Buffer";
        }

        if (Flags.HasFlag(RowBufferColumnFlags.BindingUnreadable))
        {
            return "Binding Not In Trace";
        }

        return Flags.HasFlag(RowBufferColumnFlags.SlotUnreadable) ? "Buffer Not In Trace" : "Value Not In Trace";
    }

    private static string FormatHex(byte[] data) => data.Length == 0 ? "Empty" : $"0x{Convert.ToHexString(data)}";
}
