namespace InternalsViewer.SSMSAddIn
{
    partial class PageViewerContainer
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
            this.pageViewerWindow = new InternalsViewer.UI.PageViewerWindow();
            this.SuspendLayout();
            // 
            // pageViewerWindow
            // 
            this.pageViewerWindow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pageViewerWindow.Location = new System.Drawing.Point(0, 0);
            this.pageViewerWindow.Name = "pageViewerWindow";
            this.pageViewerWindow.Page = null;
            this.pageViewerWindow.Size = new System.Drawing.Size(800, 600);
            this.pageViewerWindow.TabIndex = 0;
            // 
            // PageViewerContainer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.pageViewerWindow);
            this.Name = "PageViewerContainer";
            this.Size = new System.Drawing.Size(800, 600);
            this.ResumeLayout(false);

        }

        #endregion

        private InternalsViewer.UI.PageViewerWindow pageViewerWindow;
    }
}
