using InternalsViewer.Query.FullTrace.Functions;

namespace InternalsViewer.Query.FullTrace.PublishedEvents;

internal sealed record PublishedEventFunctions(EventPublisher[] Publishers, ulong[] Writers)
{
    private const string PackageSeparator = "Pkg::";

    private const string PublishMethod = "Publish";

    private const string WriterClass = "XE_BufferMgr";

    private const string WriterMethod = "Reserve";

    public static PublishedEventFunctions Empty { get; } = new([], []);

    public static PublishedEventFunctions From(Dictionary<uint, FunctionClassification> functions)
        => new([.. functions.Where(f => f.Value.PublishEventName is not null)
                            .Select(f => new EventPublisher(f.Key, f.Value.PublishEventName!))],
               [.. functions.Where(f => f.Value.IsEventWriter).Select(f => (ulong)f.Key)]);

    public static PublishedEventFunctions Merge(IEnumerable<PublishedEventFunctions> functions)
    {
        var all = functions.ToList();

        return new([.. all.SelectMany(f => f.Publishers)], [.. all.SelectMany(f => f.Writers)]);
    }

    public static string? GetPublishEventName(ReadOnlySpan<char> className, ReadOnlySpan<char> methodName)
    {
        if (methodName is not PublishMethod)
        {
            return null;
        }

        var package = className.IndexOf(PackageSeparator, StringComparison.Ordinal);

        if (package <= 0)
        {
            return null;
        }

        var eventName = className[(package + PackageSeparator.Length)..];

        return eventName.Length > 0 && !eventName.Contains("::", StringComparison.Ordinal) ? eventName.ToString() : null;
    }

    public static bool IsEventWriter(ReadOnlySpan<char> className, ReadOnlySpan<char> methodName)
        => className is WriterClass && methodName is WriterMethod;

    public PublishedEventFunctions Rebase(ulong address)
        => new([.. Publishers.Select(p => p with { Address = p.Address + address })], [.. Writers.Select(f => f + address)]);
}
