namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class CallSignaturePart(string text,
                                     CallSignaturePartKind kind,
                                     ulong? value = null,
                                     string? toolTip = null,
                                     bool canFind = false,
                                     string? decoded = null,
                                     string? decodedClass = null)
{
    public string Text { get; } = text;

    public CallSignaturePartKind Kind { get; } = kind;

    public ulong? Value { get; } = value;

    public string? ToolTip { get; } = toolTip;

    public bool CanFind { get; } = canFind;

    public string? Decoded { get; } = decoded;

    public string? DecodedClass { get; } = decodedClass;

    public bool IsDecodedFor(bool decode) => decode && Decoded is not null;

    public string GetText(bool decode)
    {
        if (!IsDecodedFor(decode))
        {
            return Text;
        }

        return DecodedClass is null ? Decoded! : $"{DecodedClass} {Decoded}";
    }
}
