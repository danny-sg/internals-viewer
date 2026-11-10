using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace InternalsViewer.UI.App.Models.Query.Plan;

public sealed class GuideStepTree(IReadOnlyList<GuideStepItem> items, ObservableCollection<GuideStepGroup> groups)
{
    public void Toggle(GuideStepItem call)
    {
        call.IsExpanded = !call.IsExpanded;

        UpdateNested(call);
    }

    public void Reveal(GuideStepItem item)
    {
        var collapsed = GetCollapsedCalls(item);

        for (var index = collapsed.Count - 1; index >= 0; index--)
        {
            Toggle(collapsed[index]);
        }
    }

    public int GetShownIndex(int index, bool forward)
    {
        if (index < 0 || index >= items.Count || GetCollapsedCalls(items[index]) is not { Count: > 0 } collapsed)
        {
            return index;
        }

        return forward ? collapsed[^1].End + 1 : collapsed[^1].Index;
    }

    private static List<GuideStepItem> GetCollapsedCalls(GuideStepItem item)
    {
        var collapsed = new List<GuideStepItem>();

        for (var call = item.Parent; call is not null; call = call.Parent)
        {
            if (!call.IsExpanded)
            {
                collapsed.Add(call);
            }
        }

        return collapsed;
    }

    private void UpdateNested(GuideStepItem call)
    {
        var isShown = call.IsExpanded && call.IsShown;

        for (var index = call.Index + 1; index <= call.End; index++)
        {
            var item = items[index];

            SetShown(item, isShown);

            if (isShown && !item.IsExpanded)
            {
                index = item.End;
            }
        }
    }

    private void SetShown(GuideStepItem item, bool isShown)
    {
        if (item.IsShown == isShown)
        {
            return;
        }

        item.IsShown = isShown;

        var group = item.Group;

        if (isShown)
        {
            group.Show(item);

            if (group.Count == 1)
            {
                groups.Insert(GetGroupPosition(group.FirstIndex), group);
            }
        }
        else
        {
            group.Hide(item);

            if (group.Count == 0)
            {
                groups.RemoveAt(GetGroupPosition(group.FirstIndex));
            }
        }
    }

    private int GetGroupPosition(int firstIndex)
    {
        var low = 0;

        var high = groups.Count;

        while (low < high)
        {
            var middle = (low + high) / 2;

            if (groups[middle].FirstIndex < firstIndex)
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
