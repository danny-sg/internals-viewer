namespace InternalsViewer.UI.App.Controls;

public sealed partial class WinDbgInstallDialog
{
    public WinDbgInstallDialog(string reason)
    {
        Reason = reason;

        InitializeComponent();
    }

    public string Reason { get; }
}
