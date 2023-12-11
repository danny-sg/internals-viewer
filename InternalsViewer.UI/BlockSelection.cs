using System.Drawing;

namespace InternalsViewer.UI;

public class BlockSelection
{
    public BlockSelection()
    {
    }

    public BlockSelection(int startPos, int endPos)
    {
        StartPos = startPos;
        EndPos = endPos;
    }

    public BlockSelection(int startPos, int endPos, Color colour)
    {
        StartPos = startPos;
        EndPos = endPos;
        Colour = colour;
    }

    public int StartPos { get; set; }

    public int EndPos { get; set; }

    public Color? Colour { get; set; }

    public bool HasColour => Colour != null;
}