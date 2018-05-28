namespace InternalsViewer.UI
{
    partial class DecodeWindow
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

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
            this.keyTextBox = new System.Windows.Forms.RichTextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.hexTextBox = new System.Windows.Forms.RichTextBox();
            this.findButton = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.dataTypeComboBox = new System.Windows.Forms.ComboBox();
            this.findTextBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // keyTextBox
            // 
            this.keyTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.keyTextBox.BackColor = System.Drawing.SystemColors.Control;
            this.keyTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.keyTextBox.Cursor = System.Windows.Forms.Cursors.Default;
            this.keyTextBox.DetectUrls = false;
            this.keyTextBox.Location = new System.Drawing.Point(71, 100);
            this.keyTextBox.Multiline = false;
            this.keyTextBox.Name = "keyTextBox";
            this.keyTextBox.ReadOnly = true;
            this.keyTextBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.None;
            this.keyTextBox.Size = new System.Drawing.Size(130, 19);
            this.keyTextBox.TabIndex = 25;
            this.keyTextBox.Text = "";
            // 
            // label6
            // 
            this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.label6.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.label6.Location = new System.Drawing.Point(8, 64);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(361, 2);
            this.label6.TabIndex = 24;
            // 
            // hexTextBox
            // 
            this.hexTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.hexTextBox.BackColor = System.Drawing.Color.White;
            this.hexTextBox.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.hexTextBox.Location = new System.Drawing.Point(71, 79);
            this.hexTextBox.Multiline = false;
            this.hexTextBox.Name = "hexTextBox";
            this.hexTextBox.ReadOnly = true;
            this.hexTextBox.Size = new System.Drawing.Size(297, 19);
            this.hexTextBox.TabIndex = 23;
            this.hexTextBox.Text = "";
            // 
            // findButton
            // 
            this.findButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.findButton.Location = new System.Drawing.Point(294, 104);
            this.findButton.Name = "findButton";
            this.findButton.Size = new System.Drawing.Size(75, 23);
            this.findButton.TabIndex = 21;
            this.findButton.Text = "&Find";
            this.findButton.UseVisualStyleBackColor = true;
            this.findButton.Click += new System.EventHandler(this.FindButton_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(9, 82);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(29, 13);
            this.label3.TabIndex = 20;
            this.label3.Text = "Hex:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(9, 35);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(60, 13);
            this.label2.TabIndex = 19;
            this.label2.Text = "Data Type:";
            // 
            // dataTypeComboBox
            // 
            this.dataTypeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.dataTypeComboBox.FormattingEnabled = true;
            this.dataTypeComboBox.Items.AddRange(new object[] {
            "binary",
            "varchar",
            "nvarchar",
            "tinyint",
            "smallint",
            "bigint",
            "int",
            "float",
            "decimal",
            "float",
            "money",
            "smallmoney",
            "real",
            "datetime",
            "smalldatetime"});
            this.dataTypeComboBox.Location = new System.Drawing.Point(71, 32);
            this.dataTypeComboBox.Name = "dataTypeComboBox";
            this.dataTypeComboBox.Size = new System.Drawing.Size(100, 21);
            this.dataTypeComboBox.TabIndex = 18;
            this.dataTypeComboBox.SelectedIndexChanged += new System.EventHandler(this.DataTypeComboBox_SelectedIndexChanged);
            // 
            // findTextBox
            // 
            this.findTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.findTextBox.Location = new System.Drawing.Point(71, 6);
            this.findTextBox.Name = "findTextBox";
            this.findTextBox.Size = new System.Drawing.Size(297, 20);
            this.findTextBox.TabIndex = 17;
            this.findTextBox.TextChanged += new System.EventHandler(this.FindTextBox_TextChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(37, 13);
            this.label1.TabIndex = 16;
            this.label1.Text = "Value:";
            // 
            // DecodeWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.keyTextBox);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.hexTextBox);
            this.Controls.Add(this.findButton);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.dataTypeComboBox);
            this.Controls.Add(this.findTextBox);
            this.Controls.Add(this.label1);
            this.MinimumSize = new System.Drawing.Size(376, 135);
            this.Name = "DecodeWindow";
            this.Size = new System.Drawing.Size(376, 135);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RichTextBox keyTextBox;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.RichTextBox hexTextBox;
        private System.Windows.Forms.Button findButton;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox dataTypeComboBox;
        private System.Windows.Forms.TextBox findTextBox;
        private System.Windows.Forms.Label label1;

    }
}
