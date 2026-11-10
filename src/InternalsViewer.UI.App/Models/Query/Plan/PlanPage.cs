using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.Plans.Model;

namespace InternalsViewer.UI.App.Models.Query.Plan;

public sealed record PlanPage(PlanNode Node, PageAddress Page, ushort? Slot)
{
    public string Address => Slot is { } slot ? new RowIdentifier(Page, slot).ToString() : Page.ToString();
}
