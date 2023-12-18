﻿using System.ComponentModel;
using System.Windows.Forms;
using InternalsViewer.UI.Controls;

namespace InternalsViewer.UI
{
    partial class HexViewer
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.headerPanel = new System.Windows.Forms.Panel();
            this.mainPanel = new System.Windows.Forms.Panel();
            this.dataRichTextBox = new InternalsViewer.UI.Controls.HexRichTextBox();
            this.dataContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.setOffsetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.findRecordToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.leftPanel = new System.Windows.Forms.Panel();
            this.addressLabel = new System.Windows.Forms.Label();
            this.dataToolTip = new System.Windows.Forms.ToolTip(this.components);
            this.addressContextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.hexNumericToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.mainPanel.SuspendLayout();
            this.dataContextMenuStrip.SuspendLayout();
            this.leftPanel.SuspendLayout();
            this.addressContextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // headerPanel
            // 
            this.headerPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.headerPanel.Location = new System.Drawing.Point(0, 0);
            this.headerPanel.Name = "headerPanel";
            this.headerPanel.Padding = new System.Windows.Forms.Padding(2, 1, 2, 0);
            this.headerPanel.Size = new System.Drawing.Size(428, 22);
            this.headerPanel.TabIndex = 2;
            this.headerPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.HeaderPanel_Paint);
            // 
            // mainPanel
            // 
            this.mainPanel.Controls.Add(this.dataRichTextBox);
            this.mainPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.mainPanel.Location = new System.Drawing.Point(53, 22);
            this.mainPanel.Name = "mainPanel";
            this.mainPanel.Padding = new System.Windows.Forms.Padding(8, 0, 0, 0);
            this.mainPanel.Size = new System.Drawing.Size(375, 412);
            this.mainPanel.TabIndex = 8;
            // 
            // dataRichTextBox
            // 
            this.dataRichTextBox.BackColor = System.Drawing.Color.White;
            this.dataRichTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dataRichTextBox.ContextMenuStrip = this.dataContextMenuStrip;
            this.dataRichTextBox.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.dataRichTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataRichTextBox.HideSelection = false;
            this.dataRichTextBox.Location = new System.Drawing.Point(8, 0);
            this.dataRichTextBox.Name = "dataRichTextBox";
            this.dataRichTextBox.ReadOnly = true;
            this.dataRichTextBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.dataRichTextBox.ShowSelectionMargin = true;
            this.dataRichTextBox.Size = new System.Drawing.Size(367, 412);
            this.dataRichTextBox.TabIndex = 0;
            this.dataRichTextBox.Text = "";
            this.dataRichTextBox.TextLineSize = new System.Drawing.Size(0, 0);
            this.dataRichTextBox.TextSize = new System.Drawing.Size(0, 0);
            this.dataRichTextBox.WordWrap = false;
            this.dataRichTextBox.VScroll += new System.EventHandler(this.DataRichTextBox_VScroll);
            this.dataRichTextBox.SelectionChanged += new System.EventHandler(this.DataRichTextBox_SelectionChanged);
            this.dataRichTextBox.Resize += new System.EventHandler(this.DataRichTextBox_Resize);
            this.dataRichTextBox.Leave += new System.EventHandler(this.DataRichTextBox_Leave);
            this.dataRichTextBox.MouseMove += new System.Windows.Forms.MouseEventHandler(this.DataRichTextBox_MouseMove);
            this.dataRichTextBox.MouseLeave += new System.EventHandler(this.DataRichTextBox_MouseLeave);
            // 
            // dataContextMenuStrip
            // 
            this.dataContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.setOffsetToolStripMenuItem,
            this.findRecordToolStripMenuItem});
            this.dataContextMenuStrip.Name = "dataContextMenuStrip";
            this.dataContextMenuStrip.Size = new System.Drawing.Size(153, 70);
            // 
            // setOffsetToolStripMenuItem
            // 
            this.setOffsetToolStripMenuItem.Name = "setOffsetToolStripMenuItem";
            this.setOffsetToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.setOffsetToolStripMenuItem.Text = "Set Offset";
            this.setOffsetToolStripMenuItem.Click += new System.EventHandler(this.SetOffsetToolStripMenuItem_Click);
            // 
            // findRecordToolStripMenuItem
            // 
            this.findRecordToolStripMenuItem.Name = "findRecordToolStripMenuItem";
            this.findRecordToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.findRecordToolStripMenuItem.Text = "Select Record";
            this.findRecordToolStripMenuItem.Click += new System.EventHandler(this.FindRecordToolStripMenuItem_Click);
            // 
            // leftPanel
            // 
            this.leftPanel.Controls.Add(this.addressLabel);
            this.leftPanel.Dock = System.Windows.Forms.DockStyle.Left;
            this.leftPanel.Location = new System.Drawing.Point(0, 22);
            this.leftPanel.Margin = new System.Windows.Forms.Padding(3, 3, 10, 3);
            this.leftPanel.Name = "leftPanel";
            this.leftPanel.Size = new System.Drawing.Size(53, 412);
            this.leftPanel.TabIndex = 9;
            this.leftPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.LeftPanel_Paint);
            // 
            // addressLabel
            // 
            this.addressLabel.BackColor = System.Drawing.Color.Transparent;
            this.addressLabel.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.addressLabel.ForeColor = System.Drawing.Color.Gray;
            this.addressLabel.Location = new System.Drawing.Point(-2, 5);
            this.addressLabel.Margin = new System.Windows.Forms.Padding(0);
            this.addressLabel.Name = "addressLabel";
            this.addressLabel.Size = new System.Drawing.Size(62, 402);
            this.addressLabel.TabIndex = 1;
            this.addressLabel.Text = "No Data";
            // 
            // dataToolTip
            // 
            this.dataToolTip.Active = false;
            this.dataToolTip.BackColor = System.Drawing.Color.WhiteSmoke;
            this.dataToolTip.ToolTipTitle = "Data Value";
            // 
            // addressContextMenuStrip
            // 
            this.addressContextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.hexNumericToolStripMenuItem});
            this.addressContextMenuStrip.Name = "addressContextMenuStrip";
            this.addressContextMenuStrip.Size = new System.Drawing.Size(94, 26);
            // 
            // hexNumericToolStripMenuItem
            // 
            this.hexNumericToolStripMenuItem.CheckOnClick = true;
            this.hexNumericToolStripMenuItem.Name = "hexNumericToolStripMenuItem";
            this.hexNumericToolStripMenuItem.Size = new System.Drawing.Size(93, 22);
            this.hexNumericToolStripMenuItem.Text = "Hex";
            // 
            // HexViewer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.Controls.Add(this.mainPanel);
            this.Controls.Add(this.leftPanel);
            this.Controls.Add(this.headerPanel);
            this.Name = "HexViewer";
            this.Size = new System.Drawing.Size(428, 434);
            this.mainPanel.ResumeLayout(false);
            this.dataContextMenuStrip.ResumeLayout(false);
            this.leftPanel.ResumeLayout(false);
            this.addressContextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private Panel headerPanel;
        private Panel mainPanel;
        private Panel leftPanel;
        private Label addressLabel;
        private HexRichTextBox dataRichTextBox;
        private ToolTip dataToolTip;
        private ContextMenuStrip dataContextMenuStrip;
        private ToolStripMenuItem setOffsetToolStripMenuItem;
        private ToolStripMenuItem findRecordToolStripMenuItem;
        private ContextMenuStrip addressContextMenuStrip;
        private ToolStripMenuItem hexNumericToolStripMenuItem;
    }
}
