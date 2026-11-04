using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.FullTrace.Native;

namespace InternalsViewer.Query.FullTrace.Stubs;

/// <summary>
/// Indirect stub jump targets resolver
/// </summary>
/// <remarks>
/// x64 Windows binaries often route control through stubs which are small redirects to the actual target. The stubs don't have symbols, or
/// it is misleading, so the actual target needs to be resolved from the stub to be able to resolve the symbols correctly. This has to be
/// done at runtime.
///
/// The process is:
///
/// 1. The Replay Bridge (<see cref="Native.ReplayBridge"/>) checks its local cache of confirmed non-stub addresses for each call target. If
///    the address is not confirmed it calls back to the Stub Resolver.
///
/// 2. This Stub Resolver checks the address and confirms if it is a real function or a stub. It does this by checking its cache:
/// 
///     - If the address is in the cache it returns the cached result, either with null (NotStub) or a previously decoded stub.
/// 
///     - If the address has not been seen it reads the first 64 bytes (CodeLength) of the module image at the address and checks for a stub
///       pattern with <see cref="StubDecoder"/>. Either a Stub or null is returned. Both are cached for future calls to the same address.
///
///     Confirmation of a non-stub (GetStub returns null for the address) returns NotStub to the Replay Bridge which then adds the address
///     to its local cache of non-stub addresses so it doesn't have to call back again across the interop for this address.
///
///     The decoded stub is resolved with the current register values and memory state (<see cref="Stub.TryResolve"/>) to get the target
///     address.
///
///     Return states:
/// 
///         - NotStub    - either an Exception has been previously hit, or the address is not a stub, derived from the cache or the
///                        decoder.
///         - Resolved   - the stub was decoded and resolved to a target address, which is returned in the target pointer.
///                      
///         - Unresolved - the stub could not be resolved with the current register values and memory state.
/// </remarks>
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
