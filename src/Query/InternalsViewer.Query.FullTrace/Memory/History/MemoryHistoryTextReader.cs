using System.Buffers.Binary;
using System.Text;

namespace InternalsViewer.Query.FullTrace.Memory.History;

public static class MemoryHistoryTextReader
{
    private const int MinimumLength = 3;

    private const int MaximumLength = 64;

    private const char LatinStart = (char)0xA0;

    private const char LatinEnd = (char)0x17F;

    private const string TruncatedSuffix = " (Truncated)";

    public static string? GetLabel(MemoryHistoryStore store, int block)
        => store.Blocks[block] is { WriteCount: > 0 } item ? GetLabel(store.GetImage(block, item.WriteCount - 1)) : null;

    public static string? GetText(MemoryHistoryStore store, int block)
        => store.Blocks[block] is { WriteCount: > 0 } item ? GetText(store.GetImage(block, item.WriteCount - 1)) : null;

    internal static string? GetText(MemoryHistoryImage image) => Read(image, int.MaxValue);

    internal static string? GetLabel(MemoryHistoryImage image)
        => Read(image, MaximumLength) is { } text
            ? Format(text, text.Length == MaximumLength && image.Data.Length > MaximumLength * sizeof(char))
            : null;

    internal static string? GetLiteralLabel(ReadOnlySpan<byte> bytes)
    {
        var width = bytes.Length > 1 && bytes[0] != 0 && bytes[1] == 0 ? sizeof(char) : 1;

        var text = new StringBuilder();

        for (var offset = 0; offset + width <= bytes.Length && text.Length < MaximumLength; offset += width)
        {
            var character = width == 1 ? (char)bytes[offset] : (char)BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);

            if (character == '\0')
            {
                return text.Length == 0 ? null : Format(text.ToString(), false);
            }

            text.Append(character);
        }

        return text.Length == 0 ? null : Format(text.ToString(), true);
    }

    private static string? Read(MemoryHistoryImage image, int maximum)
    {
        var text = new StringBuilder();

        for (var offset = 0; offset + sizeof(char) <= image.Data.Length && text.Length < maximum; offset += sizeof(char))
        {
            if (!image.Known[offset] || !image.Known[offset + 1])
            {
                break;
            }

            var character = (char)BinaryPrimitives.ReadUInt16LittleEndian(image.Data.AsSpan(offset));

            if (character == '\0')
            {
                break;
            }

            if (!IsText(character))
            {
                return null;
            }

            text.Append(character);
        }

        return text.Length < MinimumLength ? null : text.ToString();
    }

    private static string Format(string text, bool isTruncated) => $"\"{text}\"{(isTruncated ? TruncatedSuffix : string.Empty)}";

    private static bool IsText(char character)
        => character is '\t' or '\n' or '\r' or (>= ' ' and <= '~') or (>= LatinStart and <= LatinEnd);
}
