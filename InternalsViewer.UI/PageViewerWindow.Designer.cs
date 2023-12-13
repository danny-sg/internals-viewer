﻿using System.ComponentModel;
using System.Windows.Forms;
using InternalsViewer.UI.Controls;

namespace InternalsViewer.UI
{
    partial class PageViewerWindow
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
            this.logContents0ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rowLogContents1ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rowLogContents2ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rowLogContents3ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.rowLogContents4ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.leftPanel = new System.Windows.Forms.Panel();
            this.headerBorderPanel = new InternalsViewer.UI.Controls.BorderPanel();
            this.pfsTextBox = new System.Windows.Forms.TextBox();
            this.bcmTextBox = new System.Windows.Forms.TextBox();
            this.dcmTextBox = new System.Windows.Forms.TextBox();
            this.sgamTextBox = new System.Windows.Forms.TextBox();
            this.gamTextBox = new System.Windows.Forms.TextBox();
            this.bcmPictureBox = new System.Windows.Forms.PictureBox();
            this.dcmPictureBox = new System.Windows.Forms.PictureBox();
            this.sGamPictureBox = new System.Windows.Forms.PictureBox();
            this.gamPictureBox = new System.Windows.Forms.PictureBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label19 = new System.Windows.Forms.Label();
            this.label20 = new System.Windows.Forms.Label();
            this.label21 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.pfsPanel = new System.Windows.Forms.Panel();
            this.label6 = new System.Windows.Forms.Label();
            this.textBox15 = new System.Windows.Forms.TextBox();
            this.pageBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.label5 = new System.Windows.Forms.Label();
            this.textBox4 = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.textBox3 = new System.Windows.Forms.TextBox();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.textBox14 = new System.Windows.Forms.TextBox();
            this.textBox13 = new System.Windows.Forms.TextBox();
            this.textBox12 = new System.Windows.Forms.TextBox();
            this.textBox11 = new System.Windows.Forms.TextBox();
            this.textBox10 = new System.Windows.Forms.TextBox();
            this.textBox9 = new System.Windows.Forms.TextBox();
            this.textBox8 = new System.Windows.Forms.TextBox();
            this.textBox7 = new System.Windows.Forms.TextBox();
            this.textBox6 = new System.Windows.Forms.TextBox();
            this.textBox17 = new System.Windows.Forms.TextBox();
            this.textBox16 = new System.Windows.Forms.TextBox();
            this.textBox5 = new System.Windows.Forms.TextBox();
            this.previousPageTextBox = new System.Windows.Forms.TextBox();
            this.nextPageTextBox = new System.Windows.Forms.TextBox();
            this.label18 = new System.Windows.Forms.Label();
            this.label16 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label36 = new System.Windows.Forms.Label();
            this.label17 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.hexViewer = new InternalsViewer.UI.HexViewer();
            this.topLeftPanel = new System.Windows.Forms.Panel();
            this.offsetTable = new InternalsViewer.UI.OffsetTable();
            this.compressionInfoPanel = new System.Windows.Forms.Panel();
            this.compressionInfoTable = new InternalsViewer.UI.Controls.CompressionInfoTable();
            this.allocationViewer = new InternalsViewer.UI.AllocationViewer();
            this.markerKeyTable = new InternalsViewer.UI.Controls.MarkerKeyTable();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.pageAddressToolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.errorImageToolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.errorToolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.serverToolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.dataaseToolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabel3 = new System.Windows.Forms.ToolStripStatusLabel();
            this.markerDescriptionToolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.offsetToolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.flatMenuStrip1 = new InternalsViewer.UI.Controls.FlatMenuStrip();
            this.toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            this.pageToolStripTextBox = new InternalsViewer.UI.Controls.PageAddressTextBox();
            this.previousToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.nextToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripLabel2 = new System.Windows.Forms.ToolStripLabel();
            this.offsetTableToolStripTextBox = new System.Windows.Forms.ToolStripTextBox();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.encodeAndFindToolStripButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.logToolStripLabel = new System.Windows.Forms.ToolStripLabel();
            this.logToolStripComboBox = new System.Windows.Forms.ToolStripComboBox();
            this.leftPanel.SuspendLayout();
            this.headerBorderPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bcmPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dcmPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sGamPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gamPictureBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.pageBindingSource)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.topLeftPanel.SuspendLayout();
            this.compressionInfoPanel.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.flatMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // logContents0ToolStripMenuItem
            // 
            this.logContents0ToolStripMenuItem.Name = "logContents0ToolStripMenuItem";
            this.logContents0ToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.logContents0ToolStripMenuItem.Text = "Row Log Contents 0";
            // 
            // rowLogContents1ToolStripMenuItem
            // 
            this.rowLogContents1ToolStripMenuItem.Name = "rowLogContents1ToolStripMenuItem";
            this.rowLogContents1ToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.rowLogContents1ToolStripMenuItem.Text = "Row Log Contents 1";
            // 
            // rowLogContents2ToolStripMenuItem
            // 
            this.rowLogContents2ToolStripMenuItem.Name = "rowLogContents2ToolStripMenuItem";
            this.rowLogContents2ToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.rowLogContents2ToolStripMenuItem.Text = "Row Log Contents 2";
            // 
            // rowLogContents3ToolStripMenuItem
            // 
            this.rowLogContents3ToolStripMenuItem.Name = "rowLogContents3ToolStripMenuItem";
            this.rowLogContents3ToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.rowLogContents3ToolStripMenuItem.Text = "Row Log Contents 3";
            // 
            // rowLogContents4ToolStripMenuItem
            // 
            this.rowLogContents4ToolStripMenuItem.Name = "rowLogContents4ToolStripMenuItem";
            this.rowLogContents4ToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.rowLogContents4ToolStripMenuItem.Text = "Row Log Contents 4";
            // 
            // leftPanel
            // 
            this.leftPanel.BackColor = System.Drawing.Color.Transparent;
            this.leftPanel.Controls.Add(this.headerBorderPanel);
            this.leftPanel.Dock = System.Windows.Forms.DockStyle.Left;
            this.leftPanel.Location = new System.Drawing.Point(0, 54);
            this.leftPanel.Margin = new System.Windows.Forms.Padding(6);
            this.leftPanel.Name = "leftPanel";
            this.leftPanel.Padding = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.leftPanel.Size = new System.Drawing.Size(600, 1107);
            this.leftPanel.TabIndex = 246;
            // 
            // headerBorderPanel
            // 
            this.headerBorderPanel.BackColor = System.Drawing.Color.White;
            this.headerBorderPanel.Controls.Add(this.pfsTextBox);
            this.headerBorderPanel.Controls.Add(this.bcmTextBox);
            this.headerBorderPanel.Controls.Add(this.dcmTextBox);
            this.headerBorderPanel.Controls.Add(this.sgamTextBox);
            this.headerBorderPanel.Controls.Add(this.gamTextBox);
            this.headerBorderPanel.Controls.Add(this.bcmPictureBox);
            this.headerBorderPanel.Controls.Add(this.dcmPictureBox);
            this.headerBorderPanel.Controls.Add(this.sGamPictureBox);
            this.headerBorderPanel.Controls.Add(this.gamPictureBox);
            this.headerBorderPanel.Controls.Add(this.label1);
            this.headerBorderPanel.Controls.Add(this.label19);
            this.headerBorderPanel.Controls.Add(this.label20);
            this.headerBorderPanel.Controls.Add(this.label21);
            this.headerBorderPanel.Controls.Add(this.label15);
            this.headerBorderPanel.Controls.Add(this.pfsPanel);
            this.headerBorderPanel.Controls.Add(this.label6);
            this.headerBorderPanel.Controls.Add(this.textBox15);
            this.headerBorderPanel.Controls.Add(this.label5);
            this.headerBorderPanel.Controls.Add(this.textBox4);
            this.headerBorderPanel.Controls.Add(this.label4);
            this.headerBorderPanel.Controls.Add(this.textBox3);
            this.headerBorderPanel.Controls.Add(this.textBox2);
            this.headerBorderPanel.Controls.Add(this.textBox14);
            this.headerBorderPanel.Controls.Add(this.textBox13);
            this.headerBorderPanel.Controls.Add(this.textBox12);
            this.headerBorderPanel.Controls.Add(this.textBox11);
            this.headerBorderPanel.Controls.Add(this.textBox10);
            this.headerBorderPanel.Controls.Add(this.textBox9);
            this.headerBorderPanel.Controls.Add(this.textBox8);
            this.headerBorderPanel.Controls.Add(this.textBox7);
            this.headerBorderPanel.Controls.Add(this.textBox6);
            this.headerBorderPanel.Controls.Add(this.textBox17);
            this.headerBorderPanel.Controls.Add(this.textBox16);
            this.headerBorderPanel.Controls.Add(this.textBox5);
            this.headerBorderPanel.Controls.Add(this.previousPageTextBox);
            this.headerBorderPanel.Controls.Add(this.nextPageTextBox);
            this.headerBorderPanel.Controls.Add(this.label18);
            this.headerBorderPanel.Controls.Add(this.label16);
            this.headerBorderPanel.Controls.Add(this.label14);
            this.headerBorderPanel.Controls.Add(this.label12);
            this.headerBorderPanel.Controls.Add(this.label11);
            this.headerBorderPanel.Controls.Add(this.label10);
            this.headerBorderPanel.Controls.Add(this.label36);
            this.headerBorderPanel.Controls.Add(this.label17);
            this.headerBorderPanel.Controls.Add(this.label13);
            this.headerBorderPanel.Controls.Add(this.label9);
            this.headerBorderPanel.Controls.Add(this.label8);
            this.headerBorderPanel.Controls.Add(this.label7);
            this.headerBorderPanel.Controls.Add(this.label3);
            this.headerBorderPanel.Controls.Add(this.label2);
            this.headerBorderPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.headerBorderPanel.Location = new System.Drawing.Point(0, 0);
            this.headerBorderPanel.Margin = new System.Windows.Forms.Padding(6);
            this.headerBorderPanel.Name = "headerBorderPanel";
            this.headerBorderPanel.Size = new System.Drawing.Size(594, 1107);
            this.headerBorderPanel.TabIndex = 0;
            // 
            // pfsTextBox
            // 
            this.pfsTextBox.BackColor = System.Drawing.Color.White;
            this.pfsTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.pfsTextBox.Cursor = System.Windows.Forms.Cursors.Hand;
            this.pfsTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.pfsTextBox.ForeColor = System.Drawing.Color.Blue;
            this.pfsTextBox.Location = new System.Drawing.Point(180, 967);
            this.pfsTextBox.Margin = new System.Windows.Forms.Padding(6);
            this.pfsTextBox.Name = "pfsTextBox";
            this.pfsTextBox.ReadOnly = true;
            this.pfsTextBox.Size = new System.Drawing.Size(150, 25);
            this.pfsTextBox.TabIndex = 267;
            this.pfsTextBox.MouseClick += new System.Windows.Forms.MouseEventHandler(this.PageTextBox_MouseClick);
            // 
            // bcmTextBox
            // 
            this.bcmTextBox.BackColor = System.Drawing.Color.White;
            this.bcmTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.bcmTextBox.Cursor = System.Windows.Forms.Cursors.Hand;
            this.bcmTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.bcmTextBox.ForeColor = System.Drawing.Color.Blue;
            this.bcmTextBox.Location = new System.Drawing.Point(180, 929);
            this.bcmTextBox.Margin = new System.Windows.Forms.Padding(6);
            this.bcmTextBox.Name = "bcmTextBox";
            this.bcmTextBox.ReadOnly = true;
            this.bcmTextBox.Size = new System.Drawing.Size(150, 25);
            this.bcmTextBox.TabIndex = 266;
            this.bcmTextBox.MouseClick += new System.Windows.Forms.MouseEventHandler(this.PageTextBox_MouseClick);
            // 
            // dcmTextBox
            // 
            this.dcmTextBox.BackColor = System.Drawing.Color.White;
            this.dcmTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dcmTextBox.Cursor = System.Windows.Forms.Cursors.Hand;
            this.dcmTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dcmTextBox.ForeColor = System.Drawing.Color.Blue;
            this.dcmTextBox.Location = new System.Drawing.Point(180, 890);
            this.dcmTextBox.Margin = new System.Windows.Forms.Padding(6);
            this.dcmTextBox.Name = "dcmTextBox";
            this.dcmTextBox.ReadOnly = true;
            this.dcmTextBox.Size = new System.Drawing.Size(150, 25);
            this.dcmTextBox.TabIndex = 265;
            this.dcmTextBox.MouseClick += new System.Windows.Forms.MouseEventHandler(this.PageTextBox_MouseClick);
            // 
            // sgamTextBox
            // 
            this.sgamTextBox.BackColor = System.Drawing.Color.White;
            this.sgamTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.sgamTextBox.Cursor = System.Windows.Forms.Cursors.Hand;
            this.sgamTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.sgamTextBox.ForeColor = System.Drawing.Color.Blue;
            this.sgamTextBox.Location = new System.Drawing.Point(180, 854);
            this.sgamTextBox.Margin = new System.Windows.Forms.Padding(6);
            this.sgamTextBox.Name = "sgamTextBox";
            this.sgamTextBox.ReadOnly = true;
            this.sgamTextBox.Size = new System.Drawing.Size(150, 25);
            this.sgamTextBox.TabIndex = 264;
            this.sgamTextBox.MouseClick += new System.Windows.Forms.MouseEventHandler(this.PageTextBox_MouseClick);
            // 
            // gamTextBox
            // 
            this.gamTextBox.BackColor = System.Drawing.Color.White;
            this.gamTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.gamTextBox.Cursor = System.Windows.Forms.Cursors.Hand;
            this.gamTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gamTextBox.ForeColor = System.Drawing.Color.Blue;
            this.gamTextBox.Location = new System.Drawing.Point(180, 817);
            this.gamTextBox.Margin = new System.Windows.Forms.Padding(6);
            this.gamTextBox.Name = "gamTextBox";
            this.gamTextBox.ReadOnly = true;
            this.gamTextBox.Size = new System.Drawing.Size(150, 25);
            this.gamTextBox.TabIndex = 263;
            this.gamTextBox.MouseClick += new System.Windows.Forms.MouseEventHandler(this.PageTextBox_MouseClick);
            // 
            // bcmPictureBox
            // 
            this.bcmPictureBox.Location = new System.Drawing.Point(96, 925);
            this.bcmPictureBox.Margin = new System.Windows.Forms.Padding(6);
            this.bcmPictureBox.Name = "bcmPictureBox";
            this.bcmPictureBox.Size = new System.Drawing.Size(32, 31);
            this.bcmPictureBox.TabIndex = 262;
            this.bcmPictureBox.TabStop = false;
            // 
            // dcmPictureBox
            // 
            this.dcmPictureBox.Location = new System.Drawing.Point(96, 888);
            this.dcmPictureBox.Margin = new System.Windows.Forms.Padding(6);
            this.dcmPictureBox.Name = "dcmPictureBox";
            this.dcmPictureBox.Size = new System.Drawing.Size(32, 31);
            this.dcmPictureBox.TabIndex = 261;
            this.dcmPictureBox.TabStop = false;
            // 
            // sGamPictureBox
            // 
            this.sGamPictureBox.Location = new System.Drawing.Point(96, 852);
            this.sGamPictureBox.Margin = new System.Windows.Forms.Padding(6);
            this.sGamPictureBox.Name = "sGamPictureBox";
            this.sGamPictureBox.Size = new System.Drawing.Size(32, 31);
            this.sGamPictureBox.TabIndex = 260;
            this.sGamPictureBox.TabStop = false;
            // 
            // gamPictureBox
            // 
            this.gamPictureBox.Location = new System.Drawing.Point(96, 815);
            this.gamPictureBox.Margin = new System.Windows.Forms.Padding(6);
            this.gamPictureBox.Name = "gamPictureBox";
            this.gamPictureBox.Size = new System.Drawing.Size(32, 31);
            this.gamPictureBox.TabIndex = 259;
            this.gamPictureBox.TabStop = false;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.BackColor = System.Drawing.Color.Transparent;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.Color.Gray;
            this.label1.Location = new System.Drawing.Point(10, 856);
            this.label1.Margin = new System.Windows.Forms.Padding(8);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(77, 26);
            this.label1.TabIndex = 258;
            this.label1.Text = "SGAM";
            // 
            // label19
            // 
            this.label19.AutoSize = true;
            this.label19.BackColor = System.Drawing.Color.Transparent;
            this.label19.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label19.ForeColor = System.Drawing.Color.Gray;
            this.label19.Location = new System.Drawing.Point(10, 929);
            this.label19.Margin = new System.Windows.Forms.Padding(8);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(61, 26);
            this.label19.TabIndex = 257;
            this.label19.Text = "BCM";
            // 
            // label20
            // 
            this.label20.AutoSize = true;
            this.label20.BackColor = System.Drawing.Color.Transparent;
            this.label20.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label20.ForeColor = System.Drawing.Color.Gray;
            this.label20.Location = new System.Drawing.Point(10, 819);
            this.label20.Margin = new System.Windows.Forms.Padding(8);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(62, 26);
            this.label20.TabIndex = 256;
            this.label20.Text = "GAM";
            // 
            // label21
            // 
            this.label21.AutoSize = true;
            this.label21.BackColor = System.Drawing.Color.Transparent;
            this.label21.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label21.ForeColor = System.Drawing.Color.Gray;
            this.label21.Location = new System.Drawing.Point(10, 892);
            this.label21.Margin = new System.Windows.Forms.Padding(8);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(62, 26);
            this.label21.TabIndex = 255;
            this.label21.Text = "DCM";
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.BackColor = System.Drawing.Color.Transparent;
            this.label15.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label15.ForeColor = System.Drawing.Color.Gray;
            this.label15.Location = new System.Drawing.Point(10, 967);
            this.label15.Margin = new System.Windows.Forms.Padding(8);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(55, 26);
            this.label15.TabIndex = 254;
            this.label15.Text = "PFS";
            this.label15.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // pfsPanel
            // 
            this.pfsPanel.BackColor = System.Drawing.Color.Transparent;
            this.pfsPanel.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.pfsPanel.Font = new System.Drawing.Font("Microsoft Sans Serif", 6F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.pfsPanel.Location = new System.Drawing.Point(96, 967);
            this.pfsPanel.Margin = new System.Windows.Forms.Padding(6);
            this.pfsPanel.Name = "pfsPanel";
            this.pfsPanel.Size = new System.Drawing.Size(66, 63);
            this.pfsPanel.TabIndex = 253;
            this.pfsPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.PfsPanel_Paint);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.BackColor = System.Drawing.Color.Transparent;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.ForeColor = System.Drawing.Color.Gray;
            this.label6.Location = new System.Drawing.Point(10, 671);
            this.label6.Margin = new System.Windows.Forms.Padding(8);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(155, 26);
            this.label6.TabIndex = 252;
            this.label6.Text = "Xact Reserved";
            // 
            // textBox15
            // 
            this.textBox15.BackColor = System.Drawing.Color.White;
            this.textBox15.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox15.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.pageBindingSource, "FreeCount", true));
            this.textBox15.Location = new System.Drawing.Point(180, 598);
            this.textBox15.Margin = new System.Windows.Forms.Padding(6);
            this.textBox15.Name = "textBox15";
            this.textBox15.ReadOnly = true;
            this.textBox15.Size = new System.Drawing.Size(154, 24);
            this.textBox15.TabIndex = 251;
            // 
            // pageBindingSource
            // 
            this.pageBindingSource.DataSource = typeof(InternalsViewer.Internals.Pages.Header);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.BackColor = System.Drawing.Color.Transparent;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.ForeColor = System.Drawing.Color.Gray;
            this.label5.Location = new System.Drawing.Point(10, 598);
            this.label5.Margin = new System.Windows.Forms.Padding(8);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(117, 26);
            this.label5.TabIndex = 250;
            this.label5.Text = "Free Bytes";
            // 
            // textBox4
            // 
            this.textBox4.BackColor = System.Drawing.Color.White;
            this.textBox4.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox4.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.pageBindingSource, "Lsn", true));
            this.textBox4.Location = new System.Drawing.Point(180, 377);
            this.textBox4.Margin = new System.Windows.Forms.Padding(6);
            this.textBox4.Name = "textBox4";
            this.textBox4.ReadOnly = true;
            this.textBox4.Size = new System.Drawing.Size(154, 24);
            this.textBox4.TabIndex = 249;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.BackColor = System.Drawing.Color.Transparent;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.ForeColor = System.Drawing.Color.Gray;
            this.label4.Location = new System.Drawing.Point(10, 377);
            this.label4.Margin = new System.Windows.Forms.Padding(8);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(55, 26);
            this.label4.TabIndex = 248;
            this.label4.Text = "LSN";
            // 
            // textBox3
            // 
            this.textBox3.BackColor = System.Drawing.Color.White;
            this.textBox3.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox3.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.pageBindingSource, "AllocationUnit", true));
            this.textBox3.Location = new System.Drawing.Point(16, 192);
            this.textBox3.Margin = new System.Windows.Forms.Padding(6);
            this.textBox3.Name = "textBox3";
            this.textBox3.ReadOnly = true;
            this.textBox3.Size = new System.Drawing.Size(560, 24);
            this.textBox3.TabIndex = 247;
            // 
            // textBox2
            // 
            this.textBox2.BackColor = System.Drawing.Color.White;
            this.textBox2.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox2.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.pageBindingSource, "PageTypeName", true));
            this.textBox2.Location = new System.Drawing.Point(16, 46);
            this.textBox2.Margin = new System.Windows.Forms.Padding(6);
            this.textBox2.Name = "textBox2";
            this.textBox2.ReadOnly = true;
            this.textBox2.Size = new System.Drawing.Size(560, 24);
            this.textBox2.TabIndex = 246;
            // 
            // textBox14
            // 
            this.textBox14.BackColor = System.Drawing.Color.White;
            this.textBox14.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox14.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.pageBindingSource, "FlagBits", true));
            this.textBox14.Location = new System.Drawing.Point(180, 744);
            this.textBox14.Margin = new System.Windows.Forms.Padding(6);
            this.textBox14.Name = "textBox14";
            this.textBox14.ReadOnly = true;
            this.textBox14.Size = new System.Drawing.Size(154, 24);
            this.textBox14.TabIndex = 245;
            // 
            // textBox13
            // 
            this.textBox13.BackColor = System.Drawing.Color.White;
            this.textBox13.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox13.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.pageBindingSource, "ReservedCount", true));
            this.textBox13.Location = new System.Drawing.Point(180, 635);
            this.textBox13.Margin = new System.Windows.Forms.Padding(6);
            this.textBox13.Name = "textBox13";
            this.textBox13.ReadOnly = true;
            this.textBox13.Size = new System.Drawing.Size(154, 24);
            this.textBox13.TabIndex = 244;
            // 
            // textBox12
            // 
            this.textBox12.BackColor = System.Drawing.Color.White;
            this.textBox12.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox12.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.pageBindingSource, "XactReservedCount", true));
            this.textBox12.Location = new System.Drawing.Point(180, 671);
            this.textBox12.Margin = new System.Windows.Forms.Padding(6);
            this.textBox12.Name = "textBox12";
            this.textBox12.ReadOnly = true;
            this.textBox12.Size = new System.Drawing.Size(154, 24);
            this.textBox12.TabIndex = 243;
            // 
            // textBox11
            // 
            this.textBox11.BackColor = System.Drawing.Color.White;
            this.textBox11.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox11.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.pageBindingSource, "IndexId", true));
            this.textBox11.Location = new System.Drawing.Point(180, 488);
            this.textBox11.Margin = new System.Windows.Forms.Padding(6);
            this.textBox11.Name = "textBox11";
            this.textBox11.ReadOnly = true;
            this.textBox11.Size = new System.Drawing.Size(154, 24);
            this.textBox11.TabIndex = 242;
            // 
            // textBox10
            // 
            this.textBox10.BackColor = System.Drawing.Color.White;
            this.textBox10.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox10.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.pageBindingSource, "Level", true));
            this.textBox10.Location = new System.Drawing.Point(180, 452);
            this.textBox10.Margin = new System.Windows.Forms.Padding(6);
            this.textBox10.Name = "textBox10";
            this.textBox10.ReadOnly = true;
            this.textBox10.Size = new System.Drawing.Size(154, 24);
            this.textBox10.TabIndex = 241;
            // 
            // textBox9
            // 
            this.textBox9.BackColor = System.Drawing.Color.White;
            this.textBox9.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox9.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.pageBindingSource, "SlotCount", true));
            this.textBox9.Location = new System.Drawing.Point(180, 415);
            this.textBox9.Margin = new System.Windows.Forms.Padding(6);
            this.textBox9.Name = "textBox9";
            this.textBox9.ReadOnly = true;
            this.textBox9.Size = new System.Drawing.Size(154, 24);
            this.textBox9.TabIndex = 240;
            // 
            // textBox8
            // 
            this.textBox8.BackColor = System.Drawing.Color.White;
            this.textBox8.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox8.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.pageBindingSource, "TornBits", true));
            this.textBox8.Location = new System.Drawing.Point(180, 708);
            this.textBox8.Margin = new System.Windows.Forms.Padding(6);
            this.textBox8.Name = "textBox8";
            this.textBox8.ReadOnly = true;
            this.textBox8.Size = new System.Drawing.Size(154, 24);
            this.textBox8.TabIndex = 239;
            // 
            // textBox7
            // 
            this.textBox7.BackColor = System.Drawing.Color.White;
            this.textBox7.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox7.Location = new System.Drawing.Point(180, 562);
            this.textBox7.Margin = new System.Windows.Forms.Padding(6);
            this.textBox7.Name = "textBox7";
            this.textBox7.ReadOnly = true;
            this.textBox7.Size = new System.Drawing.Size(154, 24);
            this.textBox7.TabIndex = 238;
            // 
            // textBox6
            // 
            this.textBox6.BackColor = System.Drawing.Color.White;
            this.textBox6.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox6.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.pageBindingSource, "FreeData", true));
            this.textBox6.Location = new System.Drawing.Point(180, 525);
            this.textBox6.Margin = new System.Windows.Forms.Padding(6);
            this.textBox6.Name = "textBox6";
            this.textBox6.ReadOnly = true;
            this.textBox6.Size = new System.Drawing.Size(154, 24);
            this.textBox6.TabIndex = 237;
            // 
            // textBox17
            // 
            this.textBox17.BackColor = System.Drawing.Color.White;
            this.textBox17.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox17.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.pageBindingSource, "AllocationUnitId", true));
            this.textBox17.Location = new System.Drawing.Point(16, 338);
            this.textBox17.Margin = new System.Windows.Forms.Padding(6);
            this.textBox17.Name = "textBox17";
            this.textBox17.ReadOnly = true;
            this.textBox17.Size = new System.Drawing.Size(560, 24);
            this.textBox17.TabIndex = 236;
            // 
            // textBox16
            // 
            this.textBox16.BackColor = System.Drawing.Color.White;
            this.textBox16.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox16.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.pageBindingSource, "PartitionId", true));
            this.textBox16.Location = new System.Drawing.Point(16, 262);
            this.textBox16.Margin = new System.Windows.Forms.Padding(6);
            this.textBox16.Name = "textBox16";
            this.textBox16.ReadOnly = true;
            this.textBox16.Size = new System.Drawing.Size(560, 24);
            this.textBox16.TabIndex = 235;
            // 
            // textBox5
            // 
            this.textBox5.BackColor = System.Drawing.Color.White;
            this.textBox5.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox5.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.pageBindingSource, "ObjectId", true));
            this.textBox5.Location = new System.Drawing.Point(180, 156);
            this.textBox5.Margin = new System.Windows.Forms.Padding(6);
            this.textBox5.Name = "textBox5";
            this.textBox5.ReadOnly = true;
            this.textBox5.Size = new System.Drawing.Size(150, 24);
            this.textBox5.TabIndex = 234;
            // 
            // previousPageTextBox
            // 
            this.previousPageTextBox.BackColor = System.Drawing.Color.White;
            this.previousPageTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.previousPageTextBox.Cursor = System.Windows.Forms.Cursors.Hand;
            this.previousPageTextBox.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.pageBindingSource, "PreviousPage", true));
            this.previousPageTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.previousPageTextBox.ForeColor = System.Drawing.Color.Blue;
            this.previousPageTextBox.Location = new System.Drawing.Point(180, 119);
            this.previousPageTextBox.Margin = new System.Windows.Forms.Padding(6);
            this.previousPageTextBox.Name = "previousPageTextBox";
            this.previousPageTextBox.ReadOnly = true;
            this.previousPageTextBox.Size = new System.Drawing.Size(150, 25);
            this.previousPageTextBox.TabIndex = 233;
            this.previousPageTextBox.MouseClick += new System.Windows.Forms.MouseEventHandler(this.PageTextBox_MouseClick);
            // 
            // nextPageTextBox
            // 
            this.nextPageTextBox.BackColor = System.Drawing.Color.White;
            this.nextPageTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.nextPageTextBox.Cursor = System.Windows.Forms.Cursors.Hand;
            this.nextPageTextBox.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.pageBindingSource, "NextPage", true));
            this.nextPageTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.nextPageTextBox.ForeColor = System.Drawing.Color.Blue;
            this.nextPageTextBox.Location = new System.Drawing.Point(180, 83);
            this.nextPageTextBox.Margin = new System.Windows.Forms.Padding(6);
            this.nextPageTextBox.Name = "nextPageTextBox";
            this.nextPageTextBox.ReadOnly = true;
            this.nextPageTextBox.Size = new System.Drawing.Size(150, 25);
            this.nextPageTextBox.TabIndex = 232;
            this.nextPageTextBox.MouseClick += new System.Windows.Forms.MouseEventHandler(this.PageTextBox_MouseClick);
            // 
            // label18
            // 
            this.label18.AutoSize = true;
            this.label18.BackColor = System.Drawing.Color.Transparent;
            this.label18.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label18.ForeColor = System.Drawing.Color.Gray;
            this.label18.Location = new System.Drawing.Point(10, 744);
            this.label18.Margin = new System.Windows.Forms.Padding(8);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(97, 26);
            this.label18.TabIndex = 231;
            this.label18.Text = "Flag Bits";
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.BackColor = System.Drawing.Color.Transparent;
            this.label16.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label16.ForeColor = System.Drawing.Color.Gray;
            this.label16.Location = new System.Drawing.Point(10, 635);
            this.label16.Margin = new System.Windows.Forms.Padding(8);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(166, 26);
            this.label16.TabIndex = 230;
            this.label16.Text = "Reserved Bytes";
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.BackColor = System.Drawing.Color.Transparent;
            this.label14.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label14.ForeColor = System.Drawing.Color.Gray;
            this.label14.Location = new System.Drawing.Point(10, 562);
            this.label14.Margin = new System.Windows.Forms.Padding(8);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(137, 26);
            this.label14.TabIndex = 229;
            this.label14.Text = "Fixed Length";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.BackColor = System.Drawing.Color.Transparent;
            this.label12.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label12.ForeColor = System.Drawing.Color.Gray;
            this.label12.Location = new System.Drawing.Point(10, 488);
            this.label12.Margin = new System.Windows.Forms.Padding(8);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(89, 26);
            this.label12.TabIndex = 228;
            this.label12.Text = "Index Id";
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.BackColor = System.Drawing.Color.Transparent;
            this.label11.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label11.ForeColor = System.Drawing.Color.Gray;
            this.label11.Location = new System.Drawing.Point(10, 452);
            this.label11.Margin = new System.Windows.Forms.Padding(8);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(64, 26);
            this.label11.TabIndex = 227;
            this.label11.Text = "Level";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.BackColor = System.Drawing.Color.Transparent;
            this.label10.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label10.ForeColor = System.Drawing.Color.Gray;
            this.label10.Location = new System.Drawing.Point(10, 415);
            this.label10.Margin = new System.Windows.Forms.Padding(8);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(114, 26);
            this.label10.TabIndex = 226;
            this.label10.Text = "Slot Count";
            // 
            // label36
            // 
            this.label36.AutoSize = true;
            this.label36.BackColor = System.Drawing.Color.Transparent;
            this.label36.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label36.ForeColor = System.Drawing.Color.Gray;
            this.label36.Location = new System.Drawing.Point(10, 8);
            this.label36.Margin = new System.Windows.Forms.Padding(8);
            this.label36.Name = "label36";
            this.label36.Size = new System.Drawing.Size(116, 26);
            this.label36.TabIndex = 225;
            this.label36.Text = "Page Type";
            // 
            // label17
            // 
            this.label17.AutoSize = true;
            this.label17.BackColor = System.Drawing.Color.Transparent;
            this.label17.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label17.ForeColor = System.Drawing.Color.Gray;
            this.label17.Location = new System.Drawing.Point(10, 708);
            this.label17.Margin = new System.Windows.Forms.Padding(8);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(98, 26);
            this.label17.TabIndex = 224;
            this.label17.Text = "Torn Bits";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.BackColor = System.Drawing.Color.Transparent;
            this.label13.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label13.ForeColor = System.Drawing.Color.Gray;
            this.label13.Location = new System.Drawing.Point(10, 525);
            this.label13.Margin = new System.Windows.Forms.Padding(8);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(108, 26);
            this.label13.TabIndex = 223;
            this.label13.Text = "Free Data";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.BackColor = System.Drawing.Color.Transparent;
            this.label9.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label9.ForeColor = System.Drawing.Color.Gray;
            this.label9.Location = new System.Drawing.Point(10, 300);
            this.label9.Margin = new System.Windows.Forms.Padding(8);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(135, 26);
            this.label9.TabIndex = 222;
            this.label9.Text = "Alloc. Unit Id";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.BackColor = System.Drawing.Color.Transparent;
            this.label8.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label8.ForeColor = System.Drawing.Color.Gray;
            this.label8.Location = new System.Drawing.Point(10, 227);
            this.label8.Margin = new System.Windows.Forms.Padding(8);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(116, 26);
            this.label8.TabIndex = 221;
            this.label8.Text = "Partition Id";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.BackColor = System.Drawing.Color.Transparent;
            this.label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label7.ForeColor = System.Drawing.Color.Gray;
            this.label7.Location = new System.Drawing.Point(10, 156);
            this.label7.Margin = new System.Windows.Forms.Padding(8);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(103, 26);
            this.label7.TabIndex = 220;
            this.label7.Text = "Object ID";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.BackColor = System.Drawing.Color.Transparent;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.ForeColor = System.Drawing.Color.Gray;
            this.label3.Location = new System.Drawing.Point(10, 119);
            this.label3.Margin = new System.Windows.Forms.Padding(8);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(120, 26);
            this.label3.TabIndex = 219;
            this.label3.Text = "Prev. Page";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.BackColor = System.Drawing.Color.Transparent;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.ForeColor = System.Drawing.Color.Gray;
            this.label2.Location = new System.Drawing.Point(10, 83);
            this.label2.Margin = new System.Windows.Forms.Padding(8);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(114, 26);
            this.label2.TabIndex = 218;
            this.label2.Text = "Next Page";
            // 
            // splitContainer1
            // 
            this.splitContainer1.BackColor = System.Drawing.Color.Transparent;
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(600, 54);
            this.splitContainer1.Margin = new System.Windows.Forms.Padding(6);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.BackColor = System.Drawing.Color.Transparent;
            this.splitContainer1.Panel1.Controls.Add(this.hexViewer);
            this.splitContainer1.Panel1.Controls.Add(this.topLeftPanel);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.BackColor = System.Drawing.Color.White;
            this.splitContainer1.Panel2.Controls.Add(this.allocationViewer);
            this.splitContainer1.Panel2.Controls.Add(this.markerKeyTable);
            this.splitContainer1.Size = new System.Drawing.Size(1148, 1107);
            this.splitContainer1.SplitterDistance = 558;
            this.splitContainer1.SplitterWidth = 8;
            this.splitContainer1.TabIndex = 247;
            // 
            // hexViewer
            // 
            this.hexViewer.AddressHex = false;
            this.hexViewer.BackColor = System.Drawing.Color.White;
            this.hexViewer.ColourAndOffsetDictionary = null;
            this.hexViewer.Colourise = true;
            this.hexViewer.DataRtf = null;
            this.hexViewer.DataText = null;
            this.hexViewer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.hexViewer.Font = new System.Drawing.Font("Courier New", 8.25F);
            this.hexViewer.Location = new System.Drawing.Point(0, 0);
            this.hexViewer.Margin = new System.Windows.Forms.Padding(2, 0, 0, 0);
            this.hexViewer.Name = "hexViewer";
            this.hexViewer.Padding = new System.Windows.Forms.Padding(2, 0, 2, 2);
            this.hexViewer.Page = null;
            this.hexViewer.SelectedOffset = -1;
            this.hexViewer.SelectedRecord = -1;
            this.hexViewer.Size = new System.Drawing.Size(788, 558);
            this.hexViewer.TabIndex = 0;
            this.hexViewer.OffsetOver += new System.EventHandler<InternalsViewer.UI.OffsetEventArgs>(this.HexViewer_OffsetOver);
            this.hexViewer.OffsetSet += new System.EventHandler<InternalsViewer.UI.OffsetEventArgs>(this.HexViewer_OffsetSet);
            this.hexViewer.RecordFind += new System.EventHandler<InternalsViewer.UI.OffsetEventArgs>(this.HexViewer_RecordFind);
            // 
            // topLeftPanel
            // 
            this.topLeftPanel.BackColor = System.Drawing.Color.Transparent;
            this.topLeftPanel.Controls.Add(this.offsetTable);
            this.topLeftPanel.Controls.Add(this.compressionInfoPanel);
            this.topLeftPanel.Dock = System.Windows.Forms.DockStyle.Right;
            this.topLeftPanel.Location = new System.Drawing.Point(788, 0);
            this.topLeftPanel.Margin = new System.Windows.Forms.Padding(6);
            this.topLeftPanel.Name = "topLeftPanel";
            this.topLeftPanel.Padding = new System.Windows.Forms.Padding(6, 0, 0, 0);
            this.topLeftPanel.Size = new System.Drawing.Size(360, 558);
            this.topLeftPanel.TabIndex = 1;
            // 
            // offsetTable
            // 
            this.offsetTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.offsetTable.Location = new System.Drawing.Point(6, 0);
            this.offsetTable.Margin = new System.Windows.Forms.Padding(12);
            this.offsetTable.Name = "offsetTable";
            this.offsetTable.Padding = new System.Windows.Forms.Padding(2);
            this.offsetTable.Page = null;
            this.offsetTable.SelectedSlot = -1;
            this.offsetTable.Size = new System.Drawing.Size(354, 354);
            this.offsetTable.TabIndex = 0;
            this.offsetTable.SlotChanged += new System.EventHandler(this.OffsetTable_SlotChanged);
            // 
            // compressionInfoPanel
            // 
            this.compressionInfoPanel.Controls.Add(this.compressionInfoTable);
            this.compressionInfoPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.compressionInfoPanel.Location = new System.Drawing.Point(6, 354);
            this.compressionInfoPanel.Margin = new System.Windows.Forms.Padding(6);
            this.compressionInfoPanel.Name = "compressionInfoPanel";
            this.compressionInfoPanel.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
            this.compressionInfoPanel.Size = new System.Drawing.Size(354, 204);
            this.compressionInfoPanel.TabIndex = 2;
            // 
            // compressionInfoTable
            // 
            this.compressionInfoTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.compressionInfoTable.Location = new System.Drawing.Point(0, 8);
            this.compressionInfoTable.Margin = new System.Windows.Forms.Padding(12);
            this.compressionInfoTable.Name = "compressionInfoTable";
            this.compressionInfoTable.Padding = new System.Windows.Forms.Padding(2);
            this.compressionInfoTable.SelectedStructure = InternalsViewer.Internals.Compression.CompressionInfoStructure.Header;
            this.compressionInfoTable.Size = new System.Drawing.Size(354, 196);
            this.compressionInfoTable.TabIndex = 0;
            this.compressionInfoTable.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(this.CompressionInfoTable_PropertyChanged);
            // 
            // allocationViewer
            // 
            this.allocationViewer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.allocationViewer.Location = new System.Drawing.Point(0, 0);
            this.allocationViewer.Margin = new System.Windows.Forms.Padding(12);
            this.allocationViewer.Name = "allocationViewer";
            this.allocationViewer.Size = new System.Drawing.Size(1148, 541);
            this.allocationViewer.TabIndex = 1;
            this.allocationViewer.Visible = false;
            this.allocationViewer.PageOver += new System.EventHandler<PageEventArgs>(this.AllocationViewer_PageOver);
            this.allocationViewer.PageClicked += new System.EventHandler<PageEventArgs>(this.AllocationViewer_PageClicked);
            // 
            // markerKeyTable
            // 
            this.markerKeyTable.BackColor = System.Drawing.Color.White;
            this.markerKeyTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.markerKeyTable.Location = new System.Drawing.Point(0, 0);
            this.markerKeyTable.Margin = new System.Windows.Forms.Padding(12);
            this.markerKeyTable.Name = "markerKeyTable";
            this.markerKeyTable.Padding = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.markerKeyTable.Size = new System.Drawing.Size(1148, 541);
            this.markerKeyTable.TabIndex = 0;
            this.markerKeyTable.SelectionChanged += new System.EventHandler(this.MarkerKeyTable_SelectionChanged);
            this.markerKeyTable.SelectionClicked += new System.EventHandler(this.MarkerKeyTable_SelectionClicked);
            this.markerKeyTable.PageNavigated += new System.EventHandler<PageEventArgs>(this.MarkerKeyTable_PageNavigated);
            // 
            // statusStrip
            // 
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.pageAddressToolStripStatusLabel,
            this.errorImageToolStripStatusLabel,
            this.errorToolStripStatusLabel,
            this.serverToolStripStatusLabel,
            this.dataaseToolStripStatusLabel,
            this.toolStripStatusLabel3,
            this.markerDescriptionToolStripStatusLabel,
            this.offsetToolStripStatusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 1161);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Padding = new System.Windows.Forms.Padding(2, 0, 28, 0);
            this.statusStrip.Size = new System.Drawing.Size(1748, 37);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 201;
            this.statusStrip.Text = "statusStrip";
            // 
            // pageAddressToolStripStatusLabel
            // 
            this.pageAddressToolStripStatusLabel.Name = "pageAddressToolStripStatusLabel";
            this.pageAddressToolStripStatusLabel.Size = new System.Drawing.Size(0, 32);
            // 
            // errorImageToolStripStatusLabel
            // 
            this.errorImageToolStripStatusLabel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.errorImageToolStripStatusLabel.Name = "errorImageToolStripStatusLabel";
            this.errorImageToolStripStatusLabel.Size = new System.Drawing.Size(0, 32);
            this.errorImageToolStripStatusLabel.Text = "Error Image";
            // 
            // errorToolStripStatusLabel
            // 
            this.errorToolStripStatusLabel.Name = "errorToolStripStatusLabel";
            this.errorToolStripStatusLabel.Size = new System.Drawing.Size(0, 32);
            // 
            // serverToolStripStatusLabel
            // 
            this.serverToolStripStatusLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.serverToolStripStatusLabel.Name = "serverToolStripStatusLabel";
            this.serverToolStripStatusLabel.Size = new System.Drawing.Size(96, 32);
            this.serverToolStripStatusLabel.Text = "[Server]";
            // 
            // dataaseToolStripStatusLabel
            // 
            this.dataaseToolStripStatusLabel.ForeColor = System.Drawing.Color.Gray;
            this.dataaseToolStripStatusLabel.Name = "dataaseToolStripStatusLabel";
            this.dataaseToolStripStatusLabel.Size = new System.Drawing.Size(127, 32);
            this.dataaseToolStripStatusLabel.Text = "[Database]";
            // 
            // toolStripStatusLabel3
            // 
            this.toolStripStatusLabel3.Name = "toolStripStatusLabel3";
            this.toolStripStatusLabel3.Size = new System.Drawing.Size(1495, 32);
            this.toolStripStatusLabel3.Spring = true;
            // 
            // markerDescriptionToolStripStatusLabel
            // 
            this.markerDescriptionToolStripStatusLabel.AutoToolTip = true;
            this.markerDescriptionToolStripStatusLabel.BackColor = System.Drawing.Color.Red;
            this.markerDescriptionToolStripStatusLabel.ImageTransparentColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.markerDescriptionToolStripStatusLabel.Name = "markerDescriptionToolStripStatusLabel";
            this.markerDescriptionToolStripStatusLabel.Size = new System.Drawing.Size(0, 32);
            // 
            // offsetToolStripStatusLabel
            // 
            this.offsetToolStripStatusLabel.Name = "offsetToolStripStatusLabel";
            this.offsetToolStripStatusLabel.Size = new System.Drawing.Size(0, 32);
            // 
            // flatMenuStrip1
            // 
            this.flatMenuStrip1.AutoSize = false;
            this.flatMenuStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.flatMenuStrip1.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.flatMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripLabel1,
            this.pageToolStripTextBox,
            this.previousToolStripButton,
            this.nextToolStripButton,
            this.toolStripSeparator2,
            this.toolStripLabel2,
            this.offsetTableToolStripTextBox,
            this.toolStripSeparator1,
            this.encodeAndFindToolStripButton,
            this.toolStripSeparator3,
            this.logToolStripLabel,
            this.logToolStripComboBox});
            this.flatMenuStrip1.Location = new System.Drawing.Point(0, 0);
            this.flatMenuStrip1.Name = "flatMenuStrip1";
            this.flatMenuStrip1.Padding = new System.Windows.Forms.Padding(0, 0, 2, 0);
            this.flatMenuStrip1.Size = new System.Drawing.Size(1748, 54);
            this.flatMenuStrip1.TabIndex = 0;
            this.flatMenuStrip1.Text = "flatMenuStrip1";
            // 
            // toolStripLabel1
            // 
            this.toolStripLabel1.Name = "toolStripLabel1";
            this.toolStripLabel1.Size = new System.Drawing.Size(71, 51);
            this.toolStripLabel1.Text = "Page:";
            // 
            // pageToolStripTextBox
            // 
            this.pageToolStripTextBox.AutoSize = false;
            this.pageToolStripTextBox.DatabaseId = 0;
            this.pageToolStripTextBox.ForeColor = System.Drawing.SystemColors.WindowText;
            this.pageToolStripTextBox.Name = "pageToolStripTextBox";
            this.pageToolStripTextBox.Padding = new System.Windows.Forms.Padding(0, 0, 2, 0);
            this.pageToolStripTextBox.Size = new System.Drawing.Size(172, 39);
            this.pageToolStripTextBox.Text = "(File Id: Page Id)";
            this.pageToolStripTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.PageToolStripTextBox_KeyDown);
            // 
            // previousToolStripButton
            // 
            this.previousToolStripButton.Image = global::InternalsViewer.UI.Properties.Resources.Backward_16xMD;
            this.previousToolStripButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.previousToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.previousToolStripButton.Name = "previousToolStripButton";
            this.previousToolStripButton.Size = new System.Drawing.Size(183, 51);
            this.previousToolStripButton.Text = "Previous Page";
            this.previousToolStripButton.ToolTipText = "Page ID - 1";
            this.previousToolStripButton.Click += new System.EventHandler(this.PreviousToolStripButton_Click);
            // 
            // nextToolStripButton
            // 
            this.nextToolStripButton.Image = global::InternalsViewer.UI.Properties.Resources.Forward_grey_16xMD;
            this.nextToolStripButton.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.nextToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.nextToolStripButton.Name = "nextToolStripButton";
            this.nextToolStripButton.Size = new System.Drawing.Size(143, 51);
            this.nextToolStripButton.Text = "Next Page";
            this.nextToolStripButton.ToolTipText = "Page ID +1";
            this.nextToolStripButton.Click += new System.EventHandler(this.NextToolStripButton_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 54);
            // 
            // toolStripLabel2
            // 
            this.toolStripLabel2.Name = "toolStripLabel2";
            this.toolStripLabel2.Size = new System.Drawing.Size(85, 51);
            this.toolStripLabel2.Text = "Offset:";
            // 
            // offsetTableToolStripTextBox
            // 
            this.offsetTableToolStripTextBox.AutoSize = false;
            this.offsetTableToolStripTextBox.Name = "offsetTableToolStripTextBox";
            this.offsetTableToolStripTextBox.Padding = new System.Windows.Forms.Padding(0, 0, 2, 0);
            this.offsetTableToolStripTextBox.Size = new System.Drawing.Size(56, 39);
            this.offsetTableToolStripTextBox.Text = "0000";
            this.offsetTableToolStripTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OffsetTableToolStripTextBox_KeyDown);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 54);
            // 
            // encodeAndFindToolStripButton
            // 
            this.encodeAndFindToolStripButton.Image = global::InternalsViewer.UI.Properties.Resources.Search_16x;
            this.encodeAndFindToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.encodeAndFindToolStripButton.Name = "encodeAndFindToolStripButton";
            this.encodeAndFindToolStripButton.Size = new System.Drawing.Size(208, 51);
            this.encodeAndFindToolStripButton.Text = "Encode && Find";
            this.encodeAndFindToolStripButton.ToolTipText = "Encode & Find";
            this.encodeAndFindToolStripButton.Click += new System.EventHandler(this.EncodeAndFindToolStripButton_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(6, 54);
            // 
            // logToolStripLabel
            // 
            this.logToolStripLabel.Name = "logToolStripLabel";
            this.logToolStripLabel.Size = new System.Drawing.Size(186, 51);
            this.logToolStripLabel.Text = "Transaction Log:";
            this.logToolStripLabel.Visible = false;
            // 
            // logToolStripComboBox
            // 
            this.logToolStripComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.logToolStripComboBox.Items.AddRange(new object[] {
            "None",
            "Before",
            "After"});
            this.logToolStripComboBox.Name = "logToolStripComboBox";
            this.logToolStripComboBox.Padding = new System.Windows.Forms.Padding(0, 2, 0, 0);
            this.logToolStripComboBox.Size = new System.Drawing.Size(115, 54);
            this.logToolStripComboBox.Visible = false;
            this.logToolStripComboBox.SelectedIndexChanged += new System.EventHandler(this.LogToolStripComboBox_SelectedIndexChanged);
            // 
            // PageViewerWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.leftPanel);
            this.Controls.Add(this.flatMenuStrip1);
            this.Controls.Add(this.statusStrip);
            this.Margin = new System.Windows.Forms.Padding(6);
            this.Name = "PageViewerWindow";
            this.Size = new System.Drawing.Size(1748, 1198);
            this.leftPanel.ResumeLayout(false);
            this.headerBorderPanel.ResumeLayout(false);
            this.headerBorderPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bcmPictureBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dcmPictureBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sGamPictureBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gamPictureBox)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.pageBindingSource)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.topLeftPanel.ResumeLayout(false);
            this.compressionInfoPanel.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.flatMenuStrip1.ResumeLayout(false);
            this.flatMenuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private ToolStripMenuItem logContents0ToolStripMenuItem;
        private ToolStripMenuItem rowLogContents1ToolStripMenuItem;
        private ToolStripMenuItem rowLogContents2ToolStripMenuItem;
        private ToolStripMenuItem rowLogContents3ToolStripMenuItem;
        private ToolStripMenuItem rowLogContents4ToolStripMenuItem;
        private FlatMenuStrip flatMenuStrip1;
        private ToolStripLabel toolStripLabel1;
        private ToolStripButton previousToolStripButton;
        private ToolStripButton nextToolStripButton;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripLabel toolStripLabel2;
        private PageAddressTextBox pageToolStripTextBox;
        private ToolStripTextBox offsetTableToolStripTextBox;
        private Panel leftPanel;
        private BorderPanel headerBorderPanel;
        private PictureBox bcmPictureBox;
        private PictureBox dcmPictureBox;
        private PictureBox sGamPictureBox;
        private PictureBox gamPictureBox;
        private Label label1;
        private Label label19;
        private Label label20;
        private Label label21;
        private Label label15;
        private Panel pfsPanel;
        private Label label6;
        private TextBox textBox15;
        private Label label5;
        private TextBox textBox4;
        private Label label4;
        private TextBox textBox3;
        private TextBox textBox2;
        private TextBox textBox14;
        private TextBox textBox13;
        private TextBox textBox12;
        private TextBox textBox11;
        private TextBox textBox10;
        private TextBox textBox9;
        private TextBox textBox8;
        private TextBox textBox7;
        private TextBox textBox6;
        private TextBox textBox17;
        private TextBox textBox16;
        private TextBox textBox5;
        private TextBox previousPageTextBox;
        private TextBox nextPageTextBox;
        private Label label18;
        private Label label16;
        private Label label14;
        private Label label12;
        private Label label11;
        private Label label10;
        private Label label36;
        private Label label17;
        private Label label13;
        private Label label9;
        private Label label8;
        private Label label7;
        private Label label3;
        private Label label2;
        private BindingSource pageBindingSource;
        private SplitContainer splitContainer1;
        private HexViewer hexViewer;
        private Panel topLeftPanel;
        private OffsetTable offsetTable;
        private MarkerKeyTable markerKeyTable;
        private AllocationViewer allocationViewer;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel pageAddressToolStripStatusLabel;
        private ToolStripStatusLabel errorImageToolStripStatusLabel;
        private ToolStripStatusLabel errorToolStripStatusLabel;
        private ToolStripStatusLabel toolStripStatusLabel3;
        private ToolStripStatusLabel markerDescriptionToolStripStatusLabel;
        private ToolStripStatusLabel offsetToolStripStatusLabel;
        private ToolStripSeparator toolStripSeparator1;
        private TextBox bcmTextBox;
        private TextBox dcmTextBox;
        private TextBox sgamTextBox;
        private TextBox gamTextBox;
        private TextBox pfsTextBox;
        private Panel compressionInfoPanel;
        private CompressionInfoTable compressionInfoTable;
        private ToolStripButton encodeAndFindToolStripButton;
        private ToolStripStatusLabel serverToolStripStatusLabel;
        private ToolStripStatusLabel dataaseToolStripStatusLabel;
        private ToolStripSeparator toolStripSeparator3;
        private ToolStripLabel logToolStripLabel;
        private ToolStripComboBox logToolStripComboBox;

    }
}
