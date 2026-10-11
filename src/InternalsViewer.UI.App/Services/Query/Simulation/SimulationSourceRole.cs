namespace InternalsViewer.UI.App.Services.Query.Simulation;

/// <summary>
/// The part an input plays in the operator that reads it
/// </summary>
public enum SimulationSourceRole
{
    None,
    Outer,
    Inner,
    Build,
    Probe,
    Seek,
    Lookup
}
