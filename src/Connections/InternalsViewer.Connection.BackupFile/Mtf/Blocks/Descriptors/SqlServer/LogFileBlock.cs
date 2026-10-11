namespace InternalsViewer.Connection.BackupFile.Mtf.Blocks.Descriptors.SqlServer;

internal sealed class LogFileBlock : DescriptorBlock
{
    public LogFileBlock(MtfReader reader) : base(reader)
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