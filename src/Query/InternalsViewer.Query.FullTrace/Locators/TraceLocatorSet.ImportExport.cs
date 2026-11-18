using InternalsViewer.Query.FullTrace.Locators.Enums;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed partial class TraceLocatorSet
{
    internal static TraceLocatorSet Read(BinaryReader reader)
    {
        var values = new Locator[reader.ReadInt32()];

        for (var index = 0; index < values.Length; index++)
        {
            var argumentSlot = reader.ReadInt32();

            var valueType = (LocatorValueType)reader.ReadByte();

            var value = reader.ReadUInt64();

            var detail = reader.ReadUInt64();

            var text = HasText(valueType) ? reader.ReadString() : null;

            values[index] = new Locator(argumentSlot, valueType, value, detail, text);
        }

        var tables = new Dictionary<(ulong FunctionAddress, ulong InstanceAddress), LocatorTable>();

        var tableCount = reader.ReadInt32();

        for (var index = 0; index < tableCount; index++)
        {
            var key = (reader.ReadUInt64(), reader.ReadUInt64());

            tables[key] = LocatorTable.Read(reader, values);
        }

        return new TraceLocatorSet(values, tables);
    }

    internal void Write(BinaryWriter writer)
    {
        writer.Write(Values.Length);

        foreach (var locator in Values)
        {
            writer.Write(locator.ArgumentSlot);

            writer.Write((byte)locator.ValueType);

            writer.Write(locator.Value);

            writer.Write(locator.Detail);

            if (HasText(locator.ValueType))
            {
                writer.Write(locator.Text ?? string.Empty);
            }
        }

        writer.Write(Tables.Count);

        foreach (var ((functionAddress, instanceAddress), table) in Tables)
        {
            writer.Write(functionAddress);

            writer.Write(instanceAddress);

            table.Write(writer);
        }
    }

    private static bool HasText(LocatorValueType valueType)
        => valueType is LocatorValueType.Text or LocatorValueType.WideText or LocatorValueType.Bindings;
}
