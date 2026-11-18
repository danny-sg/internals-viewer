using InternalsViewer.Query.FullTrace.Symbols.Enums;

namespace InternalsViewer.Query.FullTrace.Symbols;

public sealed record PrimitiveType(string Name, int Size, PrimitiveTypeKind Kind, bool IsCharacter = false)
{
    public bool IsInteger => Kind is PrimitiveTypeKind.SignedInteger or PrimitiveTypeKind.UnsignedInteger;
}
