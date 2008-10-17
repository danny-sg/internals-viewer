using System;

namespace InternalsViewer.Internals
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
