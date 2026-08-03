using System.Numerics;
using InternalsViewer.Execution.AccessPaths.Search;

namespace InternalsViewer.Execution.AccessPaths.Joins;

/// <summary>
/// Hashes a join key onto a hash table bucket
/// </summary>
public static class JoinHash
{
    public const int DefaultBucketBits = 4;

    public const int MinBucketBits = 2;

    public const int MaxBucketBits = 9;

    private const uint Multiplier = 0x9AD0AC2F;

    private const uint NullValue = 0xB5297A4D;

    /// <summary>
    /// Chooses a bucket count for a build side expected to hold a given number of rows
    /// </summary>
    /// <remarks>
    /// One row per bucket is the target, so the count is the row count rounded up to a power of two. The clamp keeps the table legible at
    /// both ends rather than tracking the row count all the way up, since every bucket is drawn.
    /// </remarks>
    public static int BucketBitsFor(long rows)
    {
        if (rows <= 0)
        {
            return DefaultBucketBits;
        }

        var bits = 64 - BitOperations.LeadingZeroCount((ulong)(rows - 1));

        return Math.Clamp(bits, MinBucketBits, MaxBucketBits);
    }

    /// <summary>
    /// Hashes the leading columns of a key, including any that are NULL
    /// </summary>
    /// <remarks>
    /// A NULL contributes a fixed value rather than being rejected, so a build row carrying one still lands in a bucket. It can never match
    /// anything, but an outer join has to be able to find it again once the probe is done.
    /// </remarks>
    public static uint Compute(in AccessKey key, int width)
    {
        var accumulator = 0u;

        for (var index = 0; index < width; index++)
        {
            var value = key[index].IsNull ? NullValue : unchecked((uint)key[index].GetHashCode());

            accumulator = Mix(accumulator ^ value);
        }

        return accumulator;
    }

    public static bool HasNull(in AccessKey key, int width)
    {
        for (var index = 0; index < width; index++)
        {
            if (key[index].IsNull)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Hashes the leading columns of a key, reporting failure when any of them is NULL
    /// </summary>
    public static bool TryCompute(in AccessKey key, int width, out uint hash)
    {
        hash = 0;

        var accumulator = 0u;

        for (var index = 0; index < width; index++)
        {
            if (key[index].IsNull)
            {
                return false;
            }

            accumulator = Mix(accumulator ^ unchecked((uint)key[index].GetHashCode()));
        }

        hash = accumulator;

        return true;
    }

    public static int GetBucket(uint hash, int bucketBits) => (int)(hash >> (32 - bucketBits));

    private static uint Mix(uint value)
    {
        var product = (ulong)value * Multiplier;

        return (uint)product ^ (uint)(product >> 32);
    }
}
