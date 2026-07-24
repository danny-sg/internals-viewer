using InternalsViewer.Connection.BackupFile.Reader;

namespace InternalsViewer.Connection.BackupFile.Format.Blocks.Descriptors.SqlServer;

internal sealed class InfoFileBlock : DescriptorBlock
{
    public InfoFileBlock(BackupReader reader) : base(reader)
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