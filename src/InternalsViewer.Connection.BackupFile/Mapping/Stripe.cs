using InternalsViewer.Connection.BackupFile.Mtf.Blocks.Descriptors;
using InternalsViewer.Connection.BackupFile.Interfaces;

namespace InternalsViewer.Connection.BackupFile.Mapping;

/// <summary>
/// One file of a striped media set opened and numbered ready for indexing
/// </summary>
/// <remarks>
/// A striped backup splits pages across files. Page runs record the stripe Index so a read can be resolved back to
/// the right file.
///
/// Index is the 0-based position in the ordered media set and must match the reader's handle list - both are derived
/// from the same family sequence ordering.
/// </remarks>
internal sealed record Stripe(int Index, IContentSource Content, IReadOnlyList<DescriptorBlock> Blocks);
