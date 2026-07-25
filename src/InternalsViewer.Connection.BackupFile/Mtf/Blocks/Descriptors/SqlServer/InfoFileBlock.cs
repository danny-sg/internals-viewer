namespace InternalsViewer.Connection.BackupFile.Mtf.Blocks.Descriptors.SqlServer;

internal sealed class InfoFileBlock : DescriptorBlock
{
    public InfoFileBlock(MtfReader reader) : base(reader)
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