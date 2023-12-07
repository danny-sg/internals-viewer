namespace InternalsViewer.UI.App;

partial class PageViewer
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

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        pageViewerWindow1 = new PageViewerWindow();
        SuspendLayout();
        // 
        // pageViewerWindow1
        // 
        pageViewerWindow1.ConnectionString = null;
        pageViewerWindow1.Dock = DockStyle.Fill;
        pageViewerWindow1.Location = new Point(0, 0);
        pageViewerWindow1.Margin = new Padding(4, 4, 4, 4);
        pageViewerWindow1.Name = "pageViewerWindow1";
        pageViewerWindow1.Page = null;
        pageViewerWindow1.Size = new Size(800, 450);
        pageViewerWindow1.TabIndex = 0;
        // 
        // PageViewer
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(800, 450);
        Controls.Add(pageViewerWindow1);
        Name = "PageViewer";
        Text = "PageViewer";
        ResumeLayout(false);
    }

    #endregion

    private PageViewerWindow pageViewerWindow1;
}