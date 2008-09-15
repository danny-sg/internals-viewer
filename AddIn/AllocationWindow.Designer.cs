namespace InternalsViewer.SSMSAddIn
{
    partial class AllocationWindow
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
            this.allocationWindow1 = new InternalsViewer.UI.AllocationWindow();
            this.SuspendLayout();
            // 
            // allocationWindow1
            // 
            this.allocationWindow1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.allocationWindow1.Location = new System.Drawing.Point(0, 0);
            this.allocationWindow1.Name = "allocationWindow1";
            this.allocationWindow1.Size = new System.Drawing.Size(800, 600);
            this.allocationWindow1.TabIndex = 0;
            // 
            // AllocationWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.allocationWindow1);
            this.Name = "AllocationWindow";
            this.Size = new System.Drawing.Size(800, 600);
            this.ResumeLayout(false);

        }

        #endregion

        private InternalsViewer.UI.AllocationWindow allocationWindow1;
    }
}
