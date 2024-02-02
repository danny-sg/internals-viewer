using System.Text;

namespace InternalsViewer.Internals.Engine.Allocation.Enums;

/// <summary>
/// Page Free Space Byte
/// </summary>
/// <remarks>
/// See <see cref="Parsers.PfsByteParser"/> for details of the PFS byte structure
/// </remarks>
public record PfsByte
{
    public static readonly PfsByte Unknown = new() { Value = 0 };

    public byte Value { get; init; }

    public SpaceFree PageSpaceFree { get; init; }

    public bool GhostRecords { get; init; }

    public bool IsIam { get; init; }

    public bool IsMixed { get; init; }

    public bool IsAllocated { get; init; }

    public override string ToString()
    {
        var stringBuilder = new StringBuilder($"0x{Value:X2} ");

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