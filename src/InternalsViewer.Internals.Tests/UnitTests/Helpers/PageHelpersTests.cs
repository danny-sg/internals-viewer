using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Engine.Pages.Enums;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.Internals.Tests.UnitTests.Helpers;

public class PageHelpersTests
{
    [Theory]
    [InlineData(PageType.Data, "Data")]
    [InlineData(PageType.Index, "Index")]
    [InlineData(PageType.Gam, "GAM (Global Allocation Map)")]
    [InlineData(PageType.Sgam, "SGAM (Shared Global Allocation Map)")]
    [InlineData(PageType.Iam, "IAM (Index Allocation Map)")]
    [InlineData(PageType.Pfs, "PFS (Page Free Space)")]
    [InlineData(PageType.Dcm, "DCM (Differential Changed Map)")]
    [InlineData(PageType.Bcm, "BCM (Bulk Changed Map)")]
    [InlineData(PageType.Boot, "Boot Page")]
    [InlineData(PageType.FileHeader, "File Header Page")]
    [InlineData(PageType.None, "")]
    public void GetPageTypeName_Returns_Full_Description(PageType pageType, string expected)
    {
        Assert.Equal(expected, PageHelpers.GetPageTypeName(pageType));
    }

    [Theory]
    [InlineData(PageType.Data, "Data")]
    [InlineData(PageType.Index, "Index")]
    [InlineData(PageType.Gam, "GAM")]
    [InlineData(PageType.Sgam, "SGAM")]
    [InlineData(PageType.Iam, "IAM")]
    [InlineData(PageType.Pfs, "PFS")]
    [InlineData(PageType.Dcm, "DCM")]
    [InlineData(PageType.Bcm, "BCM")]
    [InlineData(PageType.Boot, "Boot")]
    [InlineData(PageType.FileHeader, "FileHeader")]
    [InlineData(PageType.None, "")]
    public void GetPageTypeShortName_Returns_Short_Name(PageType pageType, string expected)
    {
        Assert.Equal(expected, PageHelpers.GetPageTypeShortName(pageType));
    }

    // GAM pages: 2, 2 + 63904, 2 + 2*63904, ...
    [Theory]
    [InlineData(AllocationPage.FirstGamPage, true)]
    [InlineData(AllocationPage.FirstGamPage + AllocationPage.AllocationExtentInterval, true)]
    [InlineData(AllocationPage.FirstGamPage + 1, false)]
    [InlineData(0, false)]
    [InlineData(AllocationPage.FirstSgamPage, false)]
    public void IsGam_Returns_Correct_For_Boundary_Pages(int pageId, bool expected)
    {
        Assert.Equal(expected, PageHelpers.IsGam(pageId));
    }

    [Theory]
    [InlineData(AllocationPage.FirstSgamPage, true)]
    [InlineData(AllocationPage.FirstSgamPage + AllocationPage.AllocationExtentInterval, true)]
    [InlineData(AllocationPage.FirstSgamPage + 1, false)]
    [InlineData(AllocationPage.FirstGamPage, false)]
    public void IsSgam_Returns_Correct_For_Boundary_Pages(int pageId, bool expected)
    {
        Assert.Equal(expected, PageHelpers.IsSgam(pageId));
    }

    [Theory]
    [InlineData(AllocationPage.FirstDcmPage, true)]
    [InlineData(AllocationPage.FirstDcmPage + AllocationPage.AllocationExtentInterval, true)]
    [InlineData(AllocationPage.FirstDcmPage + 1, false)]
    public void IsDcm_Returns_Correct_For_Boundary_Pages(int pageId, bool expected)
    {
        Assert.Equal(expected, PageHelpers.IsDcm(pageId));
    }

    [Theory]
    [InlineData(AllocationPage.FirstBcmPage, true)]
    [InlineData(AllocationPage.FirstBcmPage + AllocationPage.AllocationExtentInterval, true)]
    [InlineData(AllocationPage.FirstBcmPage + 1, false)]
    public void IsBcm_Returns_Correct_For_Boundary_Pages(int pageId, bool expected)
    {
        Assert.Equal(expected, PageHelpers.IsBcm(pageId));
    }

    [Theory]
    [InlineData(1, true)]                              // First PFS page
    [InlineData(PfsPage.PfsInterval + 1, true)]        // Second PFS page (8089)
    [InlineData(PfsPage.PfsInterval * 2 + 1, true)]    // Third PFS page
    [InlineData(2, false)]
    [InlineData(PfsPage.PfsInterval, false)]
    [InlineData(PfsPage.PfsInterval + 2, false)]
    public void IsPfs_Returns_Correct_For_Boundary_Pages(int pageId, bool expected)
    {
        Assert.Equal(expected, PageHelpers.IsPfs(pageId));
    }
}
