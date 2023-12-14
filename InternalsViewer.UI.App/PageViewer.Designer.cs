using System.ComponentModel;

namespace InternalsViewer.UI.App;

partial class PageViewer
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

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        pageViewerWindow = new PageViewerWindow(PageService);

        SuspendLayout();
        // 
        // pageViewerWindow1
        // 
        pageViewerWindow.ConnectionString = null;
        pageViewerWindow.Dock = DockStyle.Fill;
        pageViewerWindow.Location = new Point(0, 0);
        pageViewerWindow.Margin = new Padding(4, 4, 4, 4);
        pageViewerWindow.Name = "pageViewerWindow";
        pageViewerWindow.Page = null;
        pageViewerWindow.Size = new Size(1008, 729);
        pageViewerWindow.TabIndex = 0;
        // 
        // PageViewer
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1008, 729);
        Controls.Add(pageViewerWindow);
        Name = "PageViewer";
        Text = "PageViewer";
        ResumeLayout(false);
    }

    #endregion

    private PageViewerWindow pageViewerWindow;
}