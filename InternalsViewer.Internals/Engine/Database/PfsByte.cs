using System.Text;

namespace InternalsViewer.Internals.Engine.Database;

/// <summary>
/// Page Free Space Byte
/// </summary>
/// <remarks>
/// See <see cref="Parsers.PfsByteParser"/> for details of the PFS byte structure
/// </remarks>
public record PfsByte
{
    public byte Byte { get; set; }

    public SpaceFree PageSpaceFree { get; set; }

    public bool GhostRecords { get; set; }

    public bool Iam { get; set; }

    public bool Mixed { get; set; }

    public bool Allocated { get; set; }

    public override string ToString()
    {
        var stringBuilder = new StringBuilder($"PFS Status");

        if (Allocated)
        {
            stringBuilder.Append(": Allocated");
        }
        else
        {
            stringBuilder.Append(": Not Allocated");
        }

        stringBuilder.Append($" | {SpaceFreeDescription(PageSpaceFree)} Full");

        if (Iam)
        {
            stringBuilder.Append(" | IAM Page");
        }

        if (Mixed)
        {
            stringBuilder.Append(" | Mixed Extent");
        }

        if (GhostRecords)
        {
            stringBuilder.Append(" | Has Ghost");
        }

        return stringBuilder.ToString();
    }

    public static string SpaceFreeDescription(SpaceFree spaceFree)
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