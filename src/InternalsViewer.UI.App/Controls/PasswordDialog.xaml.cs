namespace InternalsViewer.UI.App.Controls;

public sealed partial class PasswordDialog
{
    public string Password
    {
        get { return (string)GetValue(PasswordProperty); }
        set { SetValue(PasswordProperty, value); }
    }

    public static readonly DependencyProperty PasswordProperty =
        DependencyProperty.Register(nameof(Password), typeof(string), typeof(PasswordDialog), new PropertyMetadata(null));

    public PasswordDialog()
    {
        InitializeComponent();
    }
}
