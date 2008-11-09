using System;
using System.Collections.Generic;
using System.Text;

namespace InternalsViewer.Internals.Structures
{
    public class IndexColumn : Column
    {
        private bool includedColumn;
        private int indexColumnId;
        private int keyOrdinal;
        private bool key;
        private byte nullBit;

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

        public int KeyOrdinal
        {
            get { return keyOrdinal; }
            set { KeyOrdinal = value; }
        }

        public byte NullBit
        {
            get { return nullBit; }
            set { nullBit = value; }
        }
    }
}
