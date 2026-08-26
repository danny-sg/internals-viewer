namespace InternalsViewer.Query.Events.BatchMode;

public sealed record BatchModeEvent : EngineEvent
{
    public int NodeId { get; init; }

    public bool? IsFastComparisonUsed { get; init; }

    public bool? IsLocalAggregationUsed { get; init; }

    public bool? IsPrefiltered { get; init; }

    public bool? IsGlobalDictionaryUsed { get; init; }

    public string? GlobalDictionaryKeyColumns { get; init; }
}
