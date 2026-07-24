using InternalsViewer.Connection.BackupFile.Format.Blocks.Descriptors;
using Microsoft.Win32.SafeHandles;

namespace InternalsViewer.Connection.BackupFile.Index;

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
internal sealed record BackupStripe(int Index, SafeFileHandle Handle, IReadOnlyList<DescriptorBlock> Blocks);
