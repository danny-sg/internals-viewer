using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Text;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed class ImageText(IReadOnlyList<TraceModule> modules) : IDisposable
{
    private const int MaximumCharacters = 260;

    private Dictionary<string, ModuleImage?> Images { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? Read(ulong address, bool isWide)
    {
        foreach (var module in modules)
        {
            if (address < module.StartAddress || address - module.StartAddress >= module.Size)
            {
                continue;
            }

            if (!Images.TryGetValue(module.Path, out var image))
            {
                image = ModuleImage.Open(module.Path);

                Images[module.Path] = image;
            }

            return image?.ReadText((uint)(address - module.StartAddress), isWide);
        }

        return null;
    }

    public void Dispose()
    {
        foreach (var image in Images.Values)
        {
            image?.Dispose();
        }

        Images.Clear();
    }

    private sealed class ModuleImage(FileStream stream, ImmutableArray<SectionHeader> sections) : IDisposable
    {
        private FileStream Stream { get; } = stream;

        private ImmutableArray<SectionHeader> Sections { get; } = sections;

        public static ModuleImage? Open(string path)
        {
            FileStream? stream = null;

            try
            {
                stream = File.OpenRead(path);

                var headers = new PEHeaders(stream);

                return new ModuleImage(stream, headers.SectionHeaders);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException)
            {
                stream?.Dispose();

                return null;
            }
        }

        public string? ReadText(uint rva, bool isWide)
        {
            foreach (var section in Sections)
            {
                var offset = (long)rva - section.VirtualAddress;

                if (offset < 0 || offset >= section.SizeOfRawData)
                {
                    continue;
                }

                if ((section.SectionCharacteristics & SectionCharacteristics.MemWrite) != 0)
                {
                    return null;
                }

                var width = isWide ? 2 : 1;

                var buffer = new byte[(int)Math.Min(MaximumCharacters * width, section.SizeOfRawData - offset)];

                Stream.Position = section.PointerToRawData + offset;

                var read = Stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);

                for (var index = 0; index + width <= read; index += width)
                {
                    if (buffer[index] == 0 && (width == 1 || buffer[index + 1] == 0))
                    {
                        return isWide ? Encoding.Unicode.GetString(buffer, 0, index) : Encoding.Latin1.GetString(buffer, 0, index);
                    }
                }

                return null;
            }

            return null;
        }

        public void Dispose() => Stream.Dispose();
    }
}
