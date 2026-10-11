using System;
using Windows.System;

namespace InternalsViewer.UI.App.Controls;

public sealed partial class WinDbgInstallDialog
{
    private const string StoreUri = "ms-windows-store://pdp/?ProductId=9PGJGD53TN86";

    public WinDbgInstallDialog(string reason)
    {
        Reason = reason;

        InitializeComponent();
    }

    public string Reason { get; }

    private void StoreLink_Click(object sender, RoutedEventArgs e) => _ = Launcher.LaunchUriAsync(new Uri(StoreUri));
}
