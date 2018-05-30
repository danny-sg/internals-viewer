using System;

namespace InternalsViewer.Internals.Records
{
    /// <summary>
    /// SQL Server 2008 Column Descriptor (CD) structure
    /// </summary>
    public class ColumnDescriptor
    {
        /// <summary>
        /// Loads a CD array.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <param name="pageData">The page data.</param>
        /// <param name="noOfColumns">The no of columns.</param>
        /// <returns></returns>
        protected static byte[] LoadCdArray(short offset, byte[] pageData,  short noOfColumns)
        {
            var cdArray = new byte[noOfColumns];

            for (var i = 0; i < noOfColumns; i += 1)
            {
                if (i % 2 == 0)
                {
                    cdArray[i] = Convert.ToByte(pageData[offset] & 15);
                }
                else
                {
                    cdArray[i] = Convert.ToByte(pageData[offset] >> 4);

                    offset++;
                }
            }

            return cdArray;
        }

        /// <summary>
        /// Get a description of a CD Array item
        /// </summary>
        /// <param name="cdItem">The Column Descriptor item.</param>
        /// <returns></returns>
        public static string GetCdDescription(byte cdItem)
        {
            switch (cdItem)
            {
                case 0: return "(null)";
                case 10: return "Long";
                case 12: return "Short - Page Symbol - 1 byte";
                case 2: return string.Format("Short - {0} byte", cdItem - 1);
                
                default:

                    if (cdItem > 10)
                    {
                        return string.Format("Short - Page Symbol - {0} bytes", cdItem - 11);
                    }
                    else
                    {
                        return string.Format("Short - {0} bytes", cdItem - 1);
                    }
            }
        }
    }
}
