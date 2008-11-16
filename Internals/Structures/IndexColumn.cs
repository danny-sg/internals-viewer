using System;
using System.Collections.Generic;
using System.Text;

namespace InternalsViewer.Internals.Structures
{
    public class IndexColumn : Column
    {
        private bool includedColumn;
        private int indexColumnId;
        private bool key;

        public bool Key
        {
            get { return key; }
            set { key = value; }
        }

        public bool IncludedColumn
        {
            get { return includedColumn; }
            set { this.includedColumn = value; }
        }

        public int IndexColumnId
        {
            get { return indexColumnId; }
            set { indexColumnId = value; }
        }
    }
}
