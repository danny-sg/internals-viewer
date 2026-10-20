using System.Text;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Interfaces.Hooks;

namespace InternalsViewer.Query.FullTrace.Locators.Parsers;

internal sealed class TextLocatorParser(bool isWide, ImageText? images) : LocatorParser
{
    private const int MaximumTextBytes = 128;

    public override LocatorValueType ValueType => isWide ? LocatorValueType.WideText : LocatorValueType.Text;

    public override IReadOnlyList<LocatorTypeName> TypeNames { get; } = [new(isWide ? "wchar_t" : "char")];

    public override bool IsConstPointerOnly => true;

    public override Locator? Read(ITraceReplay replay, int register, ulong address)
    {
        Span<byte> bytes = stackalloc byte[MaximumTextBytes];

        bytes.Clear();

        var count = replay.Read(address, bytes);

        var data = bytes[..((count + sizeof(ulong) - 1) / sizeof(ulong) * sizeof(ulong))];

        var end = FindTerminator(data, isWide ? 2 : 1);

        if ((count == 0 || end < 0) && images?.Read(address, isWide) is { } imageText)
        {
            return TextLocator.Create(register, ValueType, imageText, isTruncated: false);
        }

        if (count == 0)
        {
            return null;
        }

        var characters = end < 0 ? data : data[..end];

        var text = isWide ? Encoding.Unicode.GetString(characters) : Encoding.Latin1.GetString(characters);

        return TextLocator.Create(register, ValueType, text, isTruncated: end < 0);
    }

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
