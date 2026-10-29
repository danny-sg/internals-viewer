using System;

namespace InternalsViewer.UI.App.Models;

public sealed record HexLink(int Offset, int Length, string Text, string Command, Action Open)
{
    public bool Contains(int offset) => offset >= Offset && offset < Offset + Length;
}
