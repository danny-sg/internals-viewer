using InternalsViewer.Query.FullTrace.Recording;

namespace InternalsViewer.UI.App.Controls.Dialogs;

public sealed partial class TimeTravelWarningDialog
{
    public TimeTravelWarningDialog()
    {
        InitializeComponent();
    }

    public bool DoNotShowAgain => DoNotShowAgainCheckBox.IsChecked == true;

    public string TraceDirectory => TimeTravelRecorder.TraceRoot;
}
