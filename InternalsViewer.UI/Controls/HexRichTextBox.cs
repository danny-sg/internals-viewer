using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

#pragma warning disable CA1416

namespace InternalsViewer.UI.Controls;

internal class HexRichTextBox : RichTextBox
{
    private readonly List<BlockSelection> blocks = new();

    public HexRichTextBox()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0xF)
        {
            base.WndProc(ref m);
            var graphic = CreateGraphics();

            OnPaint(new PaintEventArgs(graphic, ClientRectangle));

            graphic.Dispose();
        }
        else
        {
            base.WndProc(ref m);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        foreach (var block in blocks)
        {
            DrawBlock(e.Graphics, block);
        }
    }

    private void DrawBlock(Graphics graphics, BlockSelection block)
    {
        if(block.Colour==null)
        {
            return;
        }

        var startCharIndex = block.StartPos * 3;
        var endCharIndex = (block.EndPos * 3) - 2;

        var topPos = GetPositionFromCharIndex(0);
        var startPos = GetPositionFromCharIndex(startCharIndex);
        var endPos = GetPositionFromCharIndex(endCharIndex);

        var blockPen = new Pen(block.Colour.Value);

        int lines;

        if (startPos.Y != endPos.Y)
        {
            lines = ((endPos.Y - startPos.Y) / TextSize.Height);

            Rectangle top;

            if (startPos.Y == endPos.Y)
            {
                top = new Rectangle(startPos.X - 2, startPos.Y, endPos.X, TextSize.Height);
            }
            else
            {
                top = new Rectangle(startPos.X - 2,
                    startPos.Y,
                    3 + TextLineSize.Width - startPos.X,
                    TextSize.Height);
            }

            var middle = new Rectangle(topPos.X - 2,
                startPos.Y + TextSize.Height,
                3 + TextLineSize.Width - topPos.X,
                TextSize.Height * (lines - 1));

            var bottom = new Rectangle(topPos.X - 2,
                endPos.Y,
                endPos.X,
                TextSize.Height);

            var path = new GraphicsPath();

            path.AddRectangle(top);

            if (lines > 1)
            {
                path.AddRectangle(middle);
            }

            path.AddRectangle(bottom);

            graphics.DrawPath(blockPen, path);

            if (lines > 1)
            {
                //Blank out top-bottom line
                graphics.DrawLine(Pens.White,
                    top.X + 1,
                    startPos.Y + TextLineSize.Height,
                    TextLineSize.Width - 3,
                    startPos.Y + TextLineSize.Height);

                //Blank out middle-bottom line
                graphics.DrawLine(Pens.White,
                    1,
                    endPos.Y,
                    endPos.X + TextSize.Width - 4,
                    endPos.Y);
            }
            else
            {
                if (endPos.X >= startPos.X)
                {
                    //Blank out start<end line
                    graphics.DrawLine(Pens.White,
                        startPos.X - 1,
                        startPos.Y + TextLineSize.Height,
                        endPos.X + TextSize.Width - 4,
                        startPos.Y + TextLineSize.Height);
                }
                else if (endPos.X + TextSize.Width != startPos.X)
                {
                    //Blank out start>end line
                    graphics.DrawLine(Pens.White,
                        startPos.X - 4,
                        startPos.Y + TextLineSize.Height,
                        endPos.X + TextSize.Width - 1,
                        startPos.Y + TextLineSize.Height);
                }
            }
        }
        else
        {
            lines = 1;

            var textRectangle = new Rectangle(startPos.X < 3 ? 0 : startPos.X - 2,
                startPos.Y,
                (endPos.X - startPos.X + TextSize.Width - 2) % TextLineSize.Width,
                lines * TextSize.Height);

            graphics.DrawRectangle(blockPen, textRectangle);
        }
    }

    public void AddBlock(BlockSelection block)
    {
        blocks.Clear();
        blocks.Add(block);
    }

    protected override void OnVScroll(EventArgs e)
    {
        base.OnVScroll(e);
        Invalidate();
    }

    internal void ClearBlocks()
    {
        blocks.Clear();
    }

    public Size TextSize { get; set; }

    public Size TextLineSize { get; set; }
}