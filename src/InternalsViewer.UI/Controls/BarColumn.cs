using System.Collections.Generic;
using System.Windows.Forms;

#pragma warning disable CA1416

namespace InternalsViewer.UI.Controls;

public sealed class BarColumn : DataGridViewImageColumn
{
    public BarColumn()
    {
        CellTemplate = new BarCell();
        ColourRanges = new List<ColourRange>();
        ValueType = typeof(decimal);
    }

    internal List<ColourRange> ColourRanges { get; set; }
}