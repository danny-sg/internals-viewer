using System.Buffers;

namespace InternalsViewer.Query.CallStack.Dia;

internal sealed class SymbolReader(IntPtr enumerator, bool includeSignature) : IDisposable
{
    private IntPtr Enumerator { get; set; } = enumerator;

    private char[]? NameBuffer { get; set; } = ArrayPool<char>.Shared.Rent(DiaResolver.MaximumNameLength);

    private char[]? SignatureBuffer { get; set; } = includeSignature ? ArrayPool<char>.Shared.Rent(DiaResolver.MaximumNameLength) : null;

    private int NameLength { get; set; }

    private int SignatureLength { get; set; }

    public uint Rva { get; private set; }

    public bool IsFunction { get; private set; }

    public ReadOnlySpan<char> Name => NameBuffer.AsSpan(0, NameLength);

    public ReadOnlySpan<char> Signature => SignatureBuffer.AsSpan(0, SignatureLength);

    public unsafe bool MoveNext()
    {
        if (Enumerator == IntPtr.Zero || NameBuffer is null)
        {
            return false;
        }

        fixed (char* name = NameBuffer)
        fixed (char* signature = SignatureBuffer)
        {
            if (!DiaBridge.NextSymbolDetail(Enumerator,
                                            name,
                                            NameBuffer.Length,
                                            signature,
                                            SignatureBuffer?.Length ?? 0,
                                            out var rva,
                                            out var isFunction))
            {
                return false;
            }

            Rva = rva;

            IsFunction = isFunction != 0;
        }

        NameLength = GetLength(NameBuffer);

        SignatureLength = SignatureBuffer is null ? 0 : GetLength(SignatureBuffer);

        return true;
    }

    public void Dispose()
    {
        if (Enumerator != IntPtr.Zero)
        {
            DiaBridge.EndEnumSymbols(Enumerator);

            Enumerator = IntPtr.Zero;
        }

        if (NameBuffer is not null)
        {
            ArrayPool<char>.Shared.Return(NameBuffer);

            NameBuffer = null;
        }

        if (SignatureBuffer is not null)
        {
            ArrayPool<char>.Shared.Return(SignatureBuffer);

            SignatureBuffer = null;
        }

        NameLength = 0;

        SignatureLength = 0;
    }

    private static int GetLength(char[] buffer)
    {
        var length = Array.IndexOf(buffer, '\0');

        return length < 0 ? buffer.Length : length;
    }
}
