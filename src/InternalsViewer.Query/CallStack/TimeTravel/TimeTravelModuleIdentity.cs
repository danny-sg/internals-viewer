using System.Reflection.PortableExecutable;

namespace InternalsViewer.Query.CallStack.TimeTravel;

internal sealed record TimeTravelModuleIdentity(string Name, ulong Address, ulong Size, string Pdb, string Guid, int Age)
{
    public static TimeTravelModuleIdentity Describe(TimeTravelModule module)
    {
        var name = Path.GetFileNameWithoutExtension(module.Path);

        try
        {
            using var stream = File.OpenRead(module.Path);

            using var reader = new PEReader(stream);

            var codeView = reader.ReadDebugDirectory().FirstOrDefault(d => d.Type == DebugDirectoryEntryType.CodeView);

            if (codeView.DataSize > 0)
            {
                var data = reader.ReadCodeViewDebugDirectoryData(codeView);

                return new TimeTravelModuleIdentity(name,
                                                    module.Address,
                                                    module.Size,
                                                    Path.GetFileName(data.Path),
                                                    data.Guid.ToString("D").ToUpperInvariant(),
                                                    data.Age);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
        }

        return new TimeTravelModuleIdentity(name, module.Address, module.Size, string.Empty, string.Empty, 0);
    }

    public CallstackFrame Frame(ulong address, ulong instance) => new()
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
