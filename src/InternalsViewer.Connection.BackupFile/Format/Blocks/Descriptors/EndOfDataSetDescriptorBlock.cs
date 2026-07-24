using System.Text;
using InternalsViewer.Connection.BackupFile.Reader;
using InternalsViewer.Connection.BackupFile.Format.Blocks.Attributes;

namespace InternalsViewer.Connection.BackupFile.Format.Blocks.Descriptors;

internal sealed class EndOfDataSetDescriptorBlock : DescriptorBlock
{
    public StartOfDataSetAttributes StartOfDataSetAttributes { get; }
    
    public uint NumberOfCorruptFiles { get; }
    
    public ulong ReservedForMbc1 { get; }
    
    public ulong ReservedForMbc2 { get; }
    
    public ushort FddMediaSequenceNumber { get; }
    
    public ushort DataSetNumber { get; }
    
    public DateTime MediaWriteDate { get; }

    public EndOfDataSetDescriptorBlock(BackupReader reader): base(reader)
    {
        StartOfDataSetAttributes = (StartOfDataSetAttributes)reader.ReadUInt32();
        NumberOfCorruptFiles = reader.ReadUInt32();
        ReservedForMbc1 = reader.ReadUInt64();
        ReservedForMbc2 = reader.ReadUInt64();
        FddMediaSequenceNumber = reader.ReadUInt16();
        DataSetNumber = reader.ReadUInt16();
        MediaWriteDate = reader.ReadDate();

        ReadStreams(reader);
    }

    public override string ToString()
    {
        return ToString(string.Empty);
    }

    public string ToString(string prefix)
    {
        var sb = new StringBuilder();

        sb.AppendLine(CommonHeaderToString(prefix));

        sb.AppendLine($"{prefix}End of Data Set Block");
        sb.AppendLine($"{prefix}=====================");

        sb.AppendLine($"{prefix}Start of Data Set Attributes: {StartOfDataSetAttributes}");
        sb.AppendLine($"{prefix}Number of Corrupt Files:      {NumberOfCorruptFiles}");
        sb.AppendLine($"{prefix}Reserved for MBC1:            {ReservedForMbc1}");
        sb.AppendLine($"{prefix}Reserved for MBC2:            {ReservedForMbc2}");
        sb.AppendLine($"{prefix}FDD Media Sequence Number:    {FddMediaSequenceNumber}");
        sb.AppendLine($"{prefix}Data Set Number:              {DataSetNumber}");
        sb.AppendLine($"{prefix}Media Write Date:             {MediaWriteDate}");

        return sb.ToString();
    }
}