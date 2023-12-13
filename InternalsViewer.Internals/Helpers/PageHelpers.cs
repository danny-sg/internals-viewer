using InternalsViewer.Internals.Engine.Pages;

namespace InternalsViewer.Internals.Helpers;

public class PageHelpers
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
            _ => string.Empty
        };
    }
}