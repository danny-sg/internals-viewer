using System.Text;
using InternalsViewer.Connection.BackupFile.Reader;
using InternalsViewer.Connection.BackupFile.Format.Blocks.Attributes;
using InternalsViewer.Connection.BackupFile.Format.Blocks.Os;
using InternalsViewer.Connection.BackupFile.Format.Streams;

namespace InternalsViewer.Connection.BackupFile.Format.Blocks.Descriptors;

/// <summary>
/// Descriptor Block (DBLK)
/// </summary>
/// <remarks>
/// Descriptor Blocks are the building blocks of the backup file. They have four parts:
/// 
///     1. Common Header (Required)
///     2. Fixed Length Data
///     3. Operating System Specific Data
///     4. Variable Length Data
///
/// This abstract class is the base class for all Descriptor Blocks and handles the Common Header and Operating System Specific Data.
/// </remarks>
internal abstract class DescriptorBlock
{
    public long StartPosition { get; set; }

    public BlockType BlockType { get; set; }
    
    public BlockAttributes Attributes { get; set; }
    
    public ushort OffsetToFirstEvent { get; set; }
    
    public OsId OsId { get; set; }
    
    public byte OsVersion { get; set; }
    
    public ulong DisplayableSize { get; set; }
    
    public ulong FormatLogicalAddress { get; set; }
    
    public ushort ReservedMbc { get; set; }
    
    public ushort Reserved1 { get; set; }
    
    public ushort Reserved2 { get; set; }
    
    public ushort Reserved3 { get; set; }
    
    public uint ControlBlock { get; set; }
    
    public uint Reserved4 { get; set; }
    
    public OsSpecificData? OsSpecificData { get; set; }

    protected StringType StringType { get; set; }
    
    public byte Reserved5 { get; set; }
    
    public ushort HeaderChecksum { get; set; }

    public List<DataStream> Streams { get; set; } = [];

    public List<DescriptorBlock> Children { get; set; } = [];

    protected DescriptorBlock(BackupReader reader)
    {
        ReadCommonHeader(reader);
    }

    protected void ReadCommonHeader(BackupReader reader)
    {
        StartPosition = reader.BaseStream.Position;
        Streams = [];

        BlockType = (BlockType)reader.ReadUInt32();
        Attributes = (BlockAttributes)reader.ReadUInt32();
        OffsetToFirstEvent = reader.ReadUInt16();
        OsId = (OsId)reader.ReadByte();
        OsVersion = reader.ReadByte();
        DisplayableSize = reader.ReadUInt64();
        FormatLogicalAddress = reader.ReadUInt64();
        ReservedMbc = reader.ReadUInt16();
        Reserved1 = reader.ReadUInt16();
        Reserved2 = reader.ReadUInt16();
        Reserved3 = reader.ReadUInt16();
        ControlBlock = reader.ReadUInt32();
        Reserved4 = reader.ReadUInt32();
        OsSpecificData = reader.ReadOsSpecificData(StartPosition, OsId, OsVersion, BlockType);
        StringType = (StringType)reader.ReadByte();
        Reserved5 = reader.ReadByte();
        HeaderChecksum = reader.ReadUInt16();
    }

    protected void ReadStreams(BackupReader reader)
    {
        var offset = OffsetToFirstEvent + StartPosition;

        var boundaryOffset = (4 - offset % 4) % 4;

        reader.BaseStream.Seek(offset + boundaryOffset, SeekOrigin.Begin);

        string streamType;

        do
        {
            var stream = new DataStream(reader);

            streamType = stream.Header.StreamId;

            Streams.Add(stream);
        } while (streamType != StreamTypes.EndPadStream && streamType != string.Empty);
    }

    public abstract override string ToString();

    protected string CommonHeaderToString(string prefix)
    {
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine($"{prefix}Common Header");
        stringBuilder.AppendLine($"{prefix}=============");
        stringBuilder.AppendLine($"{prefix}Block Type:             {BlockType}");
        stringBuilder.AppendLine($"{prefix}Attributes:             {Attributes}");
        stringBuilder.AppendLine($"{prefix}Offset To First Event:  {OffsetToFirstEvent}");
        stringBuilder.AppendLine($"{prefix}OS Id:                  {OsId}");
        stringBuilder.AppendLine($"{prefix}OS Version:             {OsVersion}");
        stringBuilder.AppendLine($"{prefix}Displayable Size:       {DisplayableSize}");
        stringBuilder.AppendLine($"{prefix}Format Logical Address: {FormatLogicalAddress}");
        stringBuilder.AppendLine($"{prefix}Reserved MBC:           {ReservedMbc}");
        stringBuilder.AppendLine($"{prefix}Reserved 1:             {Reserved1}");
        stringBuilder.AppendLine($"{prefix}Reserved 2:             {Reserved2}");
        stringBuilder.AppendLine($"{prefix}Reserved 3:             {Reserved3}");
        stringBuilder.AppendLine($"{prefix}Control Block:          {ControlBlock}");
        stringBuilder.AppendLine($"{prefix}Reserved 4:             {Reserved4}");
        stringBuilder.AppendLine($"{prefix}OS Specific Data:       {OsSpecificData}");
        stringBuilder.AppendLine($"{prefix}String Type:            {StringType}");
        stringBuilder.AppendLine($"{prefix}Reserved 5:             {Reserved5}");
        stringBuilder.AppendLine($"{prefix}Header Checksum:        {HeaderChecksum}");
        stringBuilder.AppendLine($"{prefix}Stream Count:           {Streams.Count}");

        return stringBuilder.ToString();
    }
}