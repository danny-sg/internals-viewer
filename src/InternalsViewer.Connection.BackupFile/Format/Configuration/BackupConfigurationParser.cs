using System.Buffers.Binary;
using System.Text;

namespace InternalsViewer.Connection.BackupFile.Format.Configuration;

internal static class BackupConfigurationParser
{
    private const int RecordHeaderLength = 8;

    private const int ScinDatabaseNamePairOffset = 104;

    private const int ScinServerNamePairOffset = 108;

    private const int SfgiNamePairOffset = 16;

    private const int SfinFileIdOffset = 20;

    private const int SfinSizeInPagesOffset = 24;

    private const int SfinFileTypeOffset = 28;

    private const int SfinFilegroupOrdinalOffset = 36;

    private const int SfinPhysicalSizeOffset = 40;

    private const int SfinLogicalNamePairOffset = 48;

    private const int SfinPhysicalNamePairOffset = 52;

    public static BackupConfiguration Parse(ReadOnlySpan<byte> data)
    {
        var configuration = new BackupConfiguration();

        var position = 0;

        var filegroupOrdinal = 0;

        while (position + RecordHeaderLength <= data.Length)
        {
            var recordType = Encoding.ASCII.GetString(data.Slice(position, 4));

            var recordSize = BinaryPrimitives.ReadInt32LittleEndian(data[(position + 4)..]);

            if (recordSize < RecordHeaderLength || position + recordSize > data.Length)
            {
                break;
            }

            var record = data.Slice(position, recordSize);

            switch (recordType)
            {
                case "SCIN":
                    configuration.DatabaseName = ReadString(record, ScinDatabaseNamePairOffset);
                    configuration.ServerName = ReadString(record, ScinServerNamePairOffset);
                    break;

                case "SFGI":
                    filegroupOrdinal++;

                    configuration.Filegroups.Add(new BackupFilegroup(filegroupOrdinal,
                                                                     ReadString(record, SfgiNamePairOffset)));
                    break;

                case "SFIN":
                    configuration.Files.Add(ReadFileEntry(record));
                    break;
            }

            position += recordSize;
        }

        return configuration;
    }

    private static BackupFileEntry ReadFileEntry(ReadOnlySpan<byte> record)
    {
        var fileType = BinaryPrimitives.ReadInt32LittleEndian(record[SfinFileTypeOffset..]);

        return new BackupFileEntry
        {
            FileId = BinaryPrimitives.ReadInt32LittleEndian(record[SfinFileIdOffset..]),
            FileType = Enum.IsDefined((BackupFileType)fileType) ? (BackupFileType)fileType : BackupFileType.Unknown,
            LogicalName = ReadString(record, SfinLogicalNamePairOffset),
            PhysicalName = ReadString(record, SfinPhysicalNamePairOffset),
            SizeInPages = BinaryPrimitives.ReadUInt32LittleEndian(record[SfinSizeInPagesOffset..]),
            PhysicalSizeBytes = BinaryPrimitives.ReadUInt32LittleEndian(record[SfinPhysicalSizeOffset..]),
            FilegroupOrdinal = BinaryPrimitives.ReadInt32LittleEndian(record[SfinFilegroupOrdinalOffset..]),
        };
    }

    private static string ReadString(ReadOnlySpan<byte> record, int pairOffset)
    {
        if (pairOffset + 4 > record.Length)
        {
            return string.Empty;
        }

        var length = BinaryPrimitives.ReadUInt16LittleEndian(record[pairOffset..]);

        var offset = BinaryPrimitives.ReadUInt16LittleEndian(record[(pairOffset + 2)..]);

        if (length == 0 || offset + length > record.Length)
        {
            return string.Empty;
        }

        return Encoding.Unicode.GetString(record.Slice(offset, length));
    }
}
