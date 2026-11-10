using System.Threading;
using System.Threading.Tasks;
using InternalsViewer.Query.FullTrace.Recording;
using InternalsViewer.Query.Recording;
using InternalsViewer.UI.App.Controls.Dialogs;
using InternalsViewer.UI.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Dialogs;

public static class TimeTravelConsent
{
    private static SettingsViewModel Settings => App.GetService<SettingsViewModel>();

    private static ITimeTravelRecorder TimeTravel => App.GetService<ITimeTravelRecorder>();

    public static async Task<bool> ConfirmAsync(XamlRoot xamlRoot)
    {
        if (Settings.ShowTimeTravelWarning)
        {
            var dialog = new TimeTravelWarningDialog
            {
                XamlRoot = xamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return false;
            }

            if (dialog.DoNotShowAgain)
            {
                Settings.ShowTimeTravelWarning = false;
            }
        }

        return await PrepareAsync(xamlRoot);
    }

    private static async Task<bool> PrepareAsync(XamlRoot xamlRoot)
    {
        try
        {
            TimeTravel.PrepareRecorder();

            if (TimeTravel.IsLicenceAccepted)
            {
                return true;
            }

            var dialog = new TimeTravelLicenceDialog(TimeTravel.ReadLicence())
            {
                XamlRoot = xamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return false;
            }

            await TimeTravel.AcceptLicenceAsync(CancellationToken.None);

            return true;
        }
        catch (DebuggerException exception)
        {
            var dialog = exception is WinDbgNotFoundException
                ? new WinDbgInstallDialog(exception.Message)
                : new ContentDialog
                {
                    Title = "Time Travel Debugging Licence",
                    Content = exception.Message,
                    CloseButtonText = "OK"
                };

            dialog.XamlRoot = xamlRoot;
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;

            await dialog.ShowAsync();

            return false;
        }
    }
}
