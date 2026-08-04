namespace InternalsViewer.Query.Plans.Model;

public sealed record QueryMemoryGrant
{
    public long? SerialRequiredKb { get; init; }

    public long? SerialDesiredKb { get; init; }

    public long? RequiredKb { get; init; }

    public long? DesiredKb { get; init; }

    public long? RequestedKb { get; init; }

    public long? GrantedKb { get; init; }

    public long? MaxUsedKb { get; init; }

    public long? MaxQueryKb { get; init; }

    public long? GrantWaitTimeSeconds { get; init; }
}
