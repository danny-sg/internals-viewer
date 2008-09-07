
namespace InternalsViewer.Internals
{
    using System.Collections;

    public class PfsByte
    {
        private bool allocated;

        private bool ghostRecords;
        private bool iam;
        private bool mixed;
        private SpaceFree pageSpaceFree;

        public PfsByte(byte pageByte)
        {
            BitArray bitArray = new BitArray(new byte[] { pageByte });

            ghostRecords = bitArray[3];
            iam = bitArray[4];
            mixed = bitArray[5];
            allocated = bitArray[6];

            pageSpaceFree = (SpaceFree)(pageByte & 7);
        }

        public SpaceFree PageSpaceFree
        {
            get { return pageSpaceFree; }
            set { pageSpaceFree = value; }
        }

        public bool GhostRecords
        {
            get { return ghostRecords; }
            set { ghostRecords = value; }
        }

        public bool Iam
        {
            get { return iam; }
            set { iam = value; }
        }

        public bool Mixed
        {
            get { return mixed; }
            set { mixed = value; }
        }

        public bool Allocated
        {
            get { return allocated; }
            set { allocated = value; }
        }

        public override string ToString()
        {
            return
                string.Format(
                    "PFS Status: {0:Allocated ; ;Not Allocated }| {1} Full{2: | IAM Page ; ; }{3:| Mixed Extent ; ; }{4:| Has Ghost ; ; }",
                    allocated ? 1 : 0,
                    SpaceFreeDescription(pageSpaceFree),
                    iam ? 1 : 0,
                    mixed ? 1 : 0,
                    ghostRecords ? 1 : 0);
        }

        public static string SpaceFreeDescription(SpaceFree spaceFree)
        {
            switch (spaceFree)
            {
                case SpaceFree.Empty:
                    return "0%";
                case SpaceFree.FiftyPercent:
                    return "50%";
                case SpaceFree.EightyPercent:
                    return "80%";
                case SpaceFree.NinetyFivePercent:
                    return "95%";
                case SpaceFree.OneHundredPercent:
                    return "100%";
                default:
                    return "Unknown";
            }
        }

        #region SpaceFree enum

        public enum SpaceFree : byte
        {
            Empty = 0x00,
            FiftyPercent = 0x01, // 1%-50%
            EightyPercent = 0x02, // 51%-80%
            NinetyFivePercent = 0x03, // 81%-95%
            OneHundredPercent = 0x04 // 96-100%
        }

        #endregion
    }
}