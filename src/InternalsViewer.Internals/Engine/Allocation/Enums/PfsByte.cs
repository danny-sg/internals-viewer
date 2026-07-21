using System.Text;

namespace InternalsViewer.Internals.Engine.Allocation.Enums;

/// <summary>
/// Page Free Space Byte
/// </summary>
/// <remarks>
/// Byte is expressed as MSb - Most Significant Bit first so smaller bits are on the right
/// 
///    PFS bits are as follows:
///    
///    Bits 87654 321
///         00000 000
///    
///    Bits 1-3 - Space Free value
///        
///        321
///        ---
///        000 - Empty
///        001 - 50%
///        010 - 80%
///        011 - 95%
///        100 - 100%
///        
///    Bit 4 - Is Ghost record
///    Bit 5 - Is IAM page
///    Bit 6 - Is Mixed Extent
///    Bit 7 - Is Allocated
///    Bit 8 - Unused
/// </remarks>
public readonly struct PfsByte(byte value) : IEquatable<PfsByte>
{
    public static readonly PfsByte Unknown = new(0);

    public byte Value { get; } = value;

    public SpaceFree PageSpaceFree => (SpaceFree)(Value & 0x07);

    public bool GhostRecords => (Value & 0x08) != 0;

    public bool IsIam => (Value & 0x10) != 0;

    public bool IsMixed => (Value & 0x20) != 0;

    public bool IsAllocated => (Value & 0x40) != 0;

    public static bool operator ==(PfsByte left, PfsByte right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(PfsByte left, PfsByte right)
    {
        return !(left == right);
    }

    public override string ToString()
    {
        var stringBuilder = new StringBuilder("PFS Status: ");

        if (IsAllocated)
        {
            stringBuilder.Append("Allocated");
        }
        else
        {
            stringBuilder.Append("Not Allocated");
        }

        stringBuilder.Append($" | {GetSpaceFreeDescription(PageSpaceFree)} Full");

        if (IsIam)
        {
            stringBuilder.Append(" | IAM Page");
        }

        if (IsMixed)
        {
            stringBuilder.Append(" | Mixed Extent");
        }

        if (GhostRecords)
        {
            stringBuilder.Append(" | Has Ghost");
        }

        return stringBuilder.ToString();
    }

    public bool Equals(PfsByte other)
    {
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return obj is PfsByte other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    private static string GetSpaceFreeDescription(SpaceFree spaceFree)
    {
        switch (spaceFree)
        {
            case SpaceFree.Empty:
                return "0%";
            case SpaceFree.FiftyPercent:
                return "50%";
            case SpaceFree.EightyPercent:
                return "80%";
            case SpaceFree.NinetyFivePercent:
                return "95%";
            case SpaceFree.OneHundredPercent:
                return "100%";
            default:
                return "Unknown";
        }
    }
}