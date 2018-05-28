using System;
using System.Collections.Generic;
using System.Text;

namespace InternalsViewer.Internals.Structures
{
    public class IndexColumn : Column
    {
        public bool Key { get; set; }

        public bool IncludedColumn { get; set; }

        public int IndexColumnId { get; set; }
    }
}
