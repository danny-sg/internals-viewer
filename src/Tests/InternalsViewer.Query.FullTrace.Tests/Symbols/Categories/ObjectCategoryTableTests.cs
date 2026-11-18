using InternalsViewer.Query.FullTrace.Symbols.Categories;
using InternalsViewer.Query.FullTrace.Symbols.Categories.Enums;

namespace InternalsViewer.Query.FullTrace.Tests.Symbols.Categories;

[Trait("Category", "Unit")]
public class ObjectCategoryTableTests
{
    [Fact]
    public void Every_Matching_Rule_Adds_Its_Badge_In_File_Order()
    {
        var table = Load("CValRef*|Value Reference|Values", "CValRefAlg*|Algebrizer|ParseAndBind");

        Assert.Equal(["Value Reference", "Algebrizer"], table.GetBadges("CValRefAlgTable").Select(b => b.Name));
    }

    [Fact]
    public void A_Badge_Named_By_Two_Rules_Is_Shown_Once()
    {
        var table = Load("CQScan*|Iterator|Execution", "*TopNew|Iterator|Execution");

        Assert.Equal(["Iterator"], table.GetBadges("CQScanTopNew").Select(b => b.Name));
    }

    [Fact]
    public void A_Badge_Name_Used_In_Two_Stages_Keeps_Each_Stage()
    {
        var table = Load("CBpBatch*|Batch|Execution", "CValRefBatch*|Batch|ValueReferences");

        Assert.Equal(ObjectStage.Execution, table.GetBadges("CBpBatchColumn").Single().Stage);

        Assert.Equal(ObjectStage.ValueReferences, table.GetBadges("CValRefBatchRedirect").Single().Stage);
    }

    [Fact]
    public void A_Class_That_Matches_No_Rule_Has_No_Badges()
    {
        var table = Load("CQScan*|Iterator|Execution");

        Assert.Empty(table.GetBadges("CXteTop"));

        Assert.Empty(table.GetBadges(null));
    }

    [Fact]
    public void A_Row_With_An_Unknown_Stage_Is_Skipped()
    {
        var table = Load("Class|Badge|Stage", "CQScan*|Iterator|Nowhere");

        Assert.Empty(table.GetBadges("CQScanTopNew"));
    }

    [Fact]
    public void A_Badge_Names_Its_Stage()
    {
        var table = Load("CQScan*|Iterator|Execution");

        var badge = Assert.Single(table.GetBadges("CQScanTopNew"));

        Assert.Equal((ObjectStage.Execution, "Execution"), (badge.Stage, badge.StageName));
    }

    [Fact]
    public void Each_Badge_In_A_Stage_Takes_Its_Own_Place_In_The_Stage()
    {
        var table = Load("CQScan*|Iterator|Execution",
                         "CEsExec|Expression Executor|Execution",
                         "CVal*|Value Provider|Values",
                         "CBpBatch*|Batch|Execution");

        var places = new[] { "CQScanTopNew", "CEsExec", "CValExpr", "CBpBatch" }.Select(c => table.GetBadges(c).Single())
                                                                                 .Select(b => (b.Index, b.Count));

        Assert.Equal([(0, 3), (1, 3), (0, 1), (2, 3)], places);
    }

    [Theory]
    [InlineData("CXteTableScan", "Compiled Plan")]
    [InlineData("CQScanTableScanNew", "Iterator")]
    [InlineData("CValRefSS", "Value Reference,Storage Engine")]
    [InlineData("CValRefOrdinalEx", "Value Reference,Operator")]
    [InlineData("CValRefRID", "Value Reference,RID")]
    [InlineData("CValRefOptTable", "Optimizer,Value Reference,Table")]
    [InlineData("CValRefAlgTable", "Algebrizer,Value Reference,Table")]
    [InlineData("CValSSTable", "Value Provider,Storage Engine")]
    [InlineData("CValExpr", "Value Provider,Expression")]
    [InlineData("CValHashCachedSwitch", "Value Provider,Hash Switch,Hash Table")]
    [InlineData("CValEsConstXVar", "Value Provider,Constant")]
    [InlineData("CMEDProxyIndex", "Metadata,Proxy")]
    public void The_Default_Table_Names_Objects_By_Class(string className, string expected)
    {
        Assert.Equal(expected.Split(','), ObjectCategoryTable.Default.GetBadges(className).Select(b => b.Name));
    }

    private static ObjectCategoryTable Load(params string[] lines) => ObjectCategoryTable.Load(new StringReader(string.Join("\n", lines)));
}
