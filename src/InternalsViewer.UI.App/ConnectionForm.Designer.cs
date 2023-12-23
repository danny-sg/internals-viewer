using InternalsViewer.UI.Controls;

namespace InternalsViewer.UI.App;

partial class ConnectionForm
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
        ServerNameLabel = new Label();
        AuthenticationLabel = new Label();
        ServerNameTextBox = new TextBox();
        AuthenticationTypeComboBox = new ComboBox();
        UserNameLabel = new Label();
        UserNametextBox = new TextBox();
        PasswordTextBox = new TextBox();
        PasswordLabel = new Label();
        CancelButton = new Button();
        ConnectButton = new Button();
        SuspendLayout();
        // 
        // ServerNameLabel
        // 
        ServerNameLabel.AutoSize = true;
        ServerNameLabel.Location = new Point(29, 38);
        ServerNameLabel.Margin = new Padding(7, 0, 7, 0);
        ServerNameLabel.Name = "ServerNameLabel";
        ServerNameLabel.Size = new Size(194, 41);
        ServerNameLabel.TabIndex = 0;
        ServerNameLabel.Text = "Server Name:";
        // 
        // AuthenticationLabel
        // 
        AuthenticationLabel.AutoSize = true;
        AuthenticationLabel.Location = new Point(29, 118);
        AuthenticationLabel.Margin = new Padding(7, 0, 7, 0);
        AuthenticationLabel.Name = "AuthenticationLabel";
        AuthenticationLabel.Size = new Size(219, 41);
        AuthenticationLabel.TabIndex = 1;
        AuthenticationLabel.Text = "Authentication:";
        // 
        // ServerNameTextBox
        // 
        ServerNameTextBox.BorderStyle = BorderStyle.FixedSingle;
        ServerNameTextBox.Location = new Point(338, 30);
        ServerNameTextBox.Margin = new Padding(7, 8, 7, 8);
        ServerNameTextBox.Name = "ServerNameTextBox";
        ServerNameTextBox.Size = new Size(1046, 47);
        ServerNameTextBox.TabIndex = 2;
        // 
        // AuthenticationTypeComboBox
        // 
        AuthenticationTypeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        AuthenticationTypeComboBox.FormattingEnabled = true;
        AuthenticationTypeComboBox.Items.AddRange(new object[] { "Windows Authentication", "SQL Server Authentication" });
        AuthenticationTypeComboBox.Location = new Point(338, 109);
        AuthenticationTypeComboBox.Margin = new Padding(7, 8, 7, 8);
        AuthenticationTypeComboBox.Name = "AuthenticationTypeComboBox";
        AuthenticationTypeComboBox.Size = new Size(1046, 49);
        AuthenticationTypeComboBox.TabIndex = 3;
        AuthenticationTypeComboBox.SelectedIndexChanged += AuthenticationTypeComboBox_SelectedIndexChanged;
        // 
        // UserNameLabel
        // 
        UserNameLabel.AutoSize = true;
        UserNameLabel.Location = new Point(56, 197);
        UserNameLabel.Margin = new Padding(7, 0, 7, 0);
        UserNameLabel.Name = "UserNameLabel";
        UserNameLabel.Size = new Size(167, 41);
        UserNameLabel.TabIndex = 4;
        UserNameLabel.Text = "User name:";
        // 
        // UserNametextBox
        // 
        UserNametextBox.BorderStyle = BorderStyle.FixedSingle;
        UserNametextBox.Location = new Point(389, 189);
        UserNametextBox.Margin = new Padding(7, 8, 7, 8);
        UserNametextBox.Name = "UserNametextBox";
        UserNametextBox.Size = new Size(998, 47);
        UserNametextBox.TabIndex = 5;
        // 
        // PasswordTextBox
        // 
        PasswordTextBox.BorderStyle = BorderStyle.FixedSingle;
        PasswordTextBox.Location = new Point(389, 268);
        PasswordTextBox.Margin = new Padding(7, 8, 7, 8);
        PasswordTextBox.Name = "PasswordTextBox";
        PasswordTextBox.PasswordChar = '*';
        PasswordTextBox.Size = new Size(998, 47);
        PasswordTextBox.TabIndex = 6;
        // 
        // PasswordLabel
        // 
        PasswordLabel.AutoSize = true;
        PasswordLabel.Location = new Point(56, 276);
        PasswordLabel.Margin = new Padding(7, 0, 7, 0);
        PasswordLabel.Name = "PasswordLabel";
        PasswordLabel.Size = new Size(150, 41);
        PasswordLabel.TabIndex = 7;
        PasswordLabel.Text = "Password:";
        // 
        // CancelButton
        // 
        CancelButton.Location = new Point(1207, 347);
        CancelButton.Margin = new Padding(7, 8, 7, 8);
        CancelButton.Name = "CancelButton";
        CancelButton.Size = new Size(182, 63);
        CancelButton.TabIndex = 8;
        CancelButton.Text = "Cancel";
        CancelButton.UseVisualStyleBackColor = true;
        CancelButton.Click += CancelButton_Click;
        // 
        // ConnectButton
        // 
        ConnectButton.Location = new Point(1010, 347);
        ConnectButton.Margin = new Padding(7, 8, 7, 8);
        ConnectButton.Name = "ConnectButton";
        ConnectButton.Size = new Size(182, 63);
        ConnectButton.TabIndex = 9;
        ConnectButton.Text = "Connect";
        ConnectButton.UseVisualStyleBackColor = true;
        ConnectButton.Click += ConnectButton_Click;
        // 
        // ConnectionForm
        // 
        AcceptButton = ConnectButton;
        AutoScaleDimensions = new SizeF(17F, 41F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1418, 435);
        Controls.Add(ConnectButton);
        Controls.Add(CancelButton);
        Controls.Add(PasswordLabel);
        Controls.Add(PasswordTextBox);
        Controls.Add(UserNametextBox);
        Controls.Add(UserNameLabel);
        Controls.Add(AuthenticationTypeComboBox);
        Controls.Add(ServerNameTextBox);
        Controls.Add(AuthenticationLabel);
        Controls.Add(ServerNameLabel);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Margin = new Padding(7, 8, 7, 8);
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "ConnectionForm";
        SizeGripStyle = SizeGripStyle.Hide;
        Text = "Connect to Database Engine";
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private Label ServerNameLabel;
    private Label AuthenticationLabel;
    private ComboBox AuthenticationTypeComboBox;
    private Label UserNameLabel;
    private Label PasswordLabel;
    private new Button CancelButton;
    private Button ConnectButton;
    private TextBox ServerNameTextBox;
    private TextBox UserNametextBox;
    private TextBox PasswordTextBox;
}