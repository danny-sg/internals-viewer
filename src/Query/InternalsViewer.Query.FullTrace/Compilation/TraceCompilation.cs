namespace InternalsViewer.Query.FullTrace.Compilation;

public sealed record TraceCompilation(TraceCompileText? Text, List<TraceToken> Tokens, List<TraceCompileStep> Steps)
{
    public static TraceCompilation Empty { get; } = new(null, [], []);

    public bool IsEmpty => Text is null && Tokens.Count == 0 && Steps.Count == 0;
}
