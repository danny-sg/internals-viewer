using InternalsViewer.Query.FullTrace.Export;
using InternalsViewer.Query.FullTrace.Helpers;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed partial class LocatorTable
{
    public static LocatorTable Read(BinaryReader reader, Locator[] locators)
        => new(BitPackedArray.Read(reader), ExportBinary.ReadArray<int>(reader), BitPackedArray.Read(reader), locators);

    public void Write(BinaryWriter writer)
    {
        CallIndexes.Write(writer);

        ExportBinary.WriteArray(writer, Palette);

        PaletteIndexes.Write(writer);
    }
}
