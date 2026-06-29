namespace InternalsViewer.Query.Plans;

/// <summary>How an operator consumes one of its inputs.</summary>
public enum InputRole
{
    /// <summary>Rows flow through as they are read (e.g. a join probe, a filter's input).</summary>
    Streaming,

    /// <summary>The whole input is consumed before any output is produced (e.g. a hash build, a sort).</summary>
    Blocking
}
