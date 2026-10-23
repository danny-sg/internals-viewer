using InternalsViewer.Query.CallStack.Arguments;
using InternalsViewer.Query.FullTrace.Arguments;

namespace InternalsViewer.Query.FullTrace.Functions;

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
