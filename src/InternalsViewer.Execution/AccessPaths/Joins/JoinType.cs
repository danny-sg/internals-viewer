
namespace InternalsViewer.Execution.AccessPaths.Joins;

/// <summary>
/// The logical join a physical join operator is carrying out
/// </summary>
/// <remarks>
/// Decides what happens to a row that finds no partner, which is the only point the physical algorithms differ by join type.
/// </remarks>
public enum JoinType
{
    Inner,
    LeftOuter,
    RightOuter,
    FullOuter,
    LeftSemi,
    LeftAntiSemi,
    RightSemi,
    RightAntiSemi
}