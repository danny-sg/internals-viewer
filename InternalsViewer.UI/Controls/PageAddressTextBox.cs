using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.UI.Controls
{
    public class PageAddressTextBox : ToolStripTextBox
    {
        private ToolStripMenuItem copyCommandToolStripMenuItem;
        private ToolStripMenuItem copyToolStripMenuItem;
        private ToolStripMenuItem option0HeaderOnlyToolStripMenuItem;
        private ToolStripMenuItem option1ToolStripMenuItem;
        private ToolStripMenuItem option2DataDumpToolStripMenuItem;
        private ToolStripMenuItem option3RowIntepretationToolStripMenuItem;
        private ContextMenuStrip pageAddressContextMenuStrip;

        private readonly string blankText = "(File Id:Page Id)";
        private readonly Color blankColour = Color.Gray;
        private readonly Color normalColour;


        public PageAddressTextBox()
        {
            normalColour = ForeColor;
            
            InitializeContextMenu();

            GotFocus += new EventHandler(PageAddressTextBox_GotFocus);
            LostFocus += new EventHandler(PageAddressTextBox_LostFocus);
            TextChanged += new EventHandler(PageAddressTextBox_TextChanged);

            Text = blankText;

            PageAddressTextBox_LostFocus(null, EventArgs.Empty);
        }

        void PageAddressTextBox_TextChanged(object sender, EventArgs e)
        {
            if (!Text.Equals(blankText))
            {
                ForeColor = normalColour;
            }
        }

        void PageAddressTextBox_LostFocus(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(Text) | Text.Equals(blankText))
            {
                Text = blankText;
                ForeColor = blankColour;
            }
            else
            {
                ForeColor = normalColour;
            }
        }

        void PageAddressTextBox_GotFocus(object sender, EventArgs e)
        {
            if (Text.Equals(blankText))
            {
                Text = string.Empty;
            }
            ForeColor = normalColour;

        }

        private void InitializeContextMenu()
        {
            pageAddressContextMenuStrip = new ContextMenuStrip();
            copyToolStripMenuItem = new ToolStripMenuItem();
            copyCommandToolStripMenuItem = new ToolStripMenuItem();
            option0HeaderOnlyToolStripMenuItem = new ToolStripMenuItem();
            option1ToolStripMenuItem = new ToolStripMenuItem();
            option2DataDumpToolStripMenuItem = new ToolStripMenuItem();
            option3RowIntepretationToolStripMenuItem = new ToolStripMenuItem();

            pageAddressContextMenuStrip.Items.AddRange(new ToolStripItem[]
                                                           {
                                                               copyToolStripMenuItem,
                                                               copyCommandToolStripMenuItem
                                                           });
            pageAddressContextMenuStrip.Size = new Size(268, 48);

            copyToolStripMenuItem.Size = new Size(267, 22);
            copyToolStripMenuItem.Text = "&Copy";

            copyCommandToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
                                                                    {
                                                                        option0HeaderOnlyToolStripMenuItem,
                                                                        option1ToolStripMenuItem,
                                                                        option2DataDumpToolStripMenuItem,
                                                                        option3RowIntepretationToolStripMenuItem
                                                                    });
            copyCommandToolStripMenuItem.Size = new Size(267, 22);
            copyCommandToolStripMenuItem.Text = "Copy DBCC PAGE command to Clipboard";
            copyCommandToolStripMenuItem.Click += new EventHandler(CopyCommandToolStripMenuItem_Click);
            option0HeaderOnlyToolStripMenuItem.Size = new Size(211, 22);
            option0HeaderOnlyToolStripMenuItem.Text = "Option 0 - Header Only";
            option0HeaderOnlyToolStripMenuItem.Click += Option0ToolStripMenuItem_Click;

            option1ToolStripMenuItem.Size = new Size(211, 22);
            option1ToolStripMenuItem.Text = "Option 1 - Row";
            option1ToolStripMenuItem.Click += Option1ToolStripMenuItem_Click;

            option2DataDumpToolStripMenuItem.Size = new Size(211, 22);
            option2DataDumpToolStripMenuItem.Text = "Option 2 - Data Dump";
            option2DataDumpToolStripMenuItem.Click += Option2ToolStripMenuItem_Click;

            option3RowIntepretationToolStripMenuItem.Size = new Size(211, 22);
            option3RowIntepretationToolStripMenuItem.Text = "Option 3 - Row intepretation";
            option3RowIntepretationToolStripMenuItem.Click += Option3ToolStripMenuItem_Click;

            TextBox.ContextMenuStrip = pageAddressContextMenuStrip;
        }

        void CopyCommandToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(PageAddress.Parse(Text).ToString());
        }

        private void Option0ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyDbccPageToClipboard(0, PageAddress.Parse(Text));
        }

        private void Option1ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyDbccPageToClipboard(1, PageAddress.Parse(Text));
        }

        private void Option2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyDbccPageToClipboard(2, PageAddress.Parse(Text));
        }

        private void Option3ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyDbccPageToClipboard(3, PageAddress.Parse(Text));
        }

        public void CopyDbccPageToClipboard(int option, PageAddress address)
        {
            var text = string.Format(Properties.Resources.SQL_CopyDbccPage, DatabaseId, address.FileId, address.PageId, option);

            Clipboard.SetText(text);
        }

        public int DatabaseId { get; set; }
    }
}
