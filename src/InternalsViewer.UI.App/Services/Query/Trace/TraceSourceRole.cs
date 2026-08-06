namespace InternalsViewer.UI.App.Services.Query.Trace;

/// <summary>
/// The part an input plays in the operator that reads it
/// </summary>
public enum TraceSourceRole
{
    None,
    Outer,
    Inner,
    Build,
    Probe,
    Seek,
    Lookup
}
