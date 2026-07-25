using System.Text;

namespace InternalsViewer.Connection.BackupFile.Mtf.Blocks.Descriptors;

internal sealed class EndOfTapeMarkerDescriptorBlock : DescriptorBlock
{
    public ulong LastDataSetPhysicalBlockAddress { get; }

    public EndOfTapeMarkerDescriptorBlock(MtfReader reader): base(reader)
    {
        LastDataSetPhysicalBlockAddress = reader.ReadUInt64();
        
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

        sb.AppendLine($"{prefix}End Of Tape Marker");
        sb.AppendLine($"{prefix}==================");

        sb.AppendLine($"{prefix}Last Data Set Physical Block Address: {LastDataSetPhysicalBlockAddress}");
    
        return sb.ToString();
    }
}