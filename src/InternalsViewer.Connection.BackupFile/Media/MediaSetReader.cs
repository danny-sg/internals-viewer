using InternalsViewer.Connection.BackupFile.Content;
using InternalsViewer.Connection.BackupFile.Mtf.Blocks;
using InternalsViewer.Connection.BackupFile.Mtf.Blocks.Descriptors;
using InternalsViewer.Connection.BackupFile.Mtf.Configuration;
using InternalsViewer.Connection.BackupFile.Mtf.Streams;
using InternalsViewer.Connection.BackupFile.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace InternalsViewer.Connection.BackupFile.Media;

/// <summary>
/// Reads and validates a set of backup files into a media set
/// </summary>
/// <remarks>
/// Unvalidated per-file claims (LoadedFamily) are checked for consistency and completeness before being projected to the ordered,
/// validated MediaSet - holding a MediaSet is proof validation has run, and the raw claims never leave this class.
/// </remarks>
internal static class MediaSetReader
{
    public static MediaSet Read(IReadOnlyList<MediaSource> sources)
    {
        // Parse media family (files) from filenames
        var families = sources.Select(LoadFamily).ToList();

        // Validate files/media set
        ValidateMediaSet(families);

        var configuration = families.Select(GetConfiguration).FirstOrDefault(c => c is not null);

        // Project to ordered, validated version of media family
        var orderedFamilies = families.OrderBy(f => f.Raid?.FamilySequence ?? 1)
                                      .Select(f => new MediaFamily(f.Raid?.FamilySequence ?? 1,
                                                                         f.Filename,
                                                                         f.Content,
                                                                         f.Blocks))
                                      .ToList();

        return new MediaSet(configuration, orderedFamilies);
    }

    /// <summary>
    /// Parsed - but not validated media family
    /// </summary>
    private sealed record LoadedFamily(string Filename,
                                       IContentSource Content,
                                       IReadOnlyList<DescriptorBlock> Blocks,
                                       RaidStreamInfo? Raid);

    /// <summary>
    /// Loads backup family (.bak file)
    /// </summary>
    /// <remarks>
    /// Loads to blocks and RAID info
    /// </remarks>
    private static LoadedFamily LoadFamily(MediaSource source)
    {
        var loader = new MtfBlockLoader(NullLogger<MtfBlockLoader>.Instance,
                                        new ContentStream(source.Content));

        List<DescriptorBlock> blocks;

        try
        {
            blocks = loader.Load();
        }
        finally
        {
            loader.Reader.Dispose();
        }

        var raidData = blocks.Where(b => b.BlockType == BlockType.Tape)
                             .SelectMany(b => b.Streams)
                             .FirstOrDefault(s => s.Header.StreamId == StreamTypes.RaidStream)?
                             .Data;

        var raid = raidData is null ? null : RaidStreamInfo.Parse(raidData);

        return new LoadedFamily(source.Filename, source.Content, blocks, raid);
    }

    /// <summary>
    /// Parses the backup configuration (MQCI stream) from the family's MSCI block
    /// </summary>
    /// <remarks>
    /// Every observed file carries an identical copy, so the first one found is used.
    ///
    /// The configuration is not needed to load the database - it is captured for consumers such as a connect dialog preview.
    /// </remarks>
    private static BackupConfiguration? GetConfiguration(LoadedFamily family)
    {
        var configData = family.Blocks
                               .Where(b => b.BlockType == BlockType.MSCI)
                               .SelectMany(b => b.Streams)
                               .FirstOrDefault(s => s.Header.StreamId == StreamTypes.SqlConfigurationStream)?
                               .Data;

        return configData is null || configData.Length == 0 ? null : BackupConfigurationParser.Parse(configData);
    }

    /// <summary>
    /// Validates the files form a complete and consistent media set
    /// </summary>
    /// <remarks>
    /// Each check guards a specific silent failure:
    ///
    ///  - Missing RAID info - completeness can't be established (tolerated for a single file, which may be complete)
    ///
    ///  - Mixed media set ids - files from different backups would interleave into corruption
    ///
    ///  - Duplicate family sequences - two copies of the same family, e.g. mirrored backups
    /// 
    ///  - Count/coverage - the RAID family count is the only source for how many files should exist, so an incomplete set fails here
    ///    instead of silently indexing part of the database
    /// </remarks>
    private static void ValidateMediaSet(IReadOnlyList<LoadedFamily> families)
    {
        var raids = families.Select(f => f.Raid).ToList();

        if (raids.Any(r => r is null))
        {
            if (families.Count == 1)
            {
                return;
            }

            throw new BackupMediaSetException(
                "One or more of the backup files does not have media set information - the files cannot be validated " +
                "as a complete media set.");
        }

        var mediaSetIds = raids.Select(r => r!.MediaSetId).Distinct().ToList();

        if (mediaSetIds.Count > 1)
        {
            throw new BackupMediaSetException(
                "The backup files are not part of the same media set - check the files are all from the same backup.");
        }

        var familyCount = raids[0]!.FamilyCount;

        var sequences = raids.Select(r => r!.FamilySequence).OrderBy(s => s).ToList();

        var duplicateFamilies = sequences.GroupBy(s => s)
                                         .Where(g => g.Count() > 1)
                                         .Select(g => g.Key)
                                         .ToList();

        if (duplicateFamilies.Count > 0)
        {
            throw new BackupMediaSetException(
                $"More than one file was provided for media family number(s): {string.Join(", ", duplicateFamilies)}. " +
                "The files may be mirrored copies of the same media family - provide only one copy of each family.");
        }

        if (families.Count != familyCount || !sequences.SequenceEqual(Enumerable.Range(1, familyCount).Select(i => (ushort)i)))
        {
            var missing = Enumerable.Range(1, familyCount)
                                    .Select(i => (ushort)i)
                                    .Except(sequences)
                                    .ToList();

            var missingText = missing.Count > 0 ? $" Missing media family number(s): {string.Join(", ", missing)}." : string.Empty;

            throw new BackupMediaSetException(
                $"The backup is striped across {familyCount} files but {families.Count} were provided.{missingText}");
        }
    }
}
