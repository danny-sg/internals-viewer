using InternalsViewer.Query.CallStack.Arguments;

namespace InternalsViewer.Query.FullTrace.Functions;

internal sealed class FunctionSymbol
{
    public required uint Rva { get; init; }

    public required string Name { get; init; }

    public required string Signature { get; init; }

    public required string DecoratedName { get; init; }

    public bool IsFirst { get; init; }

    public bool HasSameName { get; init; }

    public bool HasSameParameters { get; init; }

    public FunctionSignature? Parsed
    {
        get
        {
            if (!IsParsed)
            {
                ParsedSignature = FunctionSignature.Parse(Signature, DecoratedName);

                IsParsed = true;
            }

            return ParsedSignature;
        }
    }

    private bool IsParsed { get; set; }

    private FunctionSignature? ParsedSignature { get; set; }
}
