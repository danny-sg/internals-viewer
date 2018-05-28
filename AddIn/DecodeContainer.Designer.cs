namespace InternalsViewer.SSMSAddIn
{
    partial class DecodeContainer
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
            this.decodeWindow = new InternalsViewer.UI.DecodeWindow();
            this.SuspendLayout();
            // 
            // decodeWindow
            // 
            this.decodeWindow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.decodeWindow.Location = new System.Drawing.Point(0, 0);
            this.decodeWindow.MinimumSize = new System.Drawing.Size(376, 135);
            this.decodeWindow.Name = "decodeWindow";
            this.decodeWindow.Size = new System.Drawing.Size(376, 150);
            this.decodeWindow.TabIndex = 0;
            // 
            // DecodeContainer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.decodeWindow);
            this.Name = "DecodeContainer";
            this.Size = new System.Drawing.Size(353, 150);
            this.ResumeLayout(false);

        }

        #endregion

        private InternalsViewer.UI.DecodeWindow decodeWindow;


    }
}
