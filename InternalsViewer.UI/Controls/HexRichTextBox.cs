using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace InternalsViewer.UI.Controls
{
    internal class HexRichTextBox : RichTextBox
    {
        private readonly List<BlockSelection> blocks = new List<BlockSelection>();
        private Size textLineSize;
        private Size textSize;

        public HexRichTextBox()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0xF)
            {
                base.WndProc(ref m);
                Graphics graphic = CreateGraphics();

                this.OnPaint(new PaintEventArgs(graphic, ClientRectangle));
                
                graphic.Dispose();
            }
            else
            {
                base.WndProc(ref m);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            foreach (BlockSelection block in blocks)
            {
                this.DrawBlock(e.Graphics, block);
            }
        }

        private void DrawBlock(Graphics graphics, BlockSelection block)
        {
            Point startPos = GetPositionFromCharIndex(block.StartPos * 3);
            Point endPos = GetPositionFromCharIndex((block.EndPos - 1) * 3);
            Pen blockPen = new Pen(block.Colour);

            int lines;

            if (startPos.Y != endPos.Y)
            {
                lines = ((endPos.Y - startPos.Y) / textSize.Height);

                Rectangle top;
                Rectangle middle;
                Rectangle bottom;

                top = new Rectangle(startPos.X - 2,
                                    startPos.Y,
                                    textLineSize.Width - startPos.X,
                                    textSize.Height);

                middle = new Rectangle(0,
                                       startPos.Y + textSize.Height,
                                       textLineSize.Width - 2,
                                       textSize.Height * (lines - 1));

                bottom = new Rectangle(0,
                                       endPos.Y,
                                       endPos.X + textSize.Width - 3,
                                       textSize.Height);

                GraphicsPath path = new GraphicsPath();

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
                                      startPos.Y + textLineSize.Height,
                                      textLineSize.Width - 3,
                                      startPos.Y + textLineSize.Height);

                    //Blank out middle-bottom line
                    graphics.DrawLine(Pens.White,
                                      1,
                                      endPos.Y,
                                      endPos.X + textSize.Width - 4,
                                      endPos.Y);
                }
                else
                {
                    if (endPos.X >= startPos.X)
                    {
                        //Blank out start<end line
                        graphics.DrawLine(Pens.White,
                                          startPos.X - 1,
                                          startPos.Y + textLineSize.Height,
                                          endPos.X + textSize.Width - 4,
                                          startPos.Y + textLineSize.Height);
                    }
                    else if (endPos.X + textSize.Width != startPos.X)
                    {
                        //Blank out start>end line
                        graphics.DrawLine(Pens.White,
                                          startPos.X - 4,
                                          startPos.Y + textLineSize.Height,
                                          endPos.X + textSize.Width - 1,
                                          startPos.Y + textLineSize.Height);
                    }
                }
            }
            else
            {
                lines = 1;

                Rectangle textRectange = new Rectangle(startPos.X < 3 ? 0 : startPos.X - 2,
                                                       startPos.Y,
                                                       (endPos.X - startPos.X + textSize.Width - 2) % textLineSize.Width,
                                                       lines * textSize.Height);

                graphics.DrawRectangle(blockPen, textRectange);
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

        public Size TextSize
        {
            get { return this.textSize; }
            set { this.textSize = value; }
        }

        public Size TextLineSize
        {
            get { return this.textLineSize; }
            set { this.textLineSize = value; }
        }

    }
}
