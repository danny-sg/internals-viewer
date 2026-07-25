using System.Text;

namespace InternalsViewer.Connection.BackupFile.Mtf.Blocks.Descriptors;

internal sealed class SoftFilemarkDescriptorBlock : DescriptorBlock
{
    public uint NumberOfFilemarkEntries { get; }

    public uint FilemarkEntriesUsed { get; }

    public uint[] BlockAddressPreviousFilemarksArray { get; }

    public SoftFilemarkDescriptorBlock(MtfReader reader) : base(reader)
    {
        NumberOfFilemarkEntries = reader.ReadUInt32();
        FilemarkEntriesUsed = reader.ReadUInt32();
        BlockAddressPreviousFilemarksArray = new uint[FilemarkEntriesUsed];

        for (uint i = 0; i < NumberOfFilemarkEntries; i++)
        {
            var val = reader.ReadUInt32();

            if (i < FilemarkEntriesUsed)
            {
                BlockAddressPreviousFilemarksArray.SetValue(val, i);
            }
        }

    }

    public override string ToString()
    {
        return ToString(string.Empty);
    }

    public string ToString(string prefix)
    {
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine(CommonHeaderToString(prefix));

        stringBuilder.AppendLine($"{prefix}Soft Filemark");
        stringBuilder.AppendLine($"{prefix}=============");

        stringBuilder.AppendLine($"{prefix}Number Of Filemark Entries: {NumberOfFilemarkEntries}");
        stringBuilder.AppendLine($"{prefix}Filemark Entries Used:      {FilemarkEntriesUsed}");

        return stringBuilder.ToString();
    }
}