using System.ComponentModel;
using System.Windows.Forms;
using InternalsViewer.Internals.Engine.Pages;
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
            components = new Container();
            logContents0ToolStripMenuItem = new ToolStripMenuItem();
            rowLogContents1ToolStripMenuItem = new ToolStripMenuItem();
            rowLogContents2ToolStripMenuItem = new ToolStripMenuItem();
            rowLogContents3ToolStripMenuItem = new ToolStripMenuItem();
            rowLogContents4ToolStripMenuItem = new ToolStripMenuItem();
            leftPanel = new Panel();
            headerBorderPanel = new BorderPanel();
            pfsTextBox = new TextBox();
            bcmTextBox = new TextBox();
            dcmTextBox = new TextBox();
            sgamTextBox = new TextBox();
            gamTextBox = new TextBox();
            bcmPictureBox = new PictureBox();
            dcmPictureBox = new PictureBox();
            sGamPictureBox = new PictureBox();
            gamPictureBox = new PictureBox();
            label1 = new Label();
            label19 = new Label();
            label20 = new Label();
            label21 = new Label();
            label15 = new Label();
            pfsPanel = new Panel();
            label6 = new Label();
            textBox15 = new TextBox();
            pageBindingSource = new BindingSource(components);
            label5 = new Label();
            textBox4 = new TextBox();
            label4 = new Label();
            ObjectNameTextBox = new TextBox();
            PageTypeTextBox = new TextBox();
            textBox14 = new TextBox();
            textBox13 = new TextBox();
            textBox12 = new TextBox();
            textBox11 = new TextBox();
            textBox10 = new TextBox();
            textBox9 = new TextBox();
            textBox8 = new TextBox();
            textBox7 = new TextBox();
            textBox6 = new TextBox();
            AllocationUnitIdTextBox = new TextBox();
            PartitionIdTextBox = new TextBox();
            ObjectIdTextBox = new TextBox();
            PreviousPageTextBox = new TextBox();
            NextPageTextBox = new TextBox();
            label18 = new Label();
            label16 = new Label();
            label14 = new Label();
            label12 = new Label();
            label11 = new Label();
            label10 = new Label();
            label36 = new Label();
            label17 = new Label();
            label13 = new Label();
            label9 = new Label();
            label8 = new Label();
            ObjectIdLabel = new Label();
            label3 = new Label();
            label2 = new Label();
            splitContainer1 = new SplitContainer();
            hexViewer = new HexViewer();
            topLeftPanel = new Panel();
            offsetTable = new OffsetTable();
            compressionInfoPanel = new Panel();
            compressionInfoTable = new CompressionInfoTable();
            allocationViewer = new AllocationViewer();
            markerKeyTable = new MarkerKeyTable();
            statusStrip = new StatusStrip();
            pageAddressToolStripStatusLabel = new ToolStripStatusLabel();
            errorImageToolStripStatusLabel = new ToolStripStatusLabel();
            errorToolStripStatusLabel = new ToolStripStatusLabel();
            serverToolStripStatusLabel = new ToolStripStatusLabel();
            dataaseToolStripStatusLabel = new ToolStripStatusLabel();
            toolStripStatusLabel3 = new ToolStripStatusLabel();
            markerDescriptionToolStripStatusLabel = new ToolStripStatusLabel();
            offsetToolStripStatusLabel = new ToolStripStatusLabel();
            flatMenuStrip1 = new FlatMenuStrip();
            toolStripLabel1 = new ToolStripLabel();
            pageToolStripTextBox = new PageAddressTextBox();
            previousToolStripButton = new ToolStripButton();
            nextToolStripButton = new ToolStripButton();
            toolStripSeparator2 = new ToolStripSeparator();
            toolStripLabel2 = new ToolStripLabel();
            offsetTableToolStripTextBox = new ToolStripTextBox();
            toolStripSeparator1 = new ToolStripSeparator();
            encodeAndFindToolStripButton = new ToolStripButton();
            toolStripSeparator3 = new ToolStripSeparator();
            logToolStripLabel = new ToolStripLabel();
            logToolStripComboBox = new ToolStripComboBox();
            leftPanel.SuspendLayout();
            headerBorderPanel.SuspendLayout();
            ((ISupportInitialize)bcmPictureBox).BeginInit();
            ((ISupportInitialize)dcmPictureBox).BeginInit();
            ((ISupportInitialize)sGamPictureBox).BeginInit();
            ((ISupportInitialize)gamPictureBox).BeginInit();
            ((ISupportInitialize)pageBindingSource).BeginInit();
            ((ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            topLeftPanel.SuspendLayout();
            compressionInfoPanel.SuspendLayout();
            statusStrip.SuspendLayout();
            flatMenuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // logContents0ToolStripMenuItem
            // 
            logContents0ToolStripMenuItem.Name = "logContents0ToolStripMenuItem";
            logContents0ToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            logContents0ToolStripMenuItem.Text = "Row Log Contents 0";
            // 
            // rowLogContents1ToolStripMenuItem
            // 
            rowLogContents1ToolStripMenuItem.Name = "rowLogContents1ToolStripMenuItem";
            rowLogContents1ToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            rowLogContents1ToolStripMenuItem.Text = "Row Log Contents 1";
            // 
            // rowLogContents2ToolStripMenuItem
            // 
            rowLogContents2ToolStripMenuItem.Name = "rowLogContents2ToolStripMenuItem";
            rowLogContents2ToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            rowLogContents2ToolStripMenuItem.Text = "Row Log Contents 2";
            // 
            // rowLogContents3ToolStripMenuItem
            // 
            rowLogContents3ToolStripMenuItem.Name = "rowLogContents3ToolStripMenuItem";
            rowLogContents3ToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            rowLogContents3ToolStripMenuItem.Text = "Row Log Contents 3";
            // 
            // rowLogContents4ToolStripMenuItem
            // 
            rowLogContents4ToolStripMenuItem.Name = "rowLogContents4ToolStripMenuItem";
            rowLogContents4ToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            rowLogContents4ToolStripMenuItem.Text = "Row Log Contents 4";
            // 
            // leftPanel
            // 
            leftPanel.BackColor = System.Drawing.Color.Transparent;
            leftPanel.Controls.Add(headerBorderPanel);
            leftPanel.Dock = DockStyle.Left;
            leftPanel.Location = new System.Drawing.Point(0, 32);
            leftPanel.Margin = new Padding(4);
            leftPanel.Name = "leftPanel";
            leftPanel.Padding = new Padding(0, 0, 4, 0);
            leftPanel.Size = new System.Drawing.Size(350, 665);
            leftPanel.TabIndex = 246;
            // 
            // headerBorderPanel
            // 
            headerBorderPanel.BackColor = System.Drawing.Color.White;
            headerBorderPanel.Controls.Add(pfsTextBox);
            headerBorderPanel.Controls.Add(bcmTextBox);
            headerBorderPanel.Controls.Add(dcmTextBox);
            headerBorderPanel.Controls.Add(sgamTextBox);
            headerBorderPanel.Controls.Add(gamTextBox);
            headerBorderPanel.Controls.Add(bcmPictureBox);
            headerBorderPanel.Controls.Add(dcmPictureBox);
            headerBorderPanel.Controls.Add(sGamPictureBox);
            headerBorderPanel.Controls.Add(gamPictureBox);
            headerBorderPanel.Controls.Add(label1);
            headerBorderPanel.Controls.Add(label19);
            headerBorderPanel.Controls.Add(label20);
            headerBorderPanel.Controls.Add(label21);
            headerBorderPanel.Controls.Add(label15);
            headerBorderPanel.Controls.Add(pfsPanel);
            headerBorderPanel.Controls.Add(label6);
            headerBorderPanel.Controls.Add(textBox15);
            headerBorderPanel.Controls.Add(label5);
            headerBorderPanel.Controls.Add(textBox4);
            headerBorderPanel.Controls.Add(label4);
            headerBorderPanel.Controls.Add(ObjectNameTextBox);
            headerBorderPanel.Controls.Add(PageTypeTextBox);
            headerBorderPanel.Controls.Add(textBox14);
            headerBorderPanel.Controls.Add(textBox13);
            headerBorderPanel.Controls.Add(textBox12);
            headerBorderPanel.Controls.Add(textBox11);
            headerBorderPanel.Controls.Add(textBox10);
            headerBorderPanel.Controls.Add(textBox9);
            headerBorderPanel.Controls.Add(textBox8);
            headerBorderPanel.Controls.Add(textBox7);
            headerBorderPanel.Controls.Add(textBox6);
            headerBorderPanel.Controls.Add(AllocationUnitIdTextBox);
            headerBorderPanel.Controls.Add(PartitionIdTextBox);
            headerBorderPanel.Controls.Add(ObjectIdTextBox);
            headerBorderPanel.Controls.Add(PreviousPageTextBox);
            headerBorderPanel.Controls.Add(NextPageTextBox);
            headerBorderPanel.Controls.Add(label18);
            headerBorderPanel.Controls.Add(label16);
            headerBorderPanel.Controls.Add(label14);
            headerBorderPanel.Controls.Add(label12);
            headerBorderPanel.Controls.Add(label11);
            headerBorderPanel.Controls.Add(label10);
            headerBorderPanel.Controls.Add(label36);
            headerBorderPanel.Controls.Add(label17);
            headerBorderPanel.Controls.Add(label13);
            headerBorderPanel.Controls.Add(label9);
            headerBorderPanel.Controls.Add(label8);
            headerBorderPanel.Controls.Add(ObjectIdLabel);
            headerBorderPanel.Controls.Add(label3);
            headerBorderPanel.Controls.Add(label2);
            headerBorderPanel.Dock = DockStyle.Fill;
            headerBorderPanel.Location = new System.Drawing.Point(0, 0);
            headerBorderPanel.Margin = new Padding(4);
            headerBorderPanel.Name = "headerBorderPanel";
            headerBorderPanel.Size = new System.Drawing.Size(346, 665);
            headerBorderPanel.TabIndex = 0;
            // 
            // pfsTextBox
            // 
            pfsTextBox.BackColor = System.Drawing.Color.White;
            pfsTextBox.BorderStyle = BorderStyle.None;
            pfsTextBox.Cursor = Cursors.Hand;
            pfsTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, 0);
            pfsTextBox.ForeColor = System.Drawing.Color.Blue;
            pfsTextBox.Location = new System.Drawing.Point(108, 540);
            pfsTextBox.Margin = new Padding(4);
            pfsTextBox.Name = "pfsTextBox";
            pfsTextBox.ReadOnly = true;
            pfsTextBox.Size = new System.Drawing.Size(88, 13);
            pfsTextBox.TabIndex = 267;
            pfsTextBox.MouseClick += PageTextBox_MouseClick;
            // 
            // bcmTextBox
            // 
            bcmTextBox.BackColor = System.Drawing.Color.White;
            bcmTextBox.BorderStyle = BorderStyle.None;
            bcmTextBox.Cursor = Cursors.Hand;
            bcmTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, 0);
            bcmTextBox.ForeColor = System.Drawing.Color.Blue;
            bcmTextBox.Location = new System.Drawing.Point(108, 517);
            bcmTextBox.Margin = new Padding(4);
            bcmTextBox.Name = "bcmTextBox";
            bcmTextBox.ReadOnly = true;
            bcmTextBox.Size = new System.Drawing.Size(88, 13);
            bcmTextBox.TabIndex = 266;
            bcmTextBox.MouseClick += PageTextBox_MouseClick;
            // 
            // dcmTextBox
            // 
            dcmTextBox.BackColor = System.Drawing.Color.White;
            dcmTextBox.BorderStyle = BorderStyle.None;
            dcmTextBox.Cursor = Cursors.Hand;
            dcmTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, 0);
            dcmTextBox.ForeColor = System.Drawing.Color.Blue;
            dcmTextBox.Location = new System.Drawing.Point(108, 494);
            dcmTextBox.Margin = new Padding(4);
            dcmTextBox.Name = "dcmTextBox";
            dcmTextBox.ReadOnly = true;
            dcmTextBox.Size = new System.Drawing.Size(88, 13);
            dcmTextBox.TabIndex = 265;
            dcmTextBox.MouseClick += PageTextBox_MouseClick;
            // 
            // sgamTextBox
            // 
            sgamTextBox.BackColor = System.Drawing.Color.White;
            sgamTextBox.BorderStyle = BorderStyle.None;
            sgamTextBox.Cursor = Cursors.Hand;
            sgamTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, 0);
            sgamTextBox.ForeColor = System.Drawing.Color.Blue;
            sgamTextBox.Location = new System.Drawing.Point(108, 472);
            sgamTextBox.Margin = new Padding(4);
            sgamTextBox.Name = "sgamTextBox";
            sgamTextBox.ReadOnly = true;
            sgamTextBox.Size = new System.Drawing.Size(88, 13);
            sgamTextBox.TabIndex = 264;
            sgamTextBox.MouseClick += PageTextBox_MouseClick;
            // 
            // gamTextBox
            // 
            gamTextBox.BackColor = System.Drawing.Color.White;
            gamTextBox.BorderStyle = BorderStyle.None;
            gamTextBox.Cursor = Cursors.Hand;
            gamTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, 0);
            gamTextBox.ForeColor = System.Drawing.Color.Blue;
            gamTextBox.Location = new System.Drawing.Point(108, 450);
            gamTextBox.Margin = new Padding(4);
            gamTextBox.Name = "gamTextBox";
            gamTextBox.ReadOnly = true;
            gamTextBox.Size = new System.Drawing.Size(88, 13);
            gamTextBox.TabIndex = 263;
            gamTextBox.MouseClick += PageTextBox_MouseClick;
            // 
            // bcmPictureBox
            // 
            bcmPictureBox.Location = new System.Drawing.Point(59, 515);
            bcmPictureBox.Margin = new Padding(4);
            bcmPictureBox.Name = "bcmPictureBox";
            bcmPictureBox.Size = new System.Drawing.Size(19, 19);
            bcmPictureBox.TabIndex = 262;
            bcmPictureBox.TabStop = false;
            // 
            // dcmPictureBox
            // 
            dcmPictureBox.Location = new System.Drawing.Point(59, 493);
            dcmPictureBox.Margin = new Padding(4);
            dcmPictureBox.Name = "dcmPictureBox";
            dcmPictureBox.Size = new System.Drawing.Size(19, 19);
            dcmPictureBox.TabIndex = 261;
            dcmPictureBox.TabStop = false;
            // 
            // sGamPictureBox
            // 
            sGamPictureBox.Location = new System.Drawing.Point(59, 471);
            sGamPictureBox.Margin = new Padding(4);
            sGamPictureBox.Name = "sGamPictureBox";
            sGamPictureBox.Size = new System.Drawing.Size(19, 19);
            sGamPictureBox.TabIndex = 260;
            sGamPictureBox.TabStop = false;
            // 
            // gamPictureBox
            // 
            gamPictureBox.Location = new System.Drawing.Point(59, 449);
            gamPictureBox.Margin = new Padding(4);
            gamPictureBox.Name = "gamPictureBox";
            gamPictureBox.Size = new System.Drawing.Size(19, 19);
            gamPictureBox.TabIndex = 259;
            gamPictureBox.TabStop = false;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.BackColor = System.Drawing.Color.Transparent;
            label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label1.ForeColor = System.Drawing.Color.Gray;
            label1.Location = new System.Drawing.Point(9, 474);
            label1.Margin = new Padding(5);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(38, 13);
            label1.TabIndex = 258;
            label1.Text = "SGAM";
            // 
            // label19
            // 
            label19.AutoSize = true;
            label19.BackColor = System.Drawing.Color.Transparent;
            label19.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label19.ForeColor = System.Drawing.Color.Gray;
            label19.Location = new System.Drawing.Point(9, 517);
            label19.Margin = new Padding(5);
            label19.Name = "label19";
            label19.Size = new System.Drawing.Size(30, 13);
            label19.TabIndex = 257;
            label19.Text = "BCM";
            // 
            // label20
            // 
            label20.AutoSize = true;
            label20.BackColor = System.Drawing.Color.Transparent;
            label20.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label20.ForeColor = System.Drawing.Color.Gray;
            label20.Location = new System.Drawing.Point(9, 451);
            label20.Margin = new Padding(5);
            label20.Name = "label20";
            label20.Size = new System.Drawing.Size(31, 13);
            label20.TabIndex = 256;
            label20.Text = "GAM";
            // 
            // label21
            // 
            label21.AutoSize = true;
            label21.BackColor = System.Drawing.Color.Transparent;
            label21.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label21.ForeColor = System.Drawing.Color.Gray;
            label21.Location = new System.Drawing.Point(9, 495);
            label21.Margin = new Padding(5);
            label21.Name = "label21";
            label21.Size = new System.Drawing.Size(31, 13);
            label21.TabIndex = 255;
            label21.Text = "DCM";
            // 
            // label15
            // 
            label15.AutoSize = true;
            label15.BackColor = System.Drawing.Color.Transparent;
            label15.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label15.ForeColor = System.Drawing.Color.Gray;
            label15.Location = new System.Drawing.Point(9, 540);
            label15.Margin = new Padding(5);
            label15.Name = "label15";
            label15.Size = new System.Drawing.Size(27, 13);
            label15.TabIndex = 254;
            label15.Text = "PFS";
            label15.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // pfsPanel
            // 
            pfsPanel.BackColor = System.Drawing.Color.Transparent;
            pfsPanel.BackgroundImageLayout = ImageLayout.Center;
            pfsPanel.Font = new System.Drawing.Font("Microsoft Sans Serif", 6F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            pfsPanel.Location = new System.Drawing.Point(59, 540);
            pfsPanel.Margin = new Padding(4);
            pfsPanel.Name = "pfsPanel";
            pfsPanel.Size = new System.Drawing.Size(38, 38);
            pfsPanel.TabIndex = 253;
            pfsPanel.Paint += PfsPanel_Paint;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.BackColor = System.Drawing.Color.Transparent;
            label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label6.ForeColor = System.Drawing.Color.Gray;
            label6.Location = new System.Drawing.Point(9, 363);
            label6.Margin = new Padding(5);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(78, 13);
            label6.TabIndex = 252;
            label6.Text = "Xact Reserved";
            // 
            // textBox15
            // 
            textBox15.BackColor = System.Drawing.Color.White;
            textBox15.BorderStyle = BorderStyle.None;
            textBox15.DataBindings.Add(new Binding("Text", pageBindingSource, "FreeCount", true));
            textBox15.Location = new System.Drawing.Point(108, 319);
            textBox15.Margin = new Padding(4);
            textBox15.Name = "textBox15";
            textBox15.ReadOnly = true;
            textBox15.Size = new System.Drawing.Size(90, 16);
            textBox15.TabIndex = 251;
            // 
            // pageBindingSource
            // 
            pageBindingSource.DataSource = typeof(PageHeader);
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.BackColor = System.Drawing.Color.Transparent;
            label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label5.ForeColor = System.Drawing.Color.Gray;
            label5.Location = new System.Drawing.Point(9, 319);
            label5.Margin = new Padding(5);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(57, 13);
            label5.TabIndex = 250;
            label5.Text = "Free Bytes";
            // 
            // textBox4
            // 
            textBox4.BackColor = System.Drawing.Color.White;
            textBox4.BorderStyle = BorderStyle.None;
            textBox4.DataBindings.Add(new Binding("Text", pageBindingSource, "Lsn", true));
            textBox4.Location = new System.Drawing.Point(108, 186);
            textBox4.Margin = new Padding(4);
            textBox4.Name = "textBox4";
            textBox4.ReadOnly = true;
            textBox4.Size = new System.Drawing.Size(90, 16);
            textBox4.TabIndex = 249;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.BackColor = System.Drawing.Color.Transparent;
            label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label4.ForeColor = System.Drawing.Color.Gray;
            label4.Location = new System.Drawing.Point(9, 186);
            label4.Margin = new Padding(5);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(28, 13);
            label4.TabIndex = 248;
            label4.Text = "LSN";
            // 
            // ObjectNameTextBox
            // 
            ObjectNameTextBox.BackColor = System.Drawing.Color.GhostWhite;
            ObjectNameTextBox.BorderStyle = BorderStyle.None;
            ObjectNameTextBox.Location = new System.Drawing.Point(9, 115);
            ObjectNameTextBox.Margin = new Padding(4);
            ObjectNameTextBox.Name = "ObjectNameTextBox";
            ObjectNameTextBox.ReadOnly = true;
            ObjectNameTextBox.Size = new System.Drawing.Size(327, 16);
            ObjectNameTextBox.TabIndex = 247;
            // 
            // PageTypeTextBox
            // 
            PageTypeTextBox.BackColor = System.Drawing.Color.White;
            PageTypeTextBox.BorderStyle = BorderStyle.None;
            PageTypeTextBox.DataBindings.Add(new Binding("Text", pageBindingSource, "PageTypeName", true));
            PageTypeTextBox.Location = new System.Drawing.Point(9, 28);
            PageTypeTextBox.Margin = new Padding(4);
            PageTypeTextBox.Name = "PageTypeTextBox";
            PageTypeTextBox.ReadOnly = true;
            PageTypeTextBox.Size = new System.Drawing.Size(327, 16);
            PageTypeTextBox.TabIndex = 246;
            // 
            // textBox14
            // 
            textBox14.BackColor = System.Drawing.Color.White;
            textBox14.BorderStyle = BorderStyle.None;
            textBox14.DataBindings.Add(new Binding("Text", pageBindingSource, "FlagBits", true));
            textBox14.Location = new System.Drawing.Point(108, 406);
            textBox14.Margin = new Padding(4);
            textBox14.Name = "textBox14";
            textBox14.ReadOnly = true;
            textBox14.Size = new System.Drawing.Size(90, 16);
            textBox14.TabIndex = 245;
            // 
            // textBox13
            // 
            textBox13.BackColor = System.Drawing.Color.White;
            textBox13.BorderStyle = BorderStyle.None;
            textBox13.DataBindings.Add(new Binding("Text", pageBindingSource, "ReservedCount", true));
            textBox13.Location = new System.Drawing.Point(108, 341);
            textBox13.Margin = new Padding(4);
            textBox13.Name = "textBox13";
            textBox13.ReadOnly = true;
            textBox13.Size = new System.Drawing.Size(90, 16);
            textBox13.TabIndex = 244;
            // 
            // textBox12
            // 
            textBox12.BackColor = System.Drawing.Color.White;
            textBox12.BorderStyle = BorderStyle.None;
            textBox12.Location = new System.Drawing.Point(108, 363);
            textBox12.Margin = new Padding(4);
            textBox12.Name = "textBox12";
            textBox12.ReadOnly = true;
            textBox12.Size = new System.Drawing.Size(90, 16);
            textBox12.TabIndex = 243;
            // 
            // textBox11
            // 
            textBox11.BackColor = System.Drawing.Color.White;
            textBox11.BorderStyle = BorderStyle.None;
            textBox11.DataBindings.Add(new Binding("Text", pageBindingSource, "InternalIndexId", true));
            textBox11.Location = new System.Drawing.Point(108, 253);
            textBox11.Margin = new Padding(4);
            textBox11.Name = "textBox11";
            textBox11.ReadOnly = true;
            textBox11.Size = new System.Drawing.Size(90, 16);
            textBox11.TabIndex = 242;
            // 
            // textBox10
            // 
            textBox10.BackColor = System.Drawing.Color.White;
            textBox10.BorderStyle = BorderStyle.None;
            textBox10.DataBindings.Add(new Binding("Text", pageBindingSource, "Level", true));
            textBox10.Location = new System.Drawing.Point(108, 231);
            textBox10.Margin = new Padding(4);
            textBox10.Name = "textBox10";
            textBox10.ReadOnly = true;
            textBox10.Size = new System.Drawing.Size(90, 16);
            textBox10.TabIndex = 241;
            // 
            // textBox9
            // 
            textBox9.BackColor = System.Drawing.Color.White;
            textBox9.BorderStyle = BorderStyle.None;
            textBox9.DataBindings.Add(new Binding("Text", pageBindingSource, "SlotCount", true));
            textBox9.Location = new System.Drawing.Point(108, 209);
            textBox9.Margin = new Padding(4);
            textBox9.Name = "textBox9";
            textBox9.ReadOnly = true;
            textBox9.Size = new System.Drawing.Size(90, 16);
            textBox9.TabIndex = 240;
            // 
            // textBox8
            // 
            textBox8.BackColor = System.Drawing.Color.White;
            textBox8.BorderStyle = BorderStyle.None;
            textBox8.DataBindings.Add(new Binding("Text", pageBindingSource, "TornBits", true));
            textBox8.Location = new System.Drawing.Point(108, 385);
            textBox8.Margin = new Padding(4);
            textBox8.Name = "textBox8";
            textBox8.ReadOnly = true;
            textBox8.Size = new System.Drawing.Size(90, 16);
            textBox8.TabIndex = 239;
            // 
            // textBox7
            // 
            textBox7.BackColor = System.Drawing.Color.White;
            textBox7.BorderStyle = BorderStyle.None;
            textBox7.Location = new System.Drawing.Point(108, 297);
            textBox7.Margin = new Padding(4);
            textBox7.Name = "textBox7";
            textBox7.ReadOnly = true;
            textBox7.Size = new System.Drawing.Size(90, 16);
            textBox7.TabIndex = 238;
            // 
            // textBox6
            // 
            textBox6.BackColor = System.Drawing.Color.White;
            textBox6.BorderStyle = BorderStyle.None;
            textBox6.DataBindings.Add(new Binding("Text", pageBindingSource, "FreeData", true));
            textBox6.Location = new System.Drawing.Point(108, 275);
            textBox6.Margin = new Padding(4);
            textBox6.Name = "textBox6";
            textBox6.ReadOnly = true;
            textBox6.Size = new System.Drawing.Size(90, 16);
            textBox6.TabIndex = 237;
            // 
            // AllocationUnitIdTextBox
            // 
            AllocationUnitIdTextBox.BackColor = System.Drawing.Color.White;
            AllocationUnitIdTextBox.BorderStyle = BorderStyle.None;
            AllocationUnitIdTextBox.DataBindings.Add(new Binding("Text", pageBindingSource, "AllocationUnitId", true));
            AllocationUnitIdTextBox.Location = new System.Drawing.Point(105, 160);
            AllocationUnitIdTextBox.Margin = new Padding(4);
            AllocationUnitIdTextBox.Name = "AllocationUnitIdTextBox";
            AllocationUnitIdTextBox.ReadOnly = true;
            AllocationUnitIdTextBox.Size = new System.Drawing.Size(231, 16);
            AllocationUnitIdTextBox.TabIndex = 236;
            // 
            // PartitionIdTextBox
            // 
            PartitionIdTextBox.BackColor = System.Drawing.Color.GhostWhite;
            PartitionIdTextBox.BorderStyle = BorderStyle.None;
            PartitionIdTextBox.Location = new System.Drawing.Point(105, 136);
            PartitionIdTextBox.Margin = new Padding(4);
            PartitionIdTextBox.Name = "PartitionIdTextBox";
            PartitionIdTextBox.ReadOnly = true;
            PartitionIdTextBox.Size = new System.Drawing.Size(231, 16);
            PartitionIdTextBox.TabIndex = 235;
            // 
            // ObjectIdTextBox
            // 
            ObjectIdTextBox.BackColor = System.Drawing.Color.GhostWhite;
            ObjectIdTextBox.BorderStyle = BorderStyle.None;
            ObjectIdTextBox.DataBindings.Add(new Binding("Text", pageBindingSource, "InternalObjectId", true));
            ObjectIdTextBox.Location = new System.Drawing.Point(105, 94);
            ObjectIdTextBox.Margin = new Padding(4);
            ObjectIdTextBox.Name = "ObjectIdTextBox";
            ObjectIdTextBox.ReadOnly = true;
            ObjectIdTextBox.Size = new System.Drawing.Size(231, 16);
            ObjectIdTextBox.TabIndex = 234;
            // 
            // PreviousPageTextBox
            // 
            PreviousPageTextBox.BackColor = System.Drawing.Color.White;
            PreviousPageTextBox.BorderStyle = BorderStyle.None;
            PreviousPageTextBox.Cursor = Cursors.Hand;
            PreviousPageTextBox.DataBindings.Add(new Binding("Text", pageBindingSource, "PreviousPage", true));
            PreviousPageTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, 0);
            PreviousPageTextBox.ForeColor = System.Drawing.Color.Blue;
            PreviousPageTextBox.Location = new System.Drawing.Point(105, 71);
            PreviousPageTextBox.Margin = new Padding(4);
            PreviousPageTextBox.Name = "PreviousPageTextBox";
            PreviousPageTextBox.ReadOnly = true;
            PreviousPageTextBox.Size = new System.Drawing.Size(88, 13);
            PreviousPageTextBox.TabIndex = 233;
            PreviousPageTextBox.MouseClick += PageTextBox_MouseClick;
            // 
            // NextPageTextBox
            // 
            NextPageTextBox.BackColor = System.Drawing.Color.White;
            NextPageTextBox.BorderStyle = BorderStyle.None;
            NextPageTextBox.Cursor = Cursors.Hand;
            NextPageTextBox.DataBindings.Add(new Binding("Text", pageBindingSource, "NextPage", true));
            NextPageTextBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, 0);
            NextPageTextBox.ForeColor = System.Drawing.Color.Blue;
            NextPageTextBox.Location = new System.Drawing.Point(105, 50);
            NextPageTextBox.Margin = new Padding(4);
            NextPageTextBox.Name = "NextPageTextBox";
            NextPageTextBox.ReadOnly = true;
            NextPageTextBox.Size = new System.Drawing.Size(88, 13);
            NextPageTextBox.TabIndex = 232;
            NextPageTextBox.MouseClick += PageTextBox_MouseClick;
            // 
            // label18
            // 
            label18.AutoSize = true;
            label18.BackColor = System.Drawing.Color.Transparent;
            label18.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label18.ForeColor = System.Drawing.Color.Gray;
            label18.Location = new System.Drawing.Point(9, 406);
            label18.Margin = new Padding(5);
            label18.Name = "label18";
            label18.Size = new System.Drawing.Size(47, 13);
            label18.TabIndex = 231;
            label18.Text = "Flag Bits";
            // 
            // label16
            // 
            label16.AutoSize = true;
            label16.BackColor = System.Drawing.Color.Transparent;
            label16.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label16.ForeColor = System.Drawing.Color.Gray;
            label16.Location = new System.Drawing.Point(9, 341);
            label16.Margin = new Padding(5);
            label16.Name = "label16";
            label16.Size = new System.Drawing.Size(82, 13);
            label16.TabIndex = 230;
            label16.Text = "Reserved Bytes";
            // 
            // label14
            // 
            label14.AutoSize = true;
            label14.BackColor = System.Drawing.Color.Transparent;
            label14.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label14.ForeColor = System.Drawing.Color.Gray;
            label14.Location = new System.Drawing.Point(9, 297);
            label14.Margin = new Padding(5);
            label14.Name = "label14";
            label14.Size = new System.Drawing.Size(68, 13);
            label14.TabIndex = 229;
            label14.Text = "Fixed Length";
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.BackColor = System.Drawing.Color.Transparent;
            label12.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label12.ForeColor = System.Drawing.Color.Gray;
            label12.Location = new System.Drawing.Point(9, 253);
            label12.Margin = new Padding(5);
            label12.Name = "label12";
            label12.Size = new System.Drawing.Size(45, 13);
            label12.TabIndex = 228;
            label12.Text = "Index Id";
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.BackColor = System.Drawing.Color.Transparent;
            label11.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label11.ForeColor = System.Drawing.Color.Gray;
            label11.Location = new System.Drawing.Point(9, 231);
            label11.Margin = new Padding(5);
            label11.Name = "label11";
            label11.Size = new System.Drawing.Size(33, 13);
            label11.TabIndex = 227;
            label11.Text = "Level";
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.BackColor = System.Drawing.Color.Transparent;
            label10.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label10.ForeColor = System.Drawing.Color.Gray;
            label10.Location = new System.Drawing.Point(9, 209);
            label10.Margin = new Padding(5);
            label10.Name = "label10";
            label10.Size = new System.Drawing.Size(56, 13);
            label10.TabIndex = 226;
            label10.Text = "Slot Count";
            // 
            // label36
            // 
            label36.AutoSize = true;
            label36.BackColor = System.Drawing.Color.Transparent;
            label36.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label36.ForeColor = System.Drawing.Color.Gray;
            label36.Location = new System.Drawing.Point(6, 5);
            label36.Margin = new Padding(5);
            label36.Name = "label36";
            label36.Size = new System.Drawing.Size(59, 13);
            label36.TabIndex = 225;
            label36.Text = "Page Type";
            // 
            // label17
            // 
            label17.AutoSize = true;
            label17.BackColor = System.Drawing.Color.Transparent;
            label17.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label17.ForeColor = System.Drawing.Color.Gray;
            label17.Location = new System.Drawing.Point(9, 385);
            label17.Margin = new Padding(5);
            label17.Name = "label17";
            label17.Size = new System.Drawing.Size(49, 13);
            label17.TabIndex = 224;
            label17.Text = "Torn Bits";
            // 
            // label13
            // 
            label13.AutoSize = true;
            label13.BackColor = System.Drawing.Color.Transparent;
            label13.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label13.ForeColor = System.Drawing.Color.Gray;
            label13.Location = new System.Drawing.Point(9, 275);
            label13.Margin = new Padding(5);
            label13.Name = "label13";
            label13.Size = new System.Drawing.Size(54, 13);
            label13.TabIndex = 223;
            label13.Text = "Free Data";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.BackColor = System.Drawing.Color.Transparent;
            label9.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label9.ForeColor = System.Drawing.Color.Gray;
            label9.Location = new System.Drawing.Point(6, 163);
            label9.Margin = new Padding(5);
            label9.Name = "label9";
            label9.Size = new System.Drawing.Size(67, 13);
            label9.TabIndex = 222;
            label9.Text = "Alloc. Unit Id";
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.BackColor = System.Drawing.Color.Transparent;
            label8.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label8.ForeColor = System.Drawing.Color.Gray;
            label8.Location = new System.Drawing.Point(6, 136);
            label8.Margin = new Padding(5);
            label8.Name = "label8";
            label8.Size = new System.Drawing.Size(57, 13);
            label8.TabIndex = 221;
            label8.Text = "Partition Id";
            // 
            // ObjectIdLabel
            // 
            ObjectIdLabel.AutoSize = true;
            ObjectIdLabel.BackColor = System.Drawing.Color.Transparent;
            ObjectIdLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            ObjectIdLabel.ForeColor = System.Drawing.Color.Gray;
            ObjectIdLabel.Location = new System.Drawing.Point(6, 94);
            ObjectIdLabel.Margin = new Padding(5);
            ObjectIdLabel.Name = "ObjectIdLabel";
            ObjectIdLabel.Size = new System.Drawing.Size(50, 13);
            ObjectIdLabel.TabIndex = 220;
            ObjectIdLabel.Text = "Object Id";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.BackColor = System.Drawing.Color.Transparent;
            label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label3.ForeColor = System.Drawing.Color.Gray;
            label3.Location = new System.Drawing.Point(6, 71);
            label3.Margin = new Padding(5);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(60, 13);
            label3.TabIndex = 219;
            label3.Text = "Prev. Page";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.BackColor = System.Drawing.Color.Transparent;
            label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
            label2.ForeColor = System.Drawing.Color.Gray;
            label2.Location = new System.Drawing.Point(6, 50);
            label2.Margin = new Padding(5);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(57, 13);
            label2.TabIndex = 218;
            label2.Text = "Next Page";
            // 
            // splitContainer1
            // 
            splitContainer1.BackColor = System.Drawing.Color.Transparent;
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new System.Drawing.Point(350, 32);
            splitContainer1.Margin = new Padding(4);
            splitContainer1.Name = "splitContainer1";
            splitContainer1.Orientation = Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.BackColor = System.Drawing.Color.Transparent;
            splitContainer1.Panel1.Controls.Add(hexViewer);
            splitContainer1.Panel1.Controls.Add(topLeftPanel);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.BackColor = System.Drawing.Color.White;
            splitContainer1.Panel2.Controls.Add(allocationViewer);
            splitContainer1.Panel2.Controls.Add(markerKeyTable);
            splitContainer1.Size = new System.Drawing.Size(670, 665);
            splitContainer1.SplitterDistance = 335;
            splitContainer1.SplitterWidth = 5;
            splitContainer1.TabIndex = 247;
            // 
            // hexViewer
            // 
            hexViewer.AddressHex = false;
            hexViewer.BackColor = System.Drawing.Color.White;
            hexViewer.ColourAndOffsetDictionary = new();
            hexViewer.Colourise = true;
            hexViewer.DataRtf = "";
            hexViewer.DataText = null;
            hexViewer.Dock = DockStyle.Fill;
            hexViewer.Font = new System.Drawing.Font("Courier New", 8.25F);
            hexViewer.Location = new System.Drawing.Point(0, 0);
            hexViewer.Margin = new Padding(1, 0, 0, 0);
            hexViewer.Name = "hexViewer";
            hexViewer.Padding = new Padding(1, 0, 1, 1);
            hexViewer.Page = null;
            hexViewer.SelectedOffset = -1;
            hexViewer.SelectedRecord = -1;
            hexViewer.Size = new System.Drawing.Size(460, 335);
            hexViewer.TabIndex = 0;
            hexViewer.OffsetOver += HexViewer_OffsetOver;
            hexViewer.OffsetSet += HexViewer_OffsetSet;
            hexViewer.RecordFind += HexViewer_RecordFind;
            // 
            // topLeftPanel
            // 
            topLeftPanel.BackColor = System.Drawing.Color.Transparent;
            topLeftPanel.Controls.Add(offsetTable);
            topLeftPanel.Controls.Add(compressionInfoPanel);
            topLeftPanel.Dock = DockStyle.Right;
            topLeftPanel.Location = new System.Drawing.Point(460, 0);
            topLeftPanel.Margin = new Padding(4);
            topLeftPanel.Name = "topLeftPanel";
            topLeftPanel.Padding = new Padding(4, 0, 0, 0);
            topLeftPanel.Size = new System.Drawing.Size(210, 335);
            topLeftPanel.TabIndex = 1;
            // 
            // offsetTable
            // 
            offsetTable.Dock = DockStyle.Fill;
            offsetTable.Location = new System.Drawing.Point(4, 0);
            offsetTable.Margin = new Padding(7);
            offsetTable.Name = "offsetTable";
            offsetTable.Padding = new Padding(1);
            offsetTable.Page = null;
            offsetTable.SelectedSlot = -1;
            offsetTable.Size = new System.Drawing.Size(206, 213);
            offsetTable.TabIndex = 0;
            offsetTable.SlotChanged += OffsetTable_SlotChanged;
            // 
            // compressionInfoPanel
            // 
            compressionInfoPanel.Controls.Add(compressionInfoTable);
            compressionInfoPanel.Dock = DockStyle.Bottom;
            compressionInfoPanel.Location = new System.Drawing.Point(4, 213);
            compressionInfoPanel.Margin = new Padding(4);
            compressionInfoPanel.Name = "compressionInfoPanel";
            compressionInfoPanel.Padding = new Padding(0, 5, 0, 0);
            compressionInfoPanel.Size = new System.Drawing.Size(206, 122);
            compressionInfoPanel.TabIndex = 2;
            // 
            // compressionInfoTable
            // 
            compressionInfoTable.Dock = DockStyle.Fill;
            compressionInfoTable.Location = new System.Drawing.Point(0, 5);
            compressionInfoTable.Margin = new Padding(7);
            compressionInfoTable.Name = "compressionInfoTable";
            compressionInfoTable.Padding = new Padding(1);
            compressionInfoTable.SelectedStructure = Internals.Compression.CompressionInfoStructure.None;
            compressionInfoTable.Size = new System.Drawing.Size(206, 117);
            compressionInfoTable.TabIndex = 0;
            compressionInfoTable.PropertyChanged += CompressionInfoTable_PropertyChanged;
            // 
            // allocationViewer
            // 
            allocationViewer.Dock = DockStyle.Fill;
            allocationViewer.Location = new System.Drawing.Point(0, 0);
            allocationViewer.Margin = new Padding(7);
            allocationViewer.Name = "allocationViewer";
            allocationViewer.Size = new System.Drawing.Size(670, 325);
            allocationViewer.TabIndex = 1;
            allocationViewer.Visible = false;
            allocationViewer.PageOver += AllocationViewer_PageOver;
            allocationViewer.PageClicked += AllocationViewer_PageClicked;
            // 
            // markerKeyTable
            // 
            markerKeyTable.BackColor = System.Drawing.Color.White;
            markerKeyTable.Dock = DockStyle.Fill;
            markerKeyTable.Location = new System.Drawing.Point(0, 0);
            markerKeyTable.Margin = new Padding(7);
            markerKeyTable.Name = "markerKeyTable";
            markerKeyTable.Padding = new Padding(1);
            markerKeyTable.Size = new System.Drawing.Size(670, 325);
            markerKeyTable.TabIndex = 0;
            markerKeyTable.SelectionChanged += MarkerKeyTable_SelectionChanged;
            markerKeyTable.SelectionClicked += MarkerKeyTable_SelectionClicked;
            markerKeyTable.PageNavigated += MarkerKeyTable_PageNavigated;
            // 
            // statusStrip
            // 
            statusStrip.ImageScalingSize = new System.Drawing.Size(32, 32);
            statusStrip.Items.AddRange(new ToolStripItem[] { pageAddressToolStripStatusLabel, errorImageToolStripStatusLabel, errorToolStripStatusLabel, serverToolStripStatusLabel, dataaseToolStripStatusLabel, toolStripStatusLabel3, markerDescriptionToolStripStatusLabel, offsetToolStripStatusLabel });
            statusStrip.Location = new System.Drawing.Point(0, 697);
            statusStrip.Name = "statusStrip";
            statusStrip.Padding = new Padding(1, 0, 16, 0);
            statusStrip.Size = new System.Drawing.Size(1020, 22);
            statusStrip.SizingGrip = false;
            statusStrip.TabIndex = 201;
            statusStrip.Text = "statusStrip";
            // 
            // pageAddressToolStripStatusLabel
            // 
            pageAddressToolStripStatusLabel.Name = "pageAddressToolStripStatusLabel";
            pageAddressToolStripStatusLabel.Size = new System.Drawing.Size(0, 17);
            // 
            // errorImageToolStripStatusLabel
            // 
            errorImageToolStripStatusLabel.DisplayStyle = ToolStripItemDisplayStyle.Image;
            errorImageToolStripStatusLabel.Name = "errorImageToolStripStatusLabel";
            errorImageToolStripStatusLabel.Size = new System.Drawing.Size(0, 17);
            errorImageToolStripStatusLabel.Text = "Error Image";
            // 
            // errorToolStripStatusLabel
            // 
            errorToolStripStatusLabel.Name = "errorToolStripStatusLabel";
            errorToolStripStatusLabel.Size = new System.Drawing.Size(0, 17);
            // 
            // serverToolStripStatusLabel
            // 
            serverToolStripStatusLabel.ForeColor = System.Drawing.Color.FromArgb(64, 64, 64);
            serverToolStripStatusLabel.Name = "serverToolStripStatusLabel";
            serverToolStripStatusLabel.Size = new System.Drawing.Size(47, 17);
            serverToolStripStatusLabel.Text = "[Server]";
            // 
            // dataaseToolStripStatusLabel
            // 
            dataaseToolStripStatusLabel.ForeColor = System.Drawing.Color.Gray;
            dataaseToolStripStatusLabel.Name = "dataaseToolStripStatusLabel";
            dataaseToolStripStatusLabel.Size = new System.Drawing.Size(63, 17);
            dataaseToolStripStatusLabel.Text = "[Database]";
            // 
            // toolStripStatusLabel3
            // 
            toolStripStatusLabel3.Name = "toolStripStatusLabel3";
            toolStripStatusLabel3.Size = new System.Drawing.Size(893, 17);
            toolStripStatusLabel3.Spring = true;
            // 
            // markerDescriptionToolStripStatusLabel
            // 
            markerDescriptionToolStripStatusLabel.AutoToolTip = true;
            markerDescriptionToolStripStatusLabel.BackColor = System.Drawing.Color.Red;
            markerDescriptionToolStripStatusLabel.ImageTransparentColor = System.Drawing.Color.FromArgb(64, 0, 0);
            markerDescriptionToolStripStatusLabel.Name = "markerDescriptionToolStripStatusLabel";
            markerDescriptionToolStripStatusLabel.Size = new System.Drawing.Size(0, 17);
            // 
            // offsetToolStripStatusLabel
            // 
            offsetToolStripStatusLabel.Name = "offsetToolStripStatusLabel";
            offsetToolStripStatusLabel.Size = new System.Drawing.Size(0, 17);
            // 
            // flatMenuStrip1
            // 
            flatMenuStrip1.AutoSize = false;
            flatMenuStrip1.GripStyle = ToolStripGripStyle.Hidden;
            flatMenuStrip1.ImageScalingSize = new System.Drawing.Size(32, 32);
            flatMenuStrip1.Items.AddRange(new ToolStripItem[] { toolStripLabel1, pageToolStripTextBox, previousToolStripButton, nextToolStripButton, toolStripSeparator2, toolStripLabel2, offsetTableToolStripTextBox, toolStripSeparator1, encodeAndFindToolStripButton, toolStripSeparator3, logToolStripLabel, logToolStripComboBox });
            flatMenuStrip1.Location = new System.Drawing.Point(0, 0);
            flatMenuStrip1.Name = "flatMenuStrip1";
            flatMenuStrip1.Size = new System.Drawing.Size(1020, 32);
            flatMenuStrip1.TabIndex = 0;
            flatMenuStrip1.Text = "flatMenuStrip1";
            // 
            // toolStripLabel1
            // 
            toolStripLabel1.Name = "toolStripLabel1";
            toolStripLabel1.Size = new System.Drawing.Size(36, 29);
            toolStripLabel1.Text = "Page:";
            // 
            // pageToolStripTextBox
            // 
            pageToolStripTextBox.AutoSize = false;
            pageToolStripTextBox.DatabaseId = 0;
            pageToolStripTextBox.ForeColor = System.Drawing.SystemColors.WindowText;
            pageToolStripTextBox.Name = "pageToolStripTextBox";
            pageToolStripTextBox.Padding = new Padding(0, 0, 2, 0);
            pageToolStripTextBox.Size = new System.Drawing.Size(101, 23);
            pageToolStripTextBox.Text = "(File Id: Page Id)";
            pageToolStripTextBox.KeyDown += PageToolStripTextBox_KeyDown;
            // 
            // previousToolStripButton
            // 
            previousToolStripButton.Image = Properties.Resources.Backward_16xMD;
            previousToolStripButton.ImageScaling = ToolStripItemImageScaling.None;
            previousToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            previousToolStripButton.Name = "previousToolStripButton";
            previousToolStripButton.Size = new System.Drawing.Size(101, 29);
            previousToolStripButton.Text = "Previous Page";
            previousToolStripButton.ToolTipText = "Page ID - 1";
            previousToolStripButton.Click += PreviousToolStripButton_Click;
            // 
            // nextToolStripButton
            // 
            nextToolStripButton.Image = Properties.Resources.Forward_grey_16xMD;
            nextToolStripButton.ImageScaling = ToolStripItemImageScaling.None;
            nextToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            nextToolStripButton.Name = "nextToolStripButton";
            nextToolStripButton.Size = new System.Drawing.Size(81, 29);
            nextToolStripButton.Text = "Next Page";
            nextToolStripButton.ToolTipText = "Page ID +1";
            nextToolStripButton.Click += NextToolStripButton_Click;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new System.Drawing.Size(6, 32);
            // 
            // toolStripLabel2
            // 
            toolStripLabel2.Name = "toolStripLabel2";
            toolStripLabel2.Size = new System.Drawing.Size(42, 29);
            toolStripLabel2.Text = "Offset:";
            // 
            // offsetTableToolStripTextBox
            // 
            offsetTableToolStripTextBox.AutoSize = false;
            offsetTableToolStripTextBox.Name = "offsetTableToolStripTextBox";
            offsetTableToolStripTextBox.Padding = new Padding(0, 0, 2, 0);
            offsetTableToolStripTextBox.Size = new System.Drawing.Size(33, 23);
            offsetTableToolStripTextBox.Text = "0000";
            offsetTableToolStripTextBox.KeyDown += OffsetTableToolStripTextBox_KeyDown;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new System.Drawing.Size(6, 32);
            // 
            // encodeAndFindToolStripButton
            // 
            encodeAndFindToolStripButton.Image = Properties.Resources.Search_16x;
            encodeAndFindToolStripButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            encodeAndFindToolStripButton.Name = "encodeAndFindToolStripButton";
            encodeAndFindToolStripButton.Size = new System.Drawing.Size(121, 29);
            encodeAndFindToolStripButton.Text = "Encode && Find";
            encodeAndFindToolStripButton.ToolTipText = "Encode & Find";
            encodeAndFindToolStripButton.Click += EncodeAndFindToolStripButton_Click;
            // 
            // toolStripSeparator3
            // 
            toolStripSeparator3.Name = "toolStripSeparator3";
            toolStripSeparator3.Size = new System.Drawing.Size(6, 32);
            // 
            // logToolStripLabel
            // 
            logToolStripLabel.Name = "logToolStripLabel";
            logToolStripLabel.Size = new System.Drawing.Size(93, 29);
            logToolStripLabel.Text = "Transaction Log:";
            logToolStripLabel.Visible = false;
            // 
            // logToolStripComboBox
            // 
            logToolStripComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            logToolStripComboBox.Items.AddRange(new object[] { "None", "Before", "After" });
            logToolStripComboBox.Name = "logToolStripComboBox";
            logToolStripComboBox.Padding = new Padding(0, 2, 0, 0);
            logToolStripComboBox.Size = new System.Drawing.Size(75, 32);
            logToolStripComboBox.Visible = false;
            logToolStripComboBox.SelectedIndexChanged += LogToolStripComboBox_SelectedIndexChanged;
            // 
            // PageViewerWindow
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(splitContainer1);
            Controls.Add(leftPanel);
            Controls.Add(flatMenuStrip1);
            Controls.Add(statusStrip);
            Margin = new Padding(4);
            Name = "PageViewerWindow";
            Size = new System.Drawing.Size(1020, 719);
            leftPanel.ResumeLayout(false);
            headerBorderPanel.ResumeLayout(false);
            headerBorderPanel.PerformLayout();
            ((ISupportInitialize)bcmPictureBox).EndInit();
            ((ISupportInitialize)dcmPictureBox).EndInit();
            ((ISupportInitialize)sGamPictureBox).EndInit();
            ((ISupportInitialize)gamPictureBox).EndInit();
            ((ISupportInitialize)pageBindingSource).EndInit();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            topLeftPanel.ResumeLayout(false);
            compressionInfoPanel.ResumeLayout(false);
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            flatMenuStrip1.ResumeLayout(false);
            flatMenuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
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
        private TextBox ObjectNameTextBox;
        private TextBox PageTypeTextBox;
        private TextBox textBox14;
        private TextBox textBox13;
        private TextBox textBox12;
        private TextBox textBox11;
        private TextBox textBox10;
        private TextBox textBox9;
        private TextBox textBox8;
        private TextBox textBox7;
        private TextBox textBox6;
        private TextBox AllocationUnitIdTextBox;
        private TextBox PartitionIdTextBox;
        private TextBox ObjectIdTextBox;
        private TextBox PreviousPageTextBox;
        private TextBox NextPageTextBox;
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
        private Label ObjectIdLabel;
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
