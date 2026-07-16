using System.Text;

namespace InternalsViewer.Internals.Engine.Allocation.Enums;

/// <summary>
/// Page Free Space Byte
/// </summary>
/// <remarks>
/// </remarks>
public readonly struct PfsByte(byte value)
{
    public static readonly PfsByte Unknown = new(0);

    public byte Value { get; } = value;

    public SpaceFree PageSpaceFree => (SpaceFree)(Value & 0x07);

    public bool GhostRecords => (Value & 0x08) != 0;

    public bool IsIam => (Value & 0x10) != 0;

    public bool IsMixed => (Value & 0x20) != 0;

    public bool IsAllocated => (Value & 0x40) != 0;

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