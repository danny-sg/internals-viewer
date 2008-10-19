using System;
using System.Collections.Generic;
using System.Text;

namespace InternalsViewer.UI.Markers
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
