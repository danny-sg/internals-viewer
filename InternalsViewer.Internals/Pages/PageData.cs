using System;
using System.Collections.Generic;

namespace InternalsViewer.Internals.Pages;

public record PageData
{
    public byte[] Data { get; init; } = Array.Empty<byte>();

    public Dictionary<string, string> HeaderValues { get; init; } = new();
}