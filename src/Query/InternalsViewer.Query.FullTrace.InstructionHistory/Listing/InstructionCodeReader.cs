using Iced.Intel;

namespace InternalsViewer.Query.FullTrace.InstructionHistory.Listing;

internal sealed class InstructionCodeReader : CodeReader
{
    private const int MaximumLength = 15;

    private byte[] Buffer { get; } = new byte[MaximumLength];

    private int Length { get; set; }

    private int Position { get; set; }

    public void Reset(ReadOnlySpan<byte> code)
    {
        Length = Math.Min(code.Length, MaximumLength);

        code[..Length].CopyTo(Buffer);

        Position = 0;
    }

    public override int ReadByte() => Position < Length ? Buffer[Position++] : -1;
}
