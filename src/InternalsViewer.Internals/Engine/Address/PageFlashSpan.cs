using System.Drawing;

namespace InternalsViewer.Internals.Engine.Address;

/// <summary>
/// A page highlight that's only active while the current playhead time falls within
/// [<see cref="StartUs"/>, <see cref="EndUs"/>] - unlike <see cref="PageSpan"/> (which, once in scope,
/// stays shown), a flash span disappears again once the playhead moves past it. Used for latches (active
/// for the hold duration) and, later, locks (active from acquire to release).
/// </summary>
public sealed record PageFlashSpan
{
    public PageFlashSpan(PageAddress address, long startUs, long endUs)
    {
        Address = address;
        StartUs = startUs;
        EndUs = endUs;
    }

    public Color? DisplayColour { get; set; }

    public PageAddress Address { get; init; }

    public long StartUs { get; init; }

    public long EndUs { get; init; }
}
