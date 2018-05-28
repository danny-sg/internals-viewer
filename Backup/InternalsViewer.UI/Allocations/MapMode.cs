
namespace InternalsViewer.UI.Allocations
{
    /// <summary>
    /// Modes for the allocation map
    /// </summary>
    public enum MapMode
    {
        /// <summary>
        /// Standard Map Mode
        /// </summary>
        Standard,

        /// <summary>
        /// PFS Mode - SQL Internals Viewer Only
        /// </summary>
        Pfs,
        
        /// <summary>
        /// Can't remember why this one's here
        /// </summary>
        Map,
        
        /// <summary>
        /// Range mode - not used
        /// </summary>
        RangeSelection,
        Full
    }
}
