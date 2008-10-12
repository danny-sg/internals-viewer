namespace InternalsViewer.SSMSAddIn
{
    partial class PageViewerWindow
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
            this.pageViewerWindow1 = new InternalsViewer.UI.PageViewerWindow();
            this.SuspendLayout();
            // 
            // pageViewerWindow1
            // 
            this.pageViewerWindow1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pageViewerWindow1.Location = new System.Drawing.Point(0, 0);
            this.pageViewerWindow1.Name = "pageViewerWindow1";
            this.pageViewerWindow1.Size = new System.Drawing.Size(150, 150);
            this.pageViewerWindow1.TabIndex = 0;
            // 
            // PageViewerWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.pageViewerWindow1);
            this.Name = "PageViewerWindow";
            this.ResumeLayout(false);

        }

        #endregion

        private InternalsViewer.UI.PageViewerWindow pageViewerWindow1;
    }
}
