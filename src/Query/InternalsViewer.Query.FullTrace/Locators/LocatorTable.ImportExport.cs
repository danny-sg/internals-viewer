using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Export;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed partial class LocatorTable
{
    public static LocatorTable Read(BinaryReader reader, int? legacyReturnRegister = null)
    {
        var calls = ExportBinary.ReadArray<int>(reader);

        var locators = new Locator[calls.Length];

        for (var index = 0; index < locators.Length; index++)
        {
            var register = reader.ReadInt32();

            if (register == legacyReturnRegister)
            {
                register = LocatorReadResolver.ReturnRegister;
            }

            var kind = (LocatorKind)reader.ReadByte();

            var value = reader.ReadUInt64();

            var detail = reader.ReadUInt64();

            var text = kind is LocatorKind.Text or LocatorKind.WideText ? reader.ReadString() : null;

            locators[index] = new Locator(register, kind, value, detail, text);
        }

        return new LocatorTable(calls, locators);
    }

    public void Write(BinaryWriter writer)
    {
        ExportBinary.WriteArray(writer, Calls);

        foreach (var locator in Locators)
        {
            writer.Write(locator.Register);

            writer.Write((byte)locator.Kind);

            writer.Write(locator.Value);

            writer.Write(locator.Detail);

            if (locator.Kind is LocatorKind.Text or LocatorKind.WideText)
            {
                writer.Write(locator.Text ?? string.Empty);
            }
        }
    }
}
