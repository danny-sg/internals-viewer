using System.Buffers.Binary;
using System.Globalization;
using InternalsViewer.Query.FullTrace.Locators.Enums;
using InternalsViewer.Query.FullTrace.Memory;
using InternalsViewer.Query.FullTrace.Memory.Layouts;

namespace InternalsViewer.Query.FullTrace.Locators;

public readonly record struct BindingLocator(Locator Locator)
{
    public const int MaximumBindings = 64;

    private const char BindingSeparator = ';';

    private const char FieldSeparator = ',';

    private const string DisplaySeparator = " | ";

    public ulong Count => Locator.Detail;

    public bool IsTruncated => Locator.Value < Locator.Detail;

    public IReadOnlyList<ColumnBinding> Bindings => Parse(Locator.Text);

    public static Locator? Read(ITraceMemoryReader memory, int argumentSlot, ulong address, ulong count)
    {
        if (count == 0)
        {
            return null;
        }

        var layout = memory.Layout.OleDbBinding;

        var bytes = new byte[(int)Math.Min(count, MaximumBindings) * layout.Size];

        var read = memory.Read(address, bytes) / layout.Size;

        if (read == 0)
        {
            return null;
        }

        var bindings = new ColumnBinding[read];

        for (var index = 0; index < read; index++)
        {
            bindings[index] = ReadBinding(layout, bytes.AsSpan(index * layout.Size, layout.Size));
        }

        return new Locator(argumentSlot, LocatorValueType.Bindings, (ulong)read, count, Format(bindings));
    }

    public override string ToString()
    {
        var text = string.Join(DisplaySeparator, Bindings);

        return IsTruncated ? $"{text}{DisplaySeparator}{Count - Locator.Value} More" : text;
    }

    private static ColumnBinding ReadBinding(EngineLayout.OleDbBindingLayout layout, ReadOnlySpan<byte> bytes)
        => new(GetValue(bytes, layout.OrdinalField),
               (ushort)GetValue(bytes, layout.TypeField),
               (uint)GetValue(bytes, layout.PartsField),
               GetValue(bytes, layout.ValueOffsetField),
               GetValue(bytes, layout.LengthOffsetField),
               GetValue(bytes, layout.StatusOffsetField));

    private static ulong GetValue(ReadOnlySpan<byte> bytes, MemoryField field)
    {
        Span<byte> value = stackalloc byte[sizeof(ulong)];

        value.Clear();

        bytes.Slice(field.Offset, field.Length).CopyTo(value);

        return BinaryPrimitives.ReadUInt64LittleEndian(value);
    }

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
