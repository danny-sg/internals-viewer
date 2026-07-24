using System.Buffers.Binary;

namespace InternalsViewer.Connection.BackupFile.Format.Streams;

/// <summary>
/// RAID stream provides information about a media family's context within the media set
/// </summary>
/// <remarks>
/// The RAID stream appears in the media header of a media family before any content.
///
/// The RAID stream for each file is compared to validate the media set - same media set id, family count matched,
/// and family sequences complete with no duplicates.
/// </remarks>
internal sealed record RaidStreamInfo(Guid MediaSetId, ushort FamilyCount, ushort FamilySequence)
{
    private const int MediaSetIdOffset = 0;

    private const int FamilyCountOffset = 20;

    private const int FamilySequenceOffset = 24;

    private const int MinimumLength = 26;

    public static RaidStreamInfo? Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < MinimumLength)
        {
            return null;
        }

        // Unique identifier for the backup
        var mediaSetId = new Guid(data.Slice(MediaSetIdOffset, 16));

        // Total number of families in the media set
        var familyCount = BinaryPrimitives.ReadUInt16LittleEndian(data[FamilyCountOffset..]);

        // Sequence number of this family within the media set
        // e.g. [Family Sequence] of [Family Count]
        var familySequence = BinaryPrimitives.ReadUInt16LittleEndian(data[FamilySequenceOffset..]);

        return new RaidStreamInfo(mediaSetId, familyCount, familySequence);
    }
}
