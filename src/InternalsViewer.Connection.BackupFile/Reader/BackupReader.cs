using InternalsViewer.Connection.BackupFile.Format;
using InternalsViewer.Connection.BackupFile.Format.Blocks;
using InternalsViewer.Connection.BackupFile.Format.Blocks.Descriptors;
using InternalsViewer.Connection.BackupFile.Format.Blocks.Descriptors.SqlServer;
using InternalsViewer.Connection.BackupFile.Format.Blocks.Os;
using InternalsViewer.Connection.BackupFile.Format.Blocks.Os.Windows;
using InternalsViewer.Connection.BackupFile.Format.Streams;

namespace InternalsViewer.Connection.BackupFile.Reader;

internal sealed class BackupReader : BinaryReader
{
    private delegate DescriptorBlock DescriptorBlockFactory(BackupReader reader);

    private static readonly Dictionary<BlockType, DescriptorBlockFactory> DescriptorBlockFactories = new()
    {
        { BlockType.Tape, s => new TapeHeaderDescriptorBlock(s) },
        { BlockType.StartOfDataSet, s => new StartOfDataSetDescriptorBlock(s) },
        { BlockType.Volume, s => new VolumeDescriptorBlock(s) },
        { BlockType.EndOfSetPad, s => new EndOfPadSetDescriptorBlock(s) },
        { BlockType.EndOfSet, s => new EndOfDataSetDescriptorBlock(s) },
        { BlockType.EndOfTape, s => new EndOfTapeMarkerDescriptorBlock(s) },
        { BlockType.SoftFileMark, s => new SoftFilemarkDescriptorBlock(s) },
        { BlockType.MSCI, s => new InfoFileBlock(s) },
        { BlockType.MSDA, s => new DataFileBlock(s) },
        { BlockType.MSTL, s => new LogFileBlock(s) },
        { BlockType.MSLS, s => new DataFileBlock(s) }
    };

    public BackupReader(string filename) : base(new FileStream(filename, FileMode.Open, FileAccess.Read))
    {
    }

    public BackupReader(byte[] data): base(new MemoryStream(data))
    {
    }

    public bool TryReadBlock(out DescriptorBlock block)
    {
        var blockType = PeekNextBlockType();

        if (!DescriptorBlockFactories.TryGetValue(blockType, out var factory))
        {
            block = null!;

            return false;
        }

        block = factory(this);

        return true;
    }

    public static bool IsKnownBlockType(BlockType blockType) => DescriptorBlockFactories.ContainsKey(blockType);

    public static IEnumerable<BlockType> KnownBlockTypes => DescriptorBlockFactories.Keys;

    public StreamHeader ReadStreamHeader()
    {
        return new StreamHeader(this);
    }

    public BlockType PeekNextBlockType()
    {
        // Check for end of file
        if (BaseStream.Position + sizeof(uint) >= BaseStream.Length)
        {
            return BlockType.None;
        }

        var blockType = (BlockType)ReadUInt32();

        BaseStream.Seek(-sizeof(uint), SeekOrigin.Current);

        return blockType;
    }

    public OsSpecificData? ReadOsSpecificData(long startPosition, OsId id, byte osVersion, BlockType type)
    {
        var previousPosition = BaseStream.Position;
        _ = ReadUInt16();
        
        var offset = ReadUInt16();

        BaseStream.Seek(startPosition + offset, SeekOrigin.Begin);

        if (id == OsId.WindowsNt && osVersion == 1 && type == BlockType.Volume)
        {
            return new WindowsNt1VolB(this);
        }

        BaseStream.Seek(previousPosition + 4, SeekOrigin.Begin);

        return null;
    }

    public string ReadVariableLengthString(long position, StringType type)
    {
        var previousPosition = BaseStream.Position;

        var length = ReadUInt16();
        var offset = position + ReadUInt16();

        BaseStream.Seek(offset, SeekOrigin.Begin);

        string result;

        switch (type)
        {
            case StringType.Ansi:
                {
                    var bytes = ReadBytes(length);
                    var encoding = new System.Text.ASCIIEncoding();
                    result = encoding.GetString(bytes);
                    break;
                }
            case StringType.Unicode:
                {
                    var bytes = ReadBytes(length);
                    var encoding = new System.Text.UnicodeEncoding();
                    result = encoding.GetString(bytes);
                    break;
                }
            default:
                result = string.Empty;
                break;
        }

        BaseStream.Seek(previousPosition + 4, SeekOrigin.Begin);

        return result;
    }

    public string ReadFixedLengthString(int size, StringType type)
    {
        string result;

        switch (type)
        {
            case StringType.Ansi:
                {
                    var bytes = ReadBytes(size);
                    var encoding = new System.Text.ASCIIEncoding();

                    result = encoding.GetString(bytes);
                    break;
                }
            case StringType.Unicode:
                {
                    var bytes = ReadBytes(size);
                    var encoding = new System.Text.UnicodeEncoding();

                    result = encoding.GetString(bytes);
                    break;
                }
            default:
                result = string.Empty;
                break;
        }

        return result;
    }

    /// <summary>
    /// Reads a Date in the format used by the backup
    /// </summary>
    /// <remarks>
    /// Backup format (MTF_DATE_TIME) is a 5 byte/40 bit value in the following format:
    /// 
    /// Byte 0          Byte 1           Byte 2            Byte 3          Byte 4
    /// 7|6|5|4|3|2|1|0 |7|6|5|4|3|2|1|0 |7|6|5|4|3|2|1|0 |7|6|5|4|3|2|1|0 |7|6|5|4|3|2|1|0
    /// y y y y y y y y  y y y y y y m m  m m d d d d d h  h h h h m m m m  m m s s s s s s
    /// 
    /// </remarks>
    public DateTime ReadDate()
    {
        var byte0 = ReadByte();
        var byte1 = ReadByte();
        var byte2 = ReadByte();
        var byte3 = ReadByte();
        var byte4 = ReadByte();

        if (byte0 == 0 && byte1 == 0 && byte2 == 0 && byte3 == 0 && byte4 == 0)
        {
            return new DateTime();
        }

        var year = (byte0 << 6) + (byte1 >> 2);
        var month = ((byte1 & 0x3) << 2) + (byte2 >> 6);
        var day = (byte2 & 0x3E) >> 1;
        var hour = ((byte2 & 0x1) << 4) + (byte3 >> 4);
        var minute = ((byte3 & 0xF) << 2) + (byte4 >> 6);
        var second = byte4 & 0x3F;

        return new DateTime(year, month, day, hour, minute, second);
    }

    ~BackupReader()
    {
        Close();
    }
}