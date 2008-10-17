using System.Collections.Generic;

namespace InternalsViewer.Internals
{
    interface IMarkerProvider
    {
        /// <summary>
        /// Provides markers for visualising a structure
        /// </summary>
        /// <returns></returns>
        List<Marker> ProvideMarkers();
    }
}
