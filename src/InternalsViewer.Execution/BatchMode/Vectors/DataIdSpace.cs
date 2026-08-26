namespace InternalsViewer.Execution.BatchMode.Vectors;

public readonly record struct DataIdSpace(long HobtId, int ColumnId, int LocalDictionaryId)
{
    public const int NoLocalDictionary = -1;

    public bool IsGlobalOnly => LocalDictionaryId == NoLocalDictionary;

    public static DataIdSpace Global(long hobtId, int columnId) => new(hobtId, columnId, NoLocalDictionary);
}
