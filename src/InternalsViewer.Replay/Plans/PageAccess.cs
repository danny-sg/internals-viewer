using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Replay.Plans;

public class PageAccess
{
    public PageAddress Page { get; set; }

    public long Sequence { get; set; }
    public long TimeUs { get; set; }

    public PageAccessType Type { get; set; }
}