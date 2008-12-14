
namespace InternalsViewer.Internals
{
    public class MarkItem
    {
        private int startPosition;
        private int length;
        private string propertyName;
        private string prefix;
        private int index = -1;

        public MarkItem(string propertName, int startPosition, int length)
        {
            this.PropertyName = propertName;
            this.StartPosition = startPosition;
            this.Length = length;
        }

        public MarkItem(string propertName, int startPosition, int length, int index)
            : this(propertName, startPosition, length)
        {
            this.Index = index;
        }

        public MarkItem(string propertName, string prefix, int startPosition, int length, int index)
            : this(propertName, startPosition, length, index)
        {
            this.Prefix = prefix;
        }

        public MarkItem(string propertyName, string prefix, int index)
        {
            this.PropertyName = propertyName;
            this.Prefix = prefix;
            this.Index = index;
            this.StartPosition = -1;

        }

        public int StartPosition
        {
            get { return this.startPosition; }
            set { this.startPosition = value; }
        }

        public int Length
        {
            get { return this.length; }
            set { this.length = value; }
        }

        public string PropertyName
        {
            get { return this.propertyName; }
            set { this.propertyName = value; }
        }

        public int Index
        {
            get { return this.index; }
            set { this.index = value; }
        }

        public string Prefix
        {
            get { return prefix; }
            set { prefix = value; }
        }
    }
}
