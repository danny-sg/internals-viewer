namespace InternalsViewer.Internals.Engine.Records.CdRecordType;

public enum CompressedRecordType
{
    Primary = 0,
    GhostEmpty = 1,
    Forwarding = 2,
    GhostData = 3,
    Forwarded = 4,
    GhostForwarded = 5,
    Index = 6,
    GhostIndex = 7
}