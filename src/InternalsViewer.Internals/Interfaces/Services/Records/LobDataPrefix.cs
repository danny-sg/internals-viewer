namespace InternalsViewer.Internals.Interfaces.Services.Records;

/// <summary>
/// The opening bytes of a blob together with the length of the blob they were taken from
/// </summary>
/// <remarks>
/// The total length is carried because checks over a blob, archive compression among them, are stated against the
/// whole blob rather than against the bytes to hand.
/// </remarks>
public readonly record struct LobDataPrefix(byte[] Data, int TotalLength)
{
    public bool IsComplete => Data.Length == TotalLength;
}
