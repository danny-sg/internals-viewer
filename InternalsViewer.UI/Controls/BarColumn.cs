using System.Collections.Generic;
using System.Windows.Forms;

namespace InternalsViewer.UI.Controls
{
    public class BarColumn : DataGridViewImageColumn
    {
        public BarColumn()
        {
            CellTemplate = new BarCell();
            this.ColourRanges = new List<ColourRange>();
            ValueType = typeof(decimal);
        }

        internal List<ColourRange> ColourRanges { get; set; }
    }
}
