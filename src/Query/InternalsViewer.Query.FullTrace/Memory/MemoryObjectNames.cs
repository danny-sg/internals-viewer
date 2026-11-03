using InternalsViewer.Query.Memory.Enums;

namespace InternalsViewer.Query.FullTrace.Memory;

public static class MemoryObjectNames
{
    private static readonly Dictionary<MemoryObjectType, string> Names = new()
    {
        [MemoryObjectType.MEMOBJ_EXECUTE] = "Internal Execution Plan (MXC)",
        [MemoryObjectType.MEMOBJ_COMPILE_ADHOC] = "Compiled Plan (CP)",
        [MemoryObjectType.MEMOBJ_QUERYEXECCNTXTFORSE] = "Storage Engine Query Context",
        [MemoryObjectType.MEMOBJ_STATEMENT] = "Compiled Statement (CStmt)",
        [MemoryObjectType.MEMOBJ_XSTMT] = "Execution Statement (XStmt)",
        [MemoryObjectType.MEMOBJ_PLANSKELETON] = "Plan Skeleton",
        [MemoryObjectType.MEMOBJ_CURSOREXEC] = "Cursor",
        [MemoryObjectType.MEMOBJ_CACHESTOREXPROC] = "Extended Procedure Cache"
    };

    public static string GetName(MemoryObjectType type) => Names.GetValueOrDefault(type, type.ToString());

    public static string GetName(string type) => Enum.TryParse<MemoryObjectType>(type, out var parsed) ? GetName(parsed) : type;
}
