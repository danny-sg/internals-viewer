using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Internals.Engine.Database.Enums;
using InternalsViewer.Internals.Interfaces.Connections;
using InternalsViewer.Internals.Providers.Metadata;
using Moq;

namespace InternalsViewer.Internals.Tests.UnitTests.Providers.Metadata;

[Trait("Category", "Unit")]
[Trait("Area", "Providers")]
public class AllocationUnitProviderTests
{
    [Fact]
    public void A_Plan_Name_Finds_The_In_Row_Data_Unit_Ignoring_Brackets_And_Case()
    {
        var database = CreateDatabase(Unit(1, "dbo", "HeapTable", string.Empty, AllocationUnitType.LargeObjectData),
                                      Unit(2, "dbo", "HeapTable", string.Empty, AllocationUnitType.InRowData),
                                      Unit(3, "dbo", "Other", string.Empty, AllocationUnitType.InRowData));

        Assert.Equal(2, AllocationUnitProvider.FindAllocationUnit(database, "[dbo]", "[heaptable]", null)?.AllocationUnitId);
    }

    [Fact]
    public void An_Index_Name_Picks_That_Index()
    {
        var database = CreateDatabase(Unit(1, "dbo", "Orders", "PK_Orders", AllocationUnitType.InRowData),
                                      Unit(2, "dbo", "Orders", "IX_Orders_Date", AllocationUnitType.InRowData));

        Assert.Equal(2, AllocationUnitProvider.FindAllocationUnit(database, "dbo", "Orders", "[IX_Orders_Date]")?.AllocationUnitId);
    }

    [Fact]
    public void An_Empty_Schema_Matches_Any_Schema()
    {
        var database = CreateDatabase(Unit(1, "sales", "Orders", string.Empty, AllocationUnitType.InRowData));

        Assert.Equal(1, AllocationUnitProvider.FindAllocationUnit(database, null, "Orders", null)?.AllocationUnitId);
    }

    [Fact]
    public void Every_Unit_Of_A_Table_Is_Listed()
    {
        var database = CreateDatabase(Unit(1, "dbo", "HeapTable", string.Empty, AllocationUnitType.InRowData),
                                      Unit(2, "dbo", "HeapTable", string.Empty, AllocationUnitType.LargeObjectData),
                                      Unit(3, "dbo", "Other", string.Empty, AllocationUnitType.InRowData));

        var units = AllocationUnitProvider.GetAllocationUnits(database, "dbo", "HeapTable", null);

        Assert.Equal([1L, 2L], units.Select(a => a.AllocationUnitId));
    }

    [Fact]
    public void No_Table_Finds_Nothing()
    {
        var database = CreateDatabase(Unit(1, "dbo", "HeapTable", string.Empty, AllocationUnitType.InRowData));

        Assert.Null(AllocationUnitProvider.FindAllocationUnit(database, "dbo", null, null));
    }

    private static AllocationUnit Unit(long id, string schema, string table, string index, AllocationUnitType type)
        => new() { AllocationUnitId = id, SchemaName = schema, TableName = table, IndexName = index, AllocationUnitType = type };

    private static DatabaseSource CreateDatabase(params AllocationUnit[] units)
        => new(new Mock<IConnectionType>().Object) { AllocationUnits = units.ToDictionary(u => u.AllocationUnitId) };
}
