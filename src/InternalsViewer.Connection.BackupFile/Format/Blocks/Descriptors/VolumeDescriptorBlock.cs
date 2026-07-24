using System.Text;
using InternalsViewer.Connection.BackupFile.Reader;
using InternalsViewer.Connection.BackupFile.Format.Blocks.Attributes;

namespace InternalsViewer.Connection.BackupFile.Format.Blocks.Descriptors;

internal sealed class VolumeDescriptorBlock : DescriptorBlock
{
    public VolumeAttributes VolumeAttributes { get; }
    
    public string DeviceName { get; }
    
    public string VolumeName { get; }
    
    public string MachineName { get; }
    
    public DateTime MediaWriteDate { get; }

    public VolumeDescriptorBlock(BackupReader reader): base(reader)
    {
        VolumeAttributes = (VolumeAttributes)reader.ReadUInt32();
        DeviceName = reader.ReadVariableLengthString(StartPosition, StringType);
        VolumeName = reader.ReadVariableLengthString(StartPosition, StringType);
        MachineName = reader.ReadVariableLengthString(StartPosition, StringType);
        MediaWriteDate = reader.ReadDate();
        
        ReadStreams(reader);
    }

    public override string ToString()
    {
        return ToString(string.Empty);
    }

    public string ToString(string prefix)
    {
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine(CommonHeaderToString(prefix));

        stringBuilder.AppendLine($"{prefix}Volume Block");
        stringBuilder.AppendLine($"{prefix}============");

        stringBuilder.AppendLine($"{prefix}Volume Attributes: {VolumeAttributes}");
        stringBuilder.AppendLine($"{prefix}Device Name:       {DeviceName}");
        stringBuilder.AppendLine($"{prefix}Volume Name:       {VolumeName}");
        stringBuilder.AppendLine($"{prefix}Machine Name:      {MachineName}");
        stringBuilder.AppendLine($"{prefix}Media Write Date:  {MediaWriteDate}");

        return stringBuilder.ToString();
    }
}