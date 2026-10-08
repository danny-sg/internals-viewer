using InternalsViewer.UI.App.Services.Query.Debugging;

namespace InternalsViewer.UI.App.Controls;

public sealed partial class TimeTravelWarningDialog
{
    public TimeTravelWarningDialog()
    {
        InitializeComponent();
    }

    public bool DoNotShowAgain => DoNotShowAgainCheckBox.IsChecked == true;

    public string TraceDirectory => TimeTravelRecorder.TraceRoot;
}
