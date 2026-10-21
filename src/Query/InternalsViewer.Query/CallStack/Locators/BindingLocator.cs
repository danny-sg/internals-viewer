using System.Buffers.Binary;
using System.Globalization;
using InternalsViewer.Query.Interfaces;

namespace InternalsViewer.Query.CallStack.Locators;

public readonly record struct BindingLocator(Locator Locator)
{
    public const int Size = 0x58;

    public const int MaximumBindings = 64;

    private const int OrdinalOffset = 0x00;

    private const int ValueOffset = 0x08;

    private const int LengthOffset = 0x10;

    private const int StatusOffset = 0x18;

    private const int PartsOffset = 0x38;

    private const int TypeOffset = 0x54;

    private const char BindingSeparator = ';';

    private const char FieldSeparator = ',';

    private const string DisplaySeparator = " | ";

    public ulong Count => Locator.Detail;

    public bool IsTruncated => Locator.Value < Locator.Detail;

    public IReadOnlyList<ColumnBinding> Bindings => Parse(Locator.Text);

    public static Locator? Read(IMemoryReader memory, int register, ulong address, ulong count)
    {
        if (count == 0)
        {
            return null;
        }

        var bytes = new byte[(int)Math.Min(count, MaximumBindings) * Size];

        var read = memory.Read(address, bytes) / Size;

        if (read == 0)
        {
            return null;
        }

        var bindings = new ColumnBinding[read];

        for (var index = 0; index < read; index++)
        {
            bindings[index] = ReadBinding(bytes.AsSpan(index * Size, Size));
        }

        return new Locator(register, LocatorValueType.Bindings, (ulong)read, count, Format(bindings));
    }

    public override string ToString()
    {
        var text = string.Join(DisplaySeparator, Bindings);

        return IsTruncated ? $"{text}{DisplaySeparator}{Count - Locator.Value} More" : text;
    }

    private static ColumnBinding ReadBinding(ReadOnlySpan<byte> bytes)
        => new(BinaryPrimitives.ReadUInt64LittleEndian(bytes[OrdinalOffset..]),
               BinaryPrimitives.ReadUInt16LittleEndian(bytes[TypeOffset..]),
               BinaryPrimitives.ReadUInt32LittleEndian(bytes[PartsOffset..]),
               BinaryPrimitives.ReadUInt64LittleEndian(bytes[ValueOffset..]),
               BinaryPrimitives.ReadUInt64LittleEndian(bytes[LengthOffset..]),
               BinaryPrimitives.ReadUInt64LittleEndian(bytes[StatusOffset..]));

    private static string Format(IEnumerable<ColumnBinding> bindings) => string.Join(BindingSeparator, bindings.Select(Format));

    private static string Format(ColumnBinding binding)
    {
        ulong[] fields = [binding.Ordinal, binding.Type, binding.Parts, binding.ValueOffset, binding.LengthOffset, binding.StatusOffset];

        return string.Join(FieldSeparator, fields.Select(f => f.ToString(CultureInfo.InvariantCulture)));
    }

    private static ColumnBinding[] Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var entries = text.Split(BindingSeparator);

        var bindings = new ColumnBinding[entries.Length];

        for (var index = 0; index < entries.Length; index++)
        {
            var fields = entries[index].Split(FieldSeparator);

            bindings[index] = new ColumnBinding(ulong.Parse(fields[0], CultureInfo.InvariantCulture),
                                                ushort.Parse(fields[1], CultureInfo.InvariantCulture),
                                                uint.Parse(fields[2], CultureInfo.InvariantCulture),
                                                ulong.Parse(fields[3], CultureInfo.InvariantCulture),
                                                ulong.Parse(fields[4], CultureInfo.InvariantCulture),
                                                ulong.Parse(fields[5], CultureInfo.InvariantCulture));
        }

        return bindings;
    }
}
