using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Pages;

namespace InternalsViewer.Internals.TransactionLog
{
    /// <summary>
    /// Log Data containing fragment of a page at a particular offset
    /// </summary>
    public class LogData
    {
        public UInt16 Offset { get; set; }

        public UInt16 Slot { get; set; }
        
        public byte[] Data { get; set; }
        
        public LogSequenceNumber LogSequenceNumber { get; set; }

        public override string ToString()
        {
            return string.Format("LSN: {0} Slot: {1} Offset: {2} Data: {3}",
                                 this.LogSequenceNumber,
                                 this.Slot,
                                 this.Offset,
                                 DataConverter.ToHexString(this.Data));
        }

        /// <summary>
        /// Merges the data into a page
        /// </summary>
        /// <param name="page">The target page.</param>
        public Page MergeData(Page page)
        {
            int dataOffset = page.OffsetTable[this.Slot] + this.Offset;

            byte[] pageData = new byte[Page.Size];

            Array.Copy(page.PageData, pageData, Page.Size);

            Array.Copy(this.Data, 0, pageData, dataOffset, this.Data.Length);

            page.PageData = pageData;

            return page;
        }

    }
}
