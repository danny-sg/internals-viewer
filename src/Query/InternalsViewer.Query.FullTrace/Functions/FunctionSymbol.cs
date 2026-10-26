using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Functions;

/// <summary>
/// Function symbol passed to visitors during symbol enumeration
/// </summary>
/// <remarks>
/// Instances are only valid for the duration of the visit callback. The name, signature and decorated name spans point into the
/// enumerator's buffers and are overwritten when the enumeration moves to the next symbol, which is why this is a ref struct.
///
/// Name/Signature/DecoratedName use ReadOnlySpan against the enumerator's buffers to prevent string allocations until the point the
/// signature is parsed, with the result cached so each symbol is parsed at most once.
/// 
/// Multiple symbols can share the same RVA (for example identical functions folded together by the linker). The duplicate flags describe
/// the symbol relative to others already seen at the same address:
///
///     - IsFirst           - first symbol seen at the address
///     - HasSameName       - same name as the first symbol at the address
///     - HasSameParameters - same parameter list as the first symbol at the address
///
/// Parsed and Layout parse the signature on demand. When a <see cref="SignatureCache"/> is supplied the parse result is reused
/// across accesses for the current symbol. The cache is reset by the enumerator before each visit.
/// </remarks>
internal readonly ref struct FunctionSymbol
{
    public required uint Rva { get; init; }

    public required ReadOnlySpan<char> Name { get; init; }

    public required ReadOnlySpan<char> Signature { get; init; }

    public required ReadOnlySpan<char> DecoratedName { get; init; }

    public bool IsFirst { get; init; }

    public bool HasSameName { get; init; }

    public bool HasSameParameters { get; init; }

    public SignatureCache? Cache { get; init; }

    public FunctionSignature? Parsed
        => Cache is { } cache ? cache.Get(Signature, DecoratedName) : FunctionSignature.Parse(Signature, DecoratedName);

    public ArgumentLayout? Layout
        => Cache is { } cache ? cache.GetLayout(Signature, DecoratedName) : Parsed is { } parsed ? ArgumentLayout.GetLayout(parsed) : null;

    internal sealed class SignatureCache
    {
        private bool IsParsed { get; set; }

        private FunctionSignature? Signature { get; set; }

        private ArgumentLayout? Layout { get; set; }

        public void Reset()
        {
            IsParsed = false;

            Signature = null;

            Layout = null;
        }

        public FunctionSignature? Get(ReadOnlySpan<char> signature, ReadOnlySpan<char> decoratedName)
        {
            if (!IsParsed)
            {
                Signature = FunctionSignature.Parse(signature, decoratedName);

                IsParsed = true;
            }

            return Signature;
        }

        public ArgumentLayout? GetLayout(ReadOnlySpan<char> signature, ReadOnlySpan<char> decoratedName)
            => Layout ??= Get(signature, decoratedName) is { } parsed ? ArgumentLayout.GetLayout(parsed) : null;
    }
}
