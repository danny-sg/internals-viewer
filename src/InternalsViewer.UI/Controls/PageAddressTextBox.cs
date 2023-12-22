﻿using System;
using System.Drawing;
using System.Windows.Forms;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.UI.Properties;

#pragma warning disable CA1416

namespace InternalsViewer.UI.Controls;

public sealed class PageAddressTextBox : ToolStripTextBox
{
    private ToolStripMenuItem? copyCommandToolStripMenuItem;
    private ToolStripMenuItem? copyToolStripMenuItem;
    private ToolStripMenuItem? option0HeaderOnlyToolStripMenuItem;
    private ToolStripMenuItem? option1ToolStripMenuItem;
    private ToolStripMenuItem? option2DataDumpToolStripMenuItem;
    private ToolStripMenuItem? option3RowInterpretationToolStripMenuItem;

    private ContextMenuStrip? pageAddressContextMenuStrip;

    private readonly string blankText = "(File Id:Page Id)";
    private readonly Color blankColour = Color.Gray;
    private readonly Color normalColour;


    public PageAddressTextBox()
    {
        normalColour = ForeColor;
            
        InitializeContextMenu();

        GotFocus += PageAddressTextBox_GotFocus;
        LostFocus += PageAddressTextBox_LostFocus;
        TextChanged += PageAddressTextBox_TextChanged;

        Text = blankText;

        PageAddressTextBox_LostFocus(null, EventArgs.Empty);
    }

    private void PageAddressTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (!Text.Equals(blankText))
        {
            ForeColor = normalColour;
        }
    }

    private void PageAddressTextBox_LostFocus(object? sender, EventArgs e)
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

    private void PageAddressTextBox_GotFocus(object? sender, EventArgs e)
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
        option3RowInterpretationToolStripMenuItem = new ToolStripMenuItem();

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
            option3RowInterpretationToolStripMenuItem
        });
        copyCommandToolStripMenuItem.Size = new Size(267, 22);
        copyCommandToolStripMenuItem.Text = "Copy DBCC PAGE command to Clipboard";
        copyCommandToolStripMenuItem.Click += CopyCommandToolStripMenuItem_Click;
        
        option0HeaderOnlyToolStripMenuItem.Size = new Size(211, 22);
        option0HeaderOnlyToolStripMenuItem.Text = "Option 0 - Header Only";
        option0HeaderOnlyToolStripMenuItem.Click += Option0ToolStripMenuItem_Click;

        option1ToolStripMenuItem.Size = new Size(211, 22);
        option1ToolStripMenuItem.Text = "Option 1 - Row";
        option1ToolStripMenuItem.Click += Option1ToolStripMenuItem_Click;

        option2DataDumpToolStripMenuItem.Size = new Size(211, 22);
        option2DataDumpToolStripMenuItem.Text = "Option 2 - Data Dump";
        option2DataDumpToolStripMenuItem.Click += Option2ToolStripMenuItem_Click;

        option3RowInterpretationToolStripMenuItem.Size = new Size(211, 22);
        option3RowInterpretationToolStripMenuItem.Text = "Option 3 - Row intepretation";
        option3RowInterpretationToolStripMenuItem.Click += Option3ToolStripMenuItem_Click;

        TextBox.ContextMenuStrip = pageAddressContextMenuStrip;
    }

    void CopyCommandToolStripMenuItem_Click(object? sender, EventArgs e)
    {
        Clipboard.SetText(PageAddressParser.Parse(Text).ToString());
    }

    private void Option0ToolStripMenuItem_Click(object? sender, EventArgs e)
    {
        CopyDbccPageToClipboard(0, PageAddressParser.Parse(Text));
    }

    private void Option1ToolStripMenuItem_Click(object? sender, EventArgs e)
    {
        CopyDbccPageToClipboard(1, PageAddressParser.Parse(Text));
    }

    private void Option2ToolStripMenuItem_Click(object? sender, EventArgs e)
    {
        CopyDbccPageToClipboard(2, PageAddressParser.Parse(Text));
    }

    private void Option3ToolStripMenuItem_Click(object? sender, EventArgs e)
    {
        CopyDbccPageToClipboard(3, PageAddressParser.Parse(Text));
    }

    public void CopyDbccPageToClipboard(int option, PageAddress address)
    {
        var text = string.Format(Resources.SQL_CopyDbccPage, DatabaseId, address.FileId, address.PageId, option);

        Clipboard.SetText(text);
    }

    public int DatabaseId { get; set; }
}