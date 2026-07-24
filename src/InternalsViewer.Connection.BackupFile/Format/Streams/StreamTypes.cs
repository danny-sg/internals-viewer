using System.Diagnostics.CodeAnalysis;

namespace InternalsViewer.Connection.BackupFile.Format.Streams;

[SuppressMessage("ReSharper", "StringLiteralTypo")]
internal static class StreamTypes
{
    public static readonly string StartPadStream = "APAD";

    public static readonly string EndPadStream = "SPAD";

    public static readonly string RaidStream = "RAID";

    public static readonly string SqlConfigurationStream = "MQCI";

    public static readonly string SqlDataStream = "MQDA";

    public static readonly string SqlTransactionLogStream = "MQTL";
}
