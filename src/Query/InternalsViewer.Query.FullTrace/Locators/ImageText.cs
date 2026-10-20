using System.Collections.Immutable;
using System.Reflection.PortableExecutable;

namespace InternalsViewer.Query.FullTrace.Locators;

internal sealed class ImageText(IReadOnlyList<TraceModule> modules) : IDisposable
{
    private Dictionary<string, ModuleImage?> Images { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int Read(ulong address, Span<byte> destination, bool isReadOnly = false)
        => GetImage(address, out var rva)?.Read(rva, destination, isReadOnly) ?? 0;

    public void Dispose()
    {
        foreach (var image in Images.Values)
        {
            image?.Dispose();
        }

        Images.Clear();
    }

    private ModuleImage? GetImage(ulong address, out uint rva)
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

            rva = (uint)(address - module.StartAddress);

            return image;
        }

        rva = 0;

        return null;
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

        public int Read(uint rva, Span<byte> destination, bool isReadOnly)
        {
            foreach (var section in Sections)
            {
                var offset = (long)rva - section.VirtualAddress;

                if (offset < 0 || offset >= section.SizeOfRawData)
                {
                    continue;
                }

                if (isReadOnly && (section.SectionCharacteristics & SectionCharacteristics.MemWrite) != 0)
                {
                    return 0;
                }

                Stream.Position = section.PointerToRawData + offset;

                return Stream.ReadAtLeast(destination[..(int)Math.Min(destination.Length, section.SizeOfRawData - offset)],
                                          1,
                                          throwOnEndOfStream: false);
            }

            return 0;
        }

        public void Dispose() => Stream.Dispose();
    }
}
