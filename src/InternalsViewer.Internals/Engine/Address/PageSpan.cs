namespace InternalsViewer.Internals.Engine.Address;

public record struct PageSpan
{
    public PageSpan(PageAddress address, long sequenceId)
        : this(address, sequenceId, sequenceId)
    {
    }

    public PageSpan(PageAddress address, long sequenceFrom, long sequenceTo)
    {
        Address = address;
        SequenceFrom = sequenceFrom;
        SequenceTo = sequenceTo;
    }

    public PageSpan()
    {

    }

    public PageAddress Address { get; set; }

    public long SequenceFrom { get; set; }
    
    public long SequenceTo { get; set; }
}