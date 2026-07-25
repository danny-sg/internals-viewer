namespace InternalsViewer.Connection.BackupFile.Mtf.Blocks.Descriptors;

internal sealed class EndOfPadSetDescriptorBlock : DescriptorBlock
{
    public EndOfPadSetDescriptorBlock(MtfReader reader): base(reader)
    {
        ReadStreams(reader);
    }

    public override string ToString()
    {
        return ToString(string.Empty);
    }

    public string ToString(string prefix)
    {
        return CommonHeaderToString(prefix);
    }
}