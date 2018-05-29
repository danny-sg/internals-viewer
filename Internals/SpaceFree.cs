namespace InternalsViewer.Internals
{
    public enum SpaceFree : byte
    {
        Empty = 0x00,
        FiftyPercent = 0x01, // 1%-50%
        EightyPercent = 0x02, // 51%-80%
        NinetyFivePercent = 0x03, // 81%-95%
        OneHundredPercent = 0x04 // 96-100%
    }
}