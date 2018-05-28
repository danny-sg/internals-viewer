using System.Drawing;

namespace InternalsViewer.UI
{
    public class BlockSelection
    {
        public BlockSelection()
        {
        }

        public BlockSelection(int startPos, int endPos)
        {
            this.StartPos = startPos;
            this.EndPos = endPos;
        }

        public BlockSelection(int startPos, int endPos, Color colour)
        {
            this.StartPos = startPos;
            this.EndPos = endPos;
            this.Colour = colour;
        }

        public int StartPos { get; set; }

        public int EndPos { get; set; }

        public Color Colour { get; set; }

        public bool HasColour
        {
            get { return Colour != null; }
        }
    }
}
