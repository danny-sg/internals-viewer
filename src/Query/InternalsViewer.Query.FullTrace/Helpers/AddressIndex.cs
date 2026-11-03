using InternalsViewer.Query.Helpers;

namespace InternalsViewer.Query.FullTrace.Helpers;

internal sealed class AddressIndex(ulong[] starts, ulong largest)
{
    public ulong[] Starts { get; } = starts;

    public ulong Largest { get; } = largest;

    public CandidateEnumerator GetCandidates(ulong address, bool isStartIncluded = true)
        => new(Starts,
               Largest,
               address,
               isStartIncluded ? SortedSearch.FirstAfter(Starts, address) : SortedSearch.FirstAtOrAfter(Starts, address));

    public struct CandidateEnumerator(ulong[] starts, ulong largest, ulong address, int next)
    {
        public int Current { get; private set; } = next;

        public readonly CandidateEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            Current--;

            return Current >= 0 && address - starts[Current] < largest;
        }
    }
}
