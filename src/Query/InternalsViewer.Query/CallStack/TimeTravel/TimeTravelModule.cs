using System.Reflection.PortableExecutable;

namespace InternalsViewer.Query.CallStack.TimeTravel;

public sealed record TimeTravelModule(string Path, ulong Address, ulong Size, string Pdb = "", string Guid = "", int Age = 0)
{
    public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

    public static TimeTravelModule Describe(string path, ulong address, ulong size)
    {
        try
        {
            using var stream = File.OpenRead(path);

            using var reader = new PEReader(stream);

            var codeView = reader.ReadDebugDirectory().FirstOrDefault(d => d.Type == DebugDirectoryEntryType.CodeView);

            if (codeView.DataSize > 0)
            {
                var data = reader.ReadCodeViewDebugDirectoryData(codeView);

                return new TimeTravelModule(path,
                                            address,
                                            size,
                                            System.IO.Path.GetFileName(data.Path),
                                            data.Guid.ToString("D").ToUpperInvariant(),
                                            data.Age);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
        }

        return new TimeTravelModule(path, address, size);
    }

    internal CallstackFrame Frame(ulong address, ulong instance) => new()
    {
        Module = Name,
        Address = address,
        Rva = (uint)(address - Address),
        Pdb = Pdb,
        Guid = Guid,
        Age = Age,
        Instance = instance
    };
}
