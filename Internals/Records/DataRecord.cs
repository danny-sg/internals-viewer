using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Pages;
using InternalsViewer.Internals.RecordLoaders;

namespace InternalsViewer.Internals.Records
{
    class DataRecord : Record, IMarkerProvider
    {
        private SparseVector sparseVector;

        public DataRecord(Page page, Int16 slotOffset)
            : base(page, slotOffset)
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
