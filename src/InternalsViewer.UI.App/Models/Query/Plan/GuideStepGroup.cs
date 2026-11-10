using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.UI.App.Models.Query.Plan;

public sealed class GuideStepGroup(string operatorName, PlanNode? node, Uri? icon, int firstIndex, int depth)
    : ObservableCollection<GuideStepItem>
{
    public string OperatorName => operatorName;

    public PlanNode? Node => node;

    public Uri? Icon => icon;

    public int FirstIndex => firstIndex;

    public int Depth => depth;

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

    public void Show(GuideStepItem item) => Insert(GetPosition(item.Index), item);

    public void Hide(GuideStepItem item) => RemoveAt(GetPosition(item.Index));

    private int GetPosition(int index)
    {
        var low = 0;

        var high = Count;

        while (low < high)
        {
            var middle = (low + high) / 2;

            if (this[middle].Index < index)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
