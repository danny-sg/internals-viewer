using InternalsViewer.Internals.Engine.Address;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InternalsViewer.Internals.Tests.VerificationTool.Models;

/// <summary>
/// Index row outputted by DBCC PAGE
/// </summary>
internal record DatabaseIndexRow
{
    public short FileId { get; set; }

    public int PageId { get; set; }

    public short Row { get; set; }

    public short Level { get; set; }

    public short ChildFileId { get; set; }

    public int ChildPageId { get; set; }

    public List<DatabaseField> Values { get; set; } = new();

    public short RowSize { get; set; }
    
    public RowIdentifier? Rid { get; set; }
}

internal record DatabaseTableRow
{
    public short FileId { get; set; }

    public int PageId { get; set; }

    public short Row { get; set; }

    public List<DatabaseField> Values { get; set; } = new();
}

internal record DatabaseIndex
{
    public string Name { get; set; } = string.Empty;

    public int ObjectId { get; set; }

    public int IndexId { get; set; }

    public string[] Fields { get; set; } = Array.Empty<string>();

    public List<DatabaseIndexRow> Rows { get; set; } = new();
}

internal record DatabaseField
{
    public string Name { get; set; } = string.Empty;

    public string? Value { get; set; }
}

internal record VerificationResult
{
    public PageAddress PageAddress { get; set; }

    public int Slot { get; set; }

    public bool IsVerified { get; set; }

    public int PassCount { get; set; }

    public int FailCount { get; set; }
}