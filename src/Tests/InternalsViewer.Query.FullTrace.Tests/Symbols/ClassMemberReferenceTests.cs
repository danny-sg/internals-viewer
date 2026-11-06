using InternalsViewer.Query.FullTrace.Symbols;

namespace InternalsViewer.Query.FullTrace.Tests.Symbols;

[Trait("Category", "Unit")]
public class ClassMemberReferenceTests
{
    [Fact]
    public void A_Member_Is_Qualified_With_Its_Module_And_Class()
    {
        var member = new ClassMemberReference(new ClassMember("sqlmin", "AcquireHoBtRowGroupLock", "(unsigned long)", 0x1000, true),
                                              "HoBtAccess",
                                              false);

        Assert.Equal("sqlmin!HoBtAccess::AcquireHoBtRowGroupLock", member.QualifiedName);
    }

    [Fact]
    public void A_Member_Without_A_Class_Is_Qualified_With_Its_Module()
    {
        var member = new ClassMemberReference(new ClassMember("sqlmin", "memcpy", "(void *,void *,size_t)", 0x1000, true), "", false);

        Assert.Equal("sqlmin!memcpy", member.QualifiedName);
    }
}
