using InternalsViewer.Connection.BackupFile.Reader;

namespace InternalsViewer.Connection.BackupFile.Format.Blocks.Descriptors;

internal sealed class EndOfPadSetDescriptorBlock : DescriptorBlock
{
    public EndOfPadSetDescriptorBlock(BackupReader reader): base(reader)
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