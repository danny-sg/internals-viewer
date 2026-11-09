using System.Collections.ObjectModel;
using InternalsViewer.UI.App.Models.Query.Plan;

namespace InternalsViewer.UI.App.Tests.Models.Query.Plan;

[Trait("Category", "Unit")]
[Trait("Area", "Trace")]
public class GuideStepTreeTests
{
    [Fact]
    public void Collapsing_A_Call_Hides_What_Is_Inside_It_And_Its_Return()
    {
        var (tree, items, groups) = Create();

        tree.Toggle(items[1]);

        Assert.Equal([0, 1, 5], Shown(groups));
    }

    [Fact]
    public void A_Group_With_Nothing_Left_To_Show_Is_Removed()
    {
        var (tree, items, groups) = Create();

        tree.Toggle(items[1]);

        Assert.Equal(["Outer", "After"], groups.Select(g => g.OperatorName));
    }

    [Fact]
    public void Expanding_A_Call_Puts_Its_Steps_And_Groups_Back_In_Order()
    {
        var (tree, items, groups) = Create();

        tree.Toggle(items[1]);

        tree.Toggle(items[1]);

        Assert.Equal([0, 1, 2, 3, 4, 5], Shown(groups));

        Assert.Equal(["Outer", "Inner", "After"], groups.Select(g => g.OperatorName));
    }

    [Fact]
    public void Expanding_A_Call_Leaves_A_Collapsed_Call_Inside_It_Collapsed()
    {
        var (tree, items, groups) = Create();

        tree.Toggle(items[2]);

        tree.Toggle(items[1]);

        tree.Toggle(items[1]);

        Assert.Equal([0, 1, 2, 4, 5], Shown(groups));
    }

    [Fact]
    public void Revealing_A_Step_Expands_Every_Collapsed_Call_Around_It()
    {
        var (tree, items, groups) = Create();

        tree.Toggle(items[2]);

        tree.Toggle(items[1]);

        tree.Reveal(items[3]);

        Assert.Equal([0, 1, 2, 3, 4, 5], Shown(groups));
    }

    [Fact]
    public void Moving_Forward_Into_A_Collapsed_Call_Goes_Past_It()
    {
        var (tree, items, _) = Create();

        tree.Toggle(items[1]);

        Assert.Equal(5, tree.GetShownIndex(2, forward: true));
    }

    [Fact]
    public void Moving_Back_Into_A_Collapsed_Call_Stops_On_The_Call()
    {
        var (tree, items, _) = Create();

        tree.Toggle(items[1]);

        Assert.Equal(1, tree.GetShownIndex(4, forward: false));
    }

    [Fact]
    public void A_Shown_Step_Keeps_Its_Index()
    {
        var (tree, _, _) = Create();

        Assert.Equal(3, tree.GetShownIndex(3, forward: true));
    }

    private static int[] Shown(IEnumerable<GuideStepGroup> groups) => [.. groups.SelectMany(g => g).Select(i => i.Index)];

    private static (GuideStepTree Tree, List<GuideStepItem> Items, ObservableCollection<GuideStepGroup> Groups) Create()
    {
        var outer = new GuideStepGroup("Outer", null, null, 0, 0);

        var inner = new GuideStepGroup("Inner", null, null, 2, 1);

        var after = new GuideStepGroup("After", null, null, 5, 0);

        var items = new List<GuideStepItem>();

        Add(items, outer, -1, 0, 0);
        Add(items, outer, -1, 0, 4);
        Add(items, inner, 1, 1, 3);
        Add(items, inner, 2, 2, 3);
        Add(items, inner, 1, 1, 4);
        Add(items, after, -1, 0, 5);

        ObservableCollection<GuideStepGroup> groups = [outer, inner, after];

        return (new GuideStepTree(items, groups), items, groups);
    }

    private static void Add(List<GuideStepItem> items, GuideStepGroup group, int parent, int depth, int end)
    {
        var item = new GuideStepItem(items.Count, "", "", "", null, group, parent >= 0 ? items[parent] : null, depth, end);

        group.Add(item);

        items.Add(item);
    }
}
