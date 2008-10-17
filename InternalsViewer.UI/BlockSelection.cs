using System.Drawing;

namespace InternalsViewer.UI
{
    public class BlockSelection
    {
        private Color colour;
        private int endPos;
        private int startPos;

        public BlockSelection()
        {
        }

        public BlockSelection(int startPos, int endPos)
        {
            this.startPos = startPos;
            this.endPos = endPos;
        }

        public BlockSelection(int startPos, int endPos, Color colour)
        {
            this.startPos = startPos;
            this.endPos = endPos;
            this.colour = colour;
        }

        public int StartPos
        {
            get { return startPos; }
            set { startPos = value; }
        }

        public int EndPos
        {
            get { return endPos; }
            set { endPos = value; }
        }

        public Color Colour
        {
            get { return colour; }
            set { colour = value; }
        }

        public bool HasColour
        {
            get { return colour != null; }
        }
    }
}
