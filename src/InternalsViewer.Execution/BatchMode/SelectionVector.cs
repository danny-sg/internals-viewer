namespace InternalsViewer.Execution.BatchMode;

public sealed class SelectionVector
{
    public SelectionVector(int rowCount)
    {
        Selection = new ushort[rowCount];

        ActiveRowCount = rowCount;

        Reset();
    }

    public ushort[] Selection { get; init; }

    public int RowCount => ActiveRowCount;

    private int ActiveRowCount { get; set; }

    public ushort this[int index] => Selection[index];

    public void Reset() => Reset(Selection.Length);

    public void Reset(int rowCount)
    {
        for (var i = 0; i < rowCount; i++)
        {
            Selection[i] = (ushort)i;
        }

        ActiveRowCount = rowCount;
    }

    public void RemoveAll()
    {
        ActiveRowCount = 0;
    }

    public bool Clear(int row)
    {
        for (var i = 0; i < ActiveRowCount; i++)
        {
            if (Selection[i] != row)
            {
                continue;
            }

            Array.Copy(Selection, i + 1, Selection, i, --ActiveRowCount - i);

            return true;
        }

        return false;
    }

    public void Add(int row)
    {
        Selection[ActiveRowCount++] = (ushort)row;
    }

    public bool IsSelected(int row)
    {
        for (var i = 0; i < ActiveRowCount; i++)
        {
            if (Selection[i] == row)
            {
                return true;
            }
        }

        return false;
    }
}
