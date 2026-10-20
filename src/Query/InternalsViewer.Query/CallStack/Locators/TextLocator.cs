using System.Text;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.CallStack.Locators;

public readonly record struct TextLocator(Locator Locator)
{
    private const int MaximumBytes = 128;

    public string Text => Locator.Text ?? string.Empty;

    public bool IsTruncated => Locator.Detail != 0;

    public static Locator? Read(IMemoryReader memory, int register, ulong address, LocatorValueType valueType)
    {
        var width = valueType == LocatorValueType.WideText ? 2 : 1;

        Span<byte> bytes = stackalloc byte[MaximumBytes];

        bytes.Clear();

        var count = memory.Read(address, bytes);

        if (count == 0)
        {
            return null;
        }

        var data = bytes[..((count + sizeof(ulong) - 1) / sizeof(ulong) * sizeof(ulong))];

        var end = FindTerminator(data, width);

        var characters = end < 0 ? data : data[..end];

        var text = width == 2 ? Encoding.Unicode.GetString(characters) : Encoding.Latin1.GetString(characters);

        return new Locator(register, valueType, (ulong)text.Length, end < 0 ? 1ul : 0, text);
    }

    public override string ToString() => IsTruncated ? $"{Text} (Truncated)" : Text;

    private static int FindTerminator(ReadOnlySpan<byte> bytes, int width)
    {
        for (var index = 0; index + width <= bytes.Length; index += width)
        {
            if (bytes[index] == 0 && (width == 1 || bytes[index + 1] == 0))
            {
                return index;
            }
        }

        return -1;
    }
}
