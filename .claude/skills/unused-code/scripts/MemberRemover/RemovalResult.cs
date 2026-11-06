namespace MemberRemover;

internal sealed class RemovalResult
{
    public List<string> Removed { get; } = [];

    public List<string> Problems { get; } = [];
}
