using System.Data;
using InternalsViewer.Internals.Generators;

// ReSharper disable StringLiteralTypo

namespace InternalsViewer.Internals.Metadata.Internals.Tables;

/// <summary>
/// sys.sysprufiles
/// </summary>
[InternalsMetadata]
public record InternalFile
{
    [InternalsMetadataColumn("dbfragid", 1, SqlDbType.Int, 4, 4, 1)]
    public int DbFragId { get; set; }

    [InternalsMetadataColumn("fileid", 2, SqlDbType.Int, 4, 8, 2)]
    public int FileId { get; set; }

    [InternalsMetadataColumn("grpid", 3, SqlDbType.Int, 4, 12, 3)]
    public int FileGroupId { get; set; }

    [InternalsMetadataColumn("status", 4, SqlDbType.Int, 4, 16, 4)]
    public int Status { get; set; }

    [InternalsMetadataColumn("filetype", 5, SqlDbType.TinyInt, 1, 20, 5)]
    public byte FileType { get; set; }

    [InternalsMetadataColumn("filestate", 6, SqlDbType.TinyInt, 1, 21, 6)]
    public byte FileState { get; set; }

    [InternalsMetadataColumn("size", 7, SqlDbType.Int, 4, 22, 7)]
    public int Size { get; set; }

    [InternalsMetadataColumn("maxsize", 8, SqlDbType.Int, 4, 26, 8)]
    public int MaxSize { get; set; }

    [InternalsMetadataColumn("growth", 9, SqlDbType.Int, 4, 30, 9)]
    public int Growth { get; set; }

    [InternalsMetadataColumn("lname", 10, SqlDbType.NVarChar, 256, -1, 10)]
    public string LogicalName { get; set; } = string.Empty;

    [InternalsMetadataColumn("pname", 11, SqlDbType.NVarChar, 520, -2, 11)]
    public string PhysicalName { get; set; } = string.Empty;

    [InternalsMetadataColumn("createlsn", 12, SqlDbType.Binary, 10, 34, 12)]
    public byte[] CreateLsn { get; set; } = new byte[10];

    [InternalsMetadataColumn("droplsn", 13, SqlDbType.Binary, 10, 44, 13)]
    public byte[] DropLsn { get; set; } = new byte[10];

    [InternalsMetadataColumn("fileguid", 14, SqlDbType.UniqueIdentifier, 16, 54, 14)]
    public Guid FileGuid { get; set; }

    [InternalsMetadataColumn("internalstatus", 15, SqlDbType.Int, 4, 70, 15)]
    public int InternalStatus { get; set; }

    [InternalsMetadataColumn("readonlylsn", 16, SqlDbType.Binary, 10, 74, 16)]
    public byte[] ReadOnlyLsn { get; set; } = new byte[10];

    [InternalsMetadataColumn("readwritelsn", 17, SqlDbType.Binary, 10, 84, 17)]
    public byte[] ReadWriteLsn { get; set; } = new byte[10];

    [InternalsMetadataColumn("readonlybaselsn", 18, SqlDbType.Binary, 10, 94, 18)]
    public byte[] ReadOnlyBaseLsn { get; set; } = new byte[10];

    [InternalsMetadataColumn("firstupdatelsn", 19, SqlDbType.Binary, 10, 104, 19)]
    public byte[] FirstUpdateLsn { get; set; } = new byte[10];

    [InternalsMetadataColumn("lastupdatelsn", 20, SqlDbType.Binary, 10, 114, 20)]
    public byte[] LastUpdateLsn { get; set; } = new byte[10];

    [InternalsMetadataColumn("backuplsn", 21, SqlDbType.Binary, 10, 124, 21)]
    public byte[] BackupLsn { get; set; } = new byte[10];

    [InternalsMetadataColumn("diffbaselsn", 22, SqlDbType.Binary, 10, 134, 22)]
    public byte[] DiffBaseLsn { get; set; } = new byte[10];

    [InternalsMetadataColumn("diffbaseguid", 23, SqlDbType.UniqueIdentifier, 16, 144, 23)]
    public Guid DiffBaseGuid { get; set; }

    [InternalsMetadataColumn("diffbasetime", 24, SqlDbType.DateTime, 8, 160, 24)]
    public DateTime DiffBaseTime { get; set; }

    [InternalsMetadataColumn("diffbaseseclsn", 25, SqlDbType.Binary, 10, 168, 25)]
    public byte[] DiffBaseSecLsn { get; set; } = new byte[10];

    [InternalsMetadataColumn("redostartlsn", 26, SqlDbType.Binary, 10, 178, 26)]
    public byte[] RedoStartLsn { get; set; } = new byte[10];

    [InternalsMetadataColumn("redotargetlsn", 27, SqlDbType.Binary, 10, 188, 27)]
    public byte[] RedoTargetLsn { get; set; } = new byte[10];

    [InternalsMetadataColumn("forkguid", 28, SqlDbType.UniqueIdentifier, 16, 198, 28)]
    public Guid ForkGuid { get; set; }

    [InternalsMetadataColumn("forklsn", 29, SqlDbType.Binary, 10, 214, 29)]
    public byte[] ForkLsn { get; set; } = new byte[10];

    [InternalsMetadataColumn("forkvc", 30, SqlDbType.BigInt, 8, 224, 30)]
    public long ForkVc { get; set; }

    [InternalsMetadataColumn("redostartforkguid", 31, SqlDbType.UniqueIdentifier, 16, 232, 31)]
    public Guid RedoStartForkGuid { get; set; }
}