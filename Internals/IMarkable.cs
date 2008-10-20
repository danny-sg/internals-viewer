using System.Collections.Generic;

namespace InternalsViewer.Internals
{
    public interface IMarkable
    {
        void Mark(string propertyName, int startPosition, int length);

        void Mark(string propertyName, int startPosition, int length, int index);

        List<MarkItem> MarkItems
        {
            get;
        }

    }
}
