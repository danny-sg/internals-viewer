using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Markers;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.RecordLoaders;
using InternalsViewer.Internals.Structures;

namespace InternalsViewer.Internals.Records
{
    public class DataRecord : Record, IMarkerProvider
    {
        private SparseVector sparseVector;

        public DataRecord(Page page, UInt16 slotOffset, Structure structure)
            : base(page, slotOffset, structure)
        {
            DataRecordLoader.Load(this);
        }

        internal static string GetStatusBitsDescription(DataRecord dataRecord)
        {
            string statusDescription = string.Empty;

            if (dataRecord.HasVariableLengthColumns)
            {
                statusDescription += ", Variable Length Flag";
            }

            if (dataRecord.HasNullBitmap && dataRecord.HasVariableLengthColumns)
            {
                statusDescription += " | NULL Bitmap Flag";
            }
            else if (dataRecord.HasNullBitmap)
            {
                statusDescription += ", NULL Bitmap Flag";
            }

            return statusDescription;
        }

        public List<Marker> ProvideMarkers()
        {
            throw new NotImplementedException();
        }

        public SparseVector SparseVector
        {
            get { return sparseVector; }
            set { sparseVector = value; }
        }
    }
}
