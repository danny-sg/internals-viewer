using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Export;
using InternalsViewer.Query.FullTrace.Helpers;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed partial class LocatorTable
{
    public static LocatorTable Read(BinaryReader reader, Locator[] locators)
        => new(BitPackedArray.Read(reader), ExportBinary.ReadArray<int>(reader), BitPackedArray.Read(reader), locators);

    public static Locator[] ReadLocators(BinaryReader reader)
    {
        var locators = new Locator[reader.ReadInt32()];

        for (var index = 0; index < locators.Length; index++)
        {
            var register = reader.ReadInt32();

            var valueType = (LocatorValueType)reader.ReadByte();

            var value = reader.ReadUInt64();

            var detail = reader.ReadUInt64();

            var text = HasText(valueType) ? reader.ReadString() : null;

            locators[index] = new Locator(register, valueType, value, detail, text);
        }

        return locators;
    }

    public static void WriteLocators(BinaryWriter writer, Locator[] locators)
    {
        writer.Write(locators.Length);

        foreach (var locator in locators)
        {
            writer.Write(locator.Register);

            writer.Write((byte)locator.ValueType);

            writer.Write(locator.Value);

            writer.Write(locator.Detail);

            if (HasText(locator.ValueType))
            {
                writer.Write(locator.Text ?? string.Empty);
            }
        }
    }

    public void Write(BinaryWriter writer)
    {
        CallIndexes.Write(writer);

        ExportBinary.WriteArray(writer, Palette);

        PaletteIndexes.Write(writer);
    }

    private static bool HasText(LocatorValueType valueType)
        => valueType is LocatorValueType.Text or LocatorValueType.WideText or LocatorValueType.Bindings;
}
