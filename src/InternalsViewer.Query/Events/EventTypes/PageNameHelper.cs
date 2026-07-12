using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Internals.Helpers;

namespace InternalsViewer.Query.Events.EventTypes;

/// <summary>
/// Names for special pages that belong to no object (allocation maps, the file header, …)
/// </summary>
public static class PageNameHelper
{
    /// <summary>
    /// The display name for a special page, or null when the page is ordinary object data
    /// </summary>
    public static string? TryGetPageName(PageAddress pageAddress)
    {
        switch (pageAddress.PageId)
        {
            case 0:
                return "File Header";

            case 9:
                return "Boot page";

            default:
                if (PageHelpers.IsBcm(pageAddress.PageId))
                {
                    return "BCM";
                }

                if (PageHelpers.IsDcm(pageAddress.PageId))
                {
                    return "DCM";
                }

                if (PageHelpers.IsGam(pageAddress.PageId))
                {
                    return "GAM";
                }

                if (PageHelpers.IsSgam(pageAddress.PageId))
                {
                    return "SGAM";
                }

                if (PageHelpers.IsPfs(pageAddress.PageId))
                {
                    return "PFS";
                }

                return null;
        }
    }
}
