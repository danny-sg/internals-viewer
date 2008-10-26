using System;
using System.Collections.Generic;
using System.Text;

namespace InternalsViewer.Internals
{
    public class Markable
    {
        private List<MarkItem> markItems = new List<MarkItem>();

        public void Mark(string propertyName, int startPosition, int length)
        {
            this.markItems.Add(new MarkItem(propertyName, startPosition, length));
        }

        public void Mark(string propertyName, int startPosition, int length, int index)
        {
            this.markItems.Add(new MarkItem(propertyName, startPosition, length, index));
        }

        public void Mark(string propertyName, string prefix, int index)
        {
            this.markItems.Add(new MarkItem(propertyName, prefix, index));
        }

        public List<MarkItem> MarkItems
        {
            get { return this.markItems; }
        }
    }
}
