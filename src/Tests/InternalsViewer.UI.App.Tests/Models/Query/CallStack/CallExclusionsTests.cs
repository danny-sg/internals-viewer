using InternalsViewer.Query.CallStack;
using InternalsViewer.UI.App.Models.Query.CallStack;

namespace InternalsViewer.UI.App.Tests.Models.Query.CallStack;

public class CallExclusionsTests
{
    [Fact]
    public void A_Call_In_A_Hidden_Category_Is_Excluded_But_Its_Callees_Are_Not()
    {
        var tree = new CallStackTree();

        var hidden = tree.AddCall(tree.Root, Frame("Compilation", 0x10), 1);
        var callee = tree.AddCall(hidden, Frame("sqlmin", 0x20), 1);

        var exclusions = new CallExclusions(new HashSet<string> { "Compilation" }, new HashSet<string>());

        Assert.False(exclusions.Includes(hidden));
        Assert.True(exclusions.Includes(callee));
    }

    [Fact]
    public void A_Pruned_Category_Excludes_Everything_Beneath_It()
    {
        var tree = new CallStackTree();

        var pruned = tree.AddCall(tree.Root, Frame("Compilation", 0x10), 1);
        var callee = tree.AddCall(pruned, Frame("sqlmin", 0x20), 1);
        var sibling = tree.AddCall(tree.Root, Frame("sqlmin", 0x30), 1);

        var exclusions = new CallExclusions(new HashSet<string>(), new HashSet<string> { "Compilation" });

        Assert.False(exclusions.Includes(pruned));
        Assert.False(exclusions.Includes(callee));
        Assert.True(exclusions.Includes(sibling));
    }

    [Fact]
    public void A_Call_Not_Placed_In_The_Tree_Is_Kept()
    {
        var exclusions = new CallExclusions(new HashSet<string> { "Compilation" }, new HashSet<string> { "Compilation" });

        Assert.True(exclusions.Includes(null));
    }

    private static CallstackFrame Frame(string module, uint rva) => new() { Module = module, Rva = rva };
}
