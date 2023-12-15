namespace InternalsViewer.Internals.Helpers;

public class IdHelpers
{
    /// <summary>
    /// Gets the Allocation Unit Id from the Object Id and Index Id
    /// </summary>
    /// <remarks>
    /// Taken from: <see href="https://www.sqlskills.com/blogs/paul/inside-the-storage-engine-how-are-allocation-unit-ids-calculated/"/>
    /// </remarks>
    public static long GetAllocationUnitId(int objectId, int indexId)
    {
        return (long)indexId << 48 | (long)objectId << 16;
    }
}