using InternalsViewer.UI.App.Properties;
using Microsoft.Data.SqlClient;

namespace InternalsViewer.UI.App;

public partial class ConnectionForm : Form
{
    public string ConnectionString { get; set; } = string.Empty;

    public ConnectionForm()
    {
        InitializeComponent();

        ServerNameTextBox.Text = Settings.Default.ServerName;
        AuthenticationTypeComboBox.SelectedIndex = Settings.Default.IsIntegratedSecurity ? 0 : 1;
        UserNametextBox.Text = Settings.Default.UserName;
    }

    private void ConnectButton_Click(object sender, EventArgs e)
    {
        var serverName = ServerNameTextBox.Text;
        var isIntegratedSecurity = AuthenticationTypeComboBox.SelectedIndex == 0;
        var userName = UserNametextBox.Text;
        var password = PasswordTextBox.Text;

        ConnectionString = BuildConnectionString(serverName, isIntegratedSecurity, userName, password);

        Settings.Default.ServerName = serverName;
        Settings.Default.IsIntegratedSecurity = isIntegratedSecurity;
        Settings.Default.UserName = userName;

        Settings.Default.Save();

        DialogResult = DialogResult.OK;

        Close();
    }

    private string BuildConnectionString(string serverName, bool isIntegratedSecurity, string userName, string password)
    {
        var builder = new SqlConnectionStringBuilder();

        builder.DataSource = serverName;
        builder.IntegratedSecurity = isIntegratedSecurity;
        builder.ApplicationName = "Internals Viewer";

        builder.TrustServerCertificate = true;

        if (isIntegratedSecurity)
        {
            builder.UserID = userName;
            builder.Password = password;
        };

        return builder.ConnectionString;
    }

    private void AuthenticationTypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (AuthenticationTypeComboBox.SelectedIndex == 0)
        {
            UserNametextBox.Enabled = false;
            PasswordTextBox.Enabled = false;
        }
        else
        {
            UserNametextBox.Enabled = true;
            PasswordTextBox.Enabled = true;
        }
    }

    private void CancelButton_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
    }
}
