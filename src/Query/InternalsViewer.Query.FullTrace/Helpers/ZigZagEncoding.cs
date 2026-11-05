namespace InternalsViewer.Query.FullTrace.Helpers;

internal static class ZigZagEncoding
{
    public static ulong Encode(long value) => (ulong)((value << 1) ^ (value >> 63));

    public static long Decode(ulong value) => (long)(value >> 1) ^ -(long)(value & 1);
}
