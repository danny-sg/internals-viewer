namespace InternalsViewer.Internals.Engine.Address;

public readonly record struct LogSequenceNumber(int VirtualLogFile, int FileOffset, short RecordSequence)
{
    public const int Size = sizeof(int) + sizeof(int) + sizeof(short);

    public override string ToString() => $"({VirtualLogFile}:{FileOffset}:{RecordSequence})";

    public string ToBinaryString()
    {
        return $"{VirtualLogFile:X8}:{FileOffset:X8}:{RecordSequence:X4}";
    }

    public decimal ToDecimal()
    {
        return decimal.Parse($"{VirtualLogFile}{FileOffset:0000000000}{RecordSequence:00000}");
    }

    public decimal ToDecimalFileOffsetOnly()
    {
        return decimal.Parse($"{VirtualLogFile}{FileOffset:0000000000}");
    }
}