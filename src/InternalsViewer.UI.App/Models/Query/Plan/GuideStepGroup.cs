using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query.Plan;

public sealed class GuideStepGroup(string operatorName, Uri? icon) : ObservableCollection<GuideStepItem>
{
    public string OperatorName => operatorName;

    public Uri? Icon => icon;

    public bool IsCurrent
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsCurrent)));
        }
    }
}
