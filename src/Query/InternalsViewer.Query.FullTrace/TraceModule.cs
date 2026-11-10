using InternalsViewer.Query.FullTrace.Calls.Tree;
using System.Diagnostics;
using System.Reflection.PortableExecutable;

namespace InternalsViewer.Query.FullTrace;

/// <summary>
/// Module loaded for Time Travel Debug trace
/// </summary>
internal sealed record TraceModule
{
    private TraceModule(string path, ulong startAddress, ulong size, string pdb = "", string guid = "", int age = 0)
    {
        Path = path;
        StartAddress = startAddress;
        Size = size;
        Identity = FrameModule.Get(System.IO.Path.GetFileNameWithoutExtension(path), pdb, guid, age);
    }

    public string Path { get; init; }

    public ulong StartAddress { get; init; }

    public ulong Size { get; init; }

    public FrameModule Identity { get; init; }

    public Version? ProductVersion { get; init; }

    public string Name => Identity.Name;

    /// <summary>
    /// Reads a module from a path and returns a TraceModule with the PDB information if available
    /// </summary>
    /// <remarks>
    /// This will attempt to read the file PE Header -> CodeView directory to extract the PDB information if available (name, GUID, age).
    ///
    /// If the file is not a valid PE file or cannot be read, it will return a TraceModule with only the path, address, and size.
    /// </remarks>
    public static TraceModule Read(string path, ulong address, ulong size)
    {
        try
        {
            var productVersion = GetProductVersion(path);

            using var stream = File.OpenRead(path);

            using var reader = new PEReader(stream);

            var codeView = reader.ReadDebugDirectory().FirstOrDefault(d => d.Type == DebugDirectoryEntryType.CodeView);

            if (codeView.DataSize > 0)
            {
                var data = reader.ReadCodeViewDebugDirectoryData(codeView);

                return new TraceModule(path,
                                       address,
                                       size,
                                       System.IO.Path.GetFileName(data.Path),
                                       data.Guid.ToString("D").ToUpperInvariant(),
                                       data.Age)
                {
                    ProductVersion = productVersion
                };
            }

            return new TraceModule(path, address, size) { ProductVersion = productVersion };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
        }

        return new TraceModule(path, address, size);
    }

    /// <summary>
    /// Creates a CallFrame for this module with the given address and instance
    /// </summary>
    /// <remarks>
    /// RVA (Relative Virtual Address) is calculated from the given address minus the module's base address (as RVA is relative to the
    /// module).
    /// </remarks>
    internal CallFrame GetFrame(ulong functionAddress, ulong instanceAddress) => new()
    {
        Module = Identity,
        Address = functionAddress,
        Rva = (uint)(functionAddress - StartAddress),
        InstanceAddress = instanceAddress
    };

    private static Version? GetProductVersion(string path)
    {
        var version = FileVersionInfo.GetVersionInfo(path);

        return version.ProductMajorPart == 0
            ? null
            : new Version(version.ProductMajorPart, version.ProductMinorPart, version.ProductBuildPart, version.ProductPrivatePart);
    }
}
