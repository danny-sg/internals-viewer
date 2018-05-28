
namespace InternalsViewer.Internals
{
    using System.Collections;

    public class PfsByte
    {
        public PfsByte(byte pageByte)
        {
            var bitArray = new BitArray(new byte[] { pageByte });

            GhostRecords = bitArray[3];
            Iam = bitArray[4];
            Mixed = bitArray[5];
            Allocated = bitArray[6];

            PageSpaceFree = (SpaceFree)(pageByte & 7);
        }

        public SpaceFree PageSpaceFree { get; set; }

        public bool GhostRecords { get; set; }

        public bool Iam { get; set; }

        public bool Mixed { get; set; }

        public bool Allocated { get; set; }

        public override string ToString()
        {
            return
                string.Format(
                    "PFS Status: {0:Allocated ; ;Not Allocated }| {1} Full{2: | IAM Page ; ; }{3:| Mixed Extent ; ; }{4:| Has Ghost ; ; }",
                    Allocated ? 1 : 0,
                    SpaceFreeDescription(PageSpaceFree),
                    Iam ? 1 : 0,
                    Mixed ? 1 : 0,
                    GhostRecords ? 1 : 0);
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