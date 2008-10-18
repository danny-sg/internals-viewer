using System;

namespace InternalsViewer.Internals.Markers
{
    [Flags]
    public enum MarkerType
    {
        Undefined,
        PageAddress,
        VariableLengthField,
        FixedLengthField,
        Uniqueifier,
        Flag,
        Bitmap,
        Internal,
        Lob,
        Compressed
    }

}
