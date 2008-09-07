using System;
using System.Text;

namespace InternalsViewer.Internals
{
    public struct LogSequenceNumber : IComparable<LogSequenceNumber>
    {
        private readonly int fileOffset;
        private readonly int recordSequence;
        private readonly int virtualLogFile;

        public LogSequenceNumber(byte[] value)
        {
            virtualLogFile = BitConverter.ToInt32(value, 0);
            fileOffset = BitConverter.ToInt32(value, 4);
            recordSequence = BitConverter.ToInt16(value, 8);
        }

        public LogSequenceNumber(string value)
        {
            StringBuilder sb = new StringBuilder(value);
            sb.Replace("(", string.Empty);
            sb.Replace(")", string.Empty);

            string[] splitAddress = sb.ToString().Split(@":".ToCharArray());

            if (splitAddress.Length != 3)
            {
                throw new ArgumentException("Invalid format");
            }

            virtualLogFile = int.Parse(splitAddress[0]);
            fileOffset = int.Parse(splitAddress[1]);
            recordSequence = int.Parse(splitAddress[2]);
        }

        public override string ToString()
        {
            return string.Format("({0}:{1}:{2})", virtualLogFile, fileOffset, recordSequence);
        }

        public string ToBinaryString()
        {
            return string.Format("{0:X8}:{1:X8}:{2:X4}", virtualLogFile, fileOffset, recordSequence);
        }

        public Decimal ToDecimal()
        {
            return Decimal.Parse(string.Format("{0}{1:0000000000}{2:00000}",
                                               virtualLogFile,
                                               fileOffset,
                                               recordSequence));
        }

        public decimal ToDecimalFileOffsetOnly()
        {
            return Decimal.Parse(string.Format("{0}{1:0000000000}", virtualLogFile, fileOffset));
        }

        int IComparable<LogSequenceNumber>.CompareTo(LogSequenceNumber other)
        {
            return this.fileOffset.CompareTo(other.virtualLogFile)
                   + this.recordSequence.CompareTo(other.fileOffset)
                   + this.recordSequence.CompareTo(other.recordSequence);

        }
    }
}
