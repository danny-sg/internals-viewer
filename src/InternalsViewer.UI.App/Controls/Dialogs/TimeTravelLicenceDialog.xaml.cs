using System;

namespace InternalsViewer.UI.App.Controls.Dialogs;

public sealed partial class TimeTravelLicenceDialog
{
    public TimeTravelLicenceDialog(string licence)
    {
        Paragraphs = licence.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        InitializeComponent();
    }

    public string[] Paragraphs { get; }
}
