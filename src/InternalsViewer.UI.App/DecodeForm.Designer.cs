namespace InternalsViewer.UI.App;

partial class DecodeForm
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
        decodeWindow = new DecodeWindow();
        SuspendLayout();
        // 
        // decodeWindow1
        // 
        decodeWindow.Dock = DockStyle.Fill;
        decodeWindow.Location = new Point(0, 0);
        decodeWindow.Margin = new Padding(4, 3, 4, 3);
        decodeWindow.MinimumSize = new Size(439, 156);
        decodeWindow.Name = "decodeWindow";
        decodeWindow.ParentWindow = null;
        decodeWindow.Size = new Size(800, 166);
        decodeWindow.TabIndex = 0;
        // 
        // DecodeForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(800, 166);
        Controls.Add(decodeWindow);
        DoubleBuffered = true;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "DecodeForm";
        Text = "Decode";
        ResumeLayout(false);
    }

    #endregion

    private DecodeWindow decodeWindow;
}