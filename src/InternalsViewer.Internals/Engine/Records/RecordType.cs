﻿
namespace InternalsViewer.Internals.Engine.Records;

public enum RecordType
{
    Primary = 0,
    Forwarded = 1,
    ForwardingStub = 2,
    Index = 3,
    Blob = 4,
    GhostIndex = 5,
    GhostData = 6,
    GhostRecordVersion = 7
}