using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Locators;

namespace InternalsViewer.Query.FullTrace.Stubs;

internal sealed class StubResolver(ImageText images, IReadOnlyList<TraceModule> modules)
{
    private const int NotStub = 0;

    private const int Resolved = 1;

    private const int Unresolved = 2;

    private const int CodeLength = 64;

    private const int RegisterCount = 16;

    public Exception? Failure { get; private set; }

    private Dictionary<ulong, Stub?> Stubs { get; } = [];

    private ReplayView Replay { get; } = new();

    private StubMemory Memory { get; } = new([.. modules.Select(m => (m.StartAddress, m.StartAddress + m.Size))]);

    public unsafe int Resolve(IntPtr view, ulong address, IntPtr registers, IntPtr target)
    {
        if (Failure is not null)
        {
            return NotStub;
        }

        try
        {
            if (GetStub(address) is not { } stub)
            {
                return NotStub;
            }

            Replay.View = view;

            if (!stub.TryResolve(new Span<ulong>((void*)registers, RegisterCount), Replay, Memory, out var resolved))
            {
                return Unresolved;
            }

            *(ulong*)target = resolved;

            return Resolved;
        }
        catch (Exception exception)
        {
            Failure = exception;

            return NotStub;
        }
        finally
        {
            Replay.View = IntPtr.Zero;
        }
    }

    private Stub? GetStub(ulong address)
    {
        if (Stubs.TryGetValue(address, out var stub))
        {
            return stub;
        }

        Span<byte> code = stackalloc byte[CodeLength];

        var read = images.Read(address, code);

        stub = StubDecoder.Decode(code[..read], address);

        Stubs[address] = stub;

        return stub;
    }
}
