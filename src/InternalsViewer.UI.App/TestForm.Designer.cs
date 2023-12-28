using System.ComponentModel;

namespace InternalsViewer.UI.App;

partial class TestForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
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
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        allocationWindow = new AllocationWindow(DatabaseLoader);
        SuspendLayout();
        // 
        // allocationWindow
        // 
        allocationWindow.Dock = DockStyle.Fill;
        allocationWindow.Location = new Point(0, 0);
        allocationWindow.Margin = new Padding(4, 4, 4, 4);
        allocationWindow.Name = "allocationWindow";
        allocationWindow.Size = new Size(800, 450);
        allocationWindow.TabIndex = 1;
        allocationWindow.OnConnect += AllocationWindowOnConnect;
        allocationWindow.ViewPage += this.allocationWindow_ViewPage;
        // 
        // TestForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(800, 450);
        Controls.Add(allocationWindow);
        Name = "TestForm";
        Text = "Internals Viewer Test App";
        ResumeLayout(false);
    }

    #endregion

    private AllocationWindow allocationWindow;
}
