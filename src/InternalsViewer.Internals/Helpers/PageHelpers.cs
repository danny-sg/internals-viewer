using InternalsViewer.Internals.Engine.Pages.Enums;

namespace InternalsViewer.Internals.Helpers;

public static class PageHelpers
{
    /// <summary>
    /// Returns the description of a Page Type
    /// </summary>
    public static string GetPageTypeName(PageType pageType)
    {
        return pageType switch
        {
            PageType.Data => "Data",
            PageType.Index => "Index",
            PageType.Lob3 => "LOB (Text/Image)",
            PageType.Lob4 => "LOB (Text/Image)",
            PageType.Sort => "Sort",
            PageType.Gam => "GAM (Global Allocation Map)",
            PageType.Sgam => "SGAM (Shared Global Allocation Map)",
            PageType.Iam => "IAM (Index Allocation Map)",
            PageType.Pfs => "PFS (Page Free Space)",
            PageType.Dcm => "DCM (Differential Changed Map)",
            PageType.Bcm => "BCM (Bulk Changed Map)",
            PageType.Boot => "Boot Page",
            PageType.FileHeader => "File Header Page",
            PageType.None => string.Empty,
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Returns the description of a Page Type
    /// </summary>
    public static string GetPageTypeShortName(PageType pageType)
    {
        return pageType switch
        {
            PageType.Data => "Data",
            PageType.Index => "Index",
            PageType.Lob3 => "LOB",
            PageType.Lob4 => "LOB",
            PageType.Sort => "Sort",
            PageType.Gam => "GAM",
            PageType.Sgam => "SGAM",
            PageType.Iam => "IAM",
            PageType.Pfs => "PFS",
            PageType.Dcm => "DCM",
            PageType.Bcm => "BCM",
            PageType.Boot => "Boot",
            PageType.FileHeader => "FileHeader",
            PageType.None => string.Empty,
            _ => "Unknown"
        };
    }
}