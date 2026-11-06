namespace UnusedCodeAnalyser;

internal sealed partial class ReferenceGraph
{
    public void Write(string outputDirectory, string solutionDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        var deadTypes = Declarations.Values
                                    .Where(d => d.IsType && !d.IsGenerated && !Live.Contains(d.Id))
                                    .OrderBy(d => d.Location, StringComparer.OrdinalIgnoreCase)
                                    .ToList();

        var deadMembers = Declarations.Values
                                      .Where(d => !d.IsType
                                                  && !d.IsGenerated
                                                  && !d.IsConstructor
                                                  && !Live.Contains(d.Id)
                                                  && d.ContainingType is not null
                                                  && Live.Contains(d.ContainingType))
                                      .OrderBy(d => d.FilePath, StringComparer.OrdinalIgnoreCase)
                                      .ThenBy(d => d.Line)
                                      .ToList();

        var removable = deadMembers.Where(d => !d.IsPositional && !d.IsLayout).ToList();

        using (var writer = Create(outputDirectory, "types.txt"))
        {
            foreach (var type in deadTypes)
            {
                writer.WriteLine($"{GetStatus(type.Id)}\t{type.Kind}\t{type.Display}\t{type.Location}\t{type.BaseTypeChain}");

                WriteReferences(writer, type.Id);
            }
        }

        using (var writer = Create(outputDirectory, "members.txt"))
        {
            foreach (var member in deadMembers)
            {
                writer.WriteLine($"{GetStatus(member.Id)}\t{GetKind(member)}\t{member.Display}\t{member.Location}");

                WriteReferences(writer, member.Id);
            }
        }

        using (var writer = Create(outputDirectory, "members-by-file.txt"))
        {
            foreach (var file in removable.GroupBy(d => d.FilePath))
            {
                writer.WriteLine(Path.GetRelativePath(solutionDirectory, file.Key));

                foreach (var member in file)
                {
                    writer.WriteLine($"  line {member.Line}: {member.Display} ({GetStatus(member.Id)})");
                }

                writer.WriteLine();
            }
        }

        using (var writer = Create(outputDirectory, "removals.tsv"))
        {
            foreach (var member in removable)
            {
                writer.WriteLine($"{Path.GetRelativePath(solutionDirectory, member.FilePath)}\t{member.Line}\t{member.Name}");
            }
        }

        var liveTypes = Declarations.Values.Where(d => d.IsType && !d.IsGenerated && Live.Contains(d.Id)).ToList();

        WriteNeverConstructed(outputDirectory, liveTypes);

        WriteImplementedOnly(outputDirectory, liveTypes);

        WriteRegistrationOnly(outputDirectory, liveTypes);

        Console.WriteLine();
        Console.WriteLine($"Declarations: {Declarations.Count}, live: {Live.Count}");
        Console.WriteLine($"Dead types: {deadTypes.Count}");

        foreach (var type in deadTypes)
        {
            Console.WriteLine($"    {type.Display} ({GetStatus(type.Id)})");
        }

        Console.WriteLine($"Dead members in live types: {deadMembers.Count}, {removable.Count} without positional or layout ones");
        Console.WriteLine($"Reports written to {outputDirectory}");
    }

    private static StreamWriter Create(string outputDirectory, string name) => new(Path.Combine(outputDirectory, name));

    private static string GetKind(Declaration declaration)
        => declaration.Kind
           + (declaration.IsPositional ? " (positional)" : string.Empty)
           + (declaration.IsLayout ? " (layout)" : string.Empty);

    private static bool IsExternal(string from) => from.StartsWith(ExternalPrefix, StringComparison.Ordinal);

    private static bool IsRegistration(string from)
        => from.Contains(".Register", StringComparison.Ordinal)
           || from.Contains("ConfigureServices", StringComparison.Ordinal)
           || from.Contains(".App.#ctor", StringComparison.Ordinal);

    private static bool IsTypeLevel(string from) => from.Split('|')[^1].StartsWith("T:", StringComparison.Ordinal);

    private string GetStatus(string id)
        => (ScopeReferences.ContainsKey(id), TestReferences.ContainsKey(id)) switch
        {
            (false, false) => "no references",
            (false, true) => "tests only",
            (true, false) => "only from dead code",
            (true, true) => "dead code and tests"
        };

    private bool IsLiveFrom(string from) => IsExternal(from) || Live.Contains(from);

    private bool IsWithin(string from, string typeId)
    {
        if (IsExternal(from))
        {
            return false;
        }

        for (var id = from; id is not null; id = Declarations.TryGetValue(id, out var declaration) ? declaration.ContainingType : null)
        {
            if (id == typeId)
            {
                return true;
            }
        }

        return false;
    }

    private List<string> GetOutsideReferences(Declaration type, bool includeMembers)
    {
        var references = Incoming.TryGetValue(type.Id, out var froms) ? froms.Keys.ToList() : [];

        if (includeMembers)
        {
            references.AddRange(Declarations.Values
                                            .Where(d => d.ContainingType == type.Id)
                                            .SelectMany(d => Incoming.TryGetValue(d.Id, out var memberFroms) ? memberFroms.Keys : []));
        }

        return [.. references.Where(r => IsLiveFrom(r) && !IsWithin(r, type.Id)).Distinct()];
    }

    private void WriteNeverConstructed(string outputDirectory, List<Declaration> types)
    {
        var constructors = Declarations.Values
                                       .Where(d => d.IsConstructor && d.ContainingType is not null)
                                       .GroupBy(d => d.ContainingType!)
                                       .ToDictionary(g => g.Key, g => g.Select(d => d.Id).ToList());

        var constructed = new HashSet<string>();

        foreach (var type in types)
        {
            if (GetOutsideReferences(type, includeMembers: false).Any(IsRegistration))
            {
                constructed.Add(type.Id);

                continue;
            }

            if (constructors.TryGetValue(type.Id, out var typeConstructors)
                && typeConstructors.Any(c => Incoming.TryGetValue(c, out var froms) && froms.Keys.Any(f => IsConstruction(f, type.Id))))
            {
                constructed.Add(type.Id);
            }
        }

        foreach (var id in constructed.ToList())
        {
            for (var baseId = Declarations[id].BaseTypeId;
                 baseId is not null;
                 baseId = Declarations.TryGetValue(baseId, out var declaration) ? declaration.BaseTypeId : null)
            {
                constructed.Add(baseId);
            }
        }

        using var writer = Create(outputDirectory, "never-constructed.txt");

        foreach (var type in types.Where(d => d.Kind.StartsWith("Class", StringComparison.Ordinal)
                                              && !d.IsAbstract
                                              && !d.IsStatic
                                              && !constructed.Contains(d.Id))
                                  .OrderBy(d => d.Location, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteLine($"{type.Kind}\t{type.Display}\t{type.Location}\t{type.BaseTypeChain}");

            foreach (var from in GetOutsideReferences(type, includeMembers: false).Order().Take(6))
            {
                writer.WriteLine($"\t\tfrom {from}");
            }
        }
    }

    private bool IsConstruction(string from, string typeId)
        => IsLiveFrom(from)
           && !(IsWithin(from, typeId) && Declarations.TryGetValue(from, out var declaration) && declaration.IsConstructor);

    private void WriteImplementedOnly(string outputDirectory, List<Declaration> types)
    {
        using var writer = Create(outputDirectory, "implemented-only.txt");

        foreach (var type in types.Where(d => d.Kind.StartsWith("Interface", StringComparison.Ordinal))
                                  .OrderBy(d => d.Location, StringComparer.OrdinalIgnoreCase))
        {
            var references = GetOutsideReferences(type, includeMembers: false);

            var memberReferences = GetOutsideReferences(type, includeMembers: true).Except(references).ToList();

            if (memberReferences.Count > 0 || !references.All(IsTypeLevel))
            {
                continue;
            }

            writer.WriteLine($"{type.Kind}\t{type.Display}\t{type.Location}\t{references.Count} implementations");

            foreach (var from in references.Order().Take(4))
            {
                writer.WriteLine($"\t\tfrom {from}");
            }
        }
    }

    private void WriteRegistrationOnly(string outputDirectory, List<Declaration> types)
    {
        using var writer = Create(outputDirectory, "registration-only.txt");

        foreach (var type in types.Where(d => !d.IsStatic).OrderBy(d => d.Location, StringComparer.OrdinalIgnoreCase))
        {
            var references = GetOutsideReferences(type, includeMembers: true);

            if (references.Count == 0 || !references.All(IsRegistration))
            {
                continue;
            }

            writer.WriteLine($"{type.Kind}\t{type.Display}\t{type.Location}");

            foreach (var from in references.Order().Take(6))
            {
                writer.WriteLine($"\t\tfrom {from}");
            }
        }
    }

    private void WriteReferences(StreamWriter writer, string id)
    {
        if (ScopeReferences.TryGetValue(id, out var scopeFroms))
        {
            foreach (var from in scopeFroms.Keys.Order().Take(6))
            {
                writer.WriteLine($"\t\tfrom {from}");
            }
        }

        if (TestReferences.TryGetValue(id, out var tests))
        {
            foreach (var from in tests.Keys.Order().Take(4))
            {
                writer.WriteLine($"\t\ttest {from}");
            }
        }
    }
}
