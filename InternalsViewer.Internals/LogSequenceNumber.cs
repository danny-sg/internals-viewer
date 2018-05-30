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
            var sb = new StringBuilder(value);
            sb.Replace("(", string.Empty);
            sb.Replace(")", string.Empty);

            var splitAddress = sb.ToString().Split(@":".ToCharArray());

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

        public decimal ToDecimal()
        {
            return decimal.Parse($"{virtualLogFile}{fileOffset:0000000000}{recordSequence:00000}");
        }

        public decimal ToDecimalFileOffsetOnly()
        {
            return decimal.Parse($"{virtualLogFile}{fileOffset:0000000000}");
        }

        int IComparable<LogSequenceNumber>.CompareTo(LogSequenceNumber other)
        {
            return fileOffset.CompareTo(other.virtualLogFile)
                   + recordSequence.CompareTo(other.fileOffset)
                   + recordSequence.CompareTo(other.recordSequence);

        }
    }
}
