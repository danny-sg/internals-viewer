using System.Text;
using InternalsViewer.Internals.Columnstore.Blobs;

namespace InternalsViewer.Internals.Columnstore.Dictionaries;

/// <summary>
/// Page of dictionary string values
/// </summary>
public abstract class StringPage
{
    public SubLobType SubLobType { get; set; }

    public int PageFlags { get; set; }

    public int StringCount { get; set; }

    public int Offset { get; set; }

    public int Size { get; set; }

    public string GetValue(int handleOffset, Encoding encoding) => encoding.GetString(GetBytes(handleOffset));

    protected abstract ReadOnlySpan<byte> GetBytes(int handleOffset);
}
