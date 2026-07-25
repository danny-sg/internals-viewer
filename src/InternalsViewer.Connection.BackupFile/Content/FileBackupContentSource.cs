using Microsoft.Win32.SafeHandles;

namespace InternalsViewer.Connection.BackupFile.Content;

/// <summary>
/// Content source for an uncompressed backup, where logical offsets are file offsets
/// </summary>
internal sealed class FileBackupContentSource : IBackupContentSource
{
    private readonly SafeFileHandle _handle;

    public FileBackupContentSource(string filename)
    {
        _handle = File.OpenHandle(filename, FileMode.Open, FileAccess.Read, FileShare.Read);

        Length = RandomAccess.GetLength(_handle);
    }

    public long Length { get; }

    public void Read(long offset, Span<byte> buffer)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var read = RandomAccess.Read(_handle, buffer[totalRead..], offset + totalRead);

            if (read == 0)
            {
                throw new EndOfStreamException($"Unexpected end of backup file reading at offset {offset + totalRead}.");
            }

            totalRead += read;
        }
    }

    public void Dispose() => _handle.Dispose();
}
