using InternalsViewer.Query.FullTrace.Memory;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public sealed class CallSignaturePart(string text,
                                     CallSignaturePartKind kind,
                                     ulong? value = null,
                                     string? toolTip = null,
                                     bool canFind = false,
                                     string? decoded = null,
                                     string? decodedClass = null,
                                     StringValue? decodedString = null,
                                     string? typeName = null,
                                     string? label = null,
                                     string? declaredType = null,
                                     MemoryMapEntry? origin = null,
                                     string? location = null)
{
    public string Text { get; } = text;

    public CallSignaturePartKind Kind { get; } = kind;

    public ulong? Value { get; } = value;

    public string? ToolTip { get; } = toolTip;

    public bool CanFind { get; } = canFind;

    public string? Decoded { get; } = decoded;

    public string? DecodedClass { get; } = decodedClass;

    public StringValue? DecodedString { get; } = decodedString;

    public string? TypeName { get; } = typeName;

    public string? Label { get; } = label;

    public string? DeclaredType { get; } = declaredType;

    public MemoryMapEntry? Origin { get; } = origin;

    public string? Location { get; } = location;

    public bool IsDecodedFor(bool decode) => decode && Decoded is not null;

    public bool ShowsTypeFor(bool decode) => TypeName is not null && (!IsDecodedFor(decode) || DecodedClass is null);

    public string GetDisplayText(bool decode) => ShowsTypeFor(decode) ? $"{TypeName} {GetText(decode)}" : GetText(decode);

    public string GetText(bool decode)
    {
        if (!IsDecodedFor(decode))
        {
            return Text;
        }

        return DecodedClass is null ? Decoded! : $"{DecodedClass} {Decoded}";
    }
}
