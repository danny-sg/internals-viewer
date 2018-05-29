using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Records;

namespace InternalsViewer.Internals.Compression
{
    public class CdArrayItem : Markable
    {
        public CdArrayItem(int index, byte value)
        {
            Index = index;
            Value = value;
        }

        private static string GetCdDescription(byte cdItem)
        {
            switch (cdItem)
            {
                case 0: return "(null)";
                case 10: return "Long";
                case 12: return "Short - Page Symbol - 1 byte";
                case 2: return $"Short - {cdItem - 1} byte";

                default:

                    if (cdItem > 10)
                    {
                        return $"Short - Page Symbol - {cdItem - 11} bytes";
                    }
                    else
                    {
                        return $"Short - {cdItem - 1} bytes";
                    }

            }
        }

        public int Index { get; set; }

        public byte Value { get; set; }

        [MarkAttribute("", "White", "Orange", true)]
        public string Description
        {
            get
            {
                return string.Format("Column {0}: {1}, Column {2}: {3}",
                                     (Index * 2),
                                     GetCdDescription(Values[0]),
                                     (Index * 2) + 1,
                                     GetCdDescription(Values[1]));
            }
        }

        public byte[] Values
        {
            get
            {
                return new byte[] { (byte)(Value & 15), (byte)(Value >> 4) };
            }
        }
    }
}
