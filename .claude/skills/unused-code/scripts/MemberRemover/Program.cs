using MemberRemover;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: MemberRemover <root directory> <removals.tsv>");

    return 1;
}

var root = Path.GetFullPath(args[0]);

var removals = File.ReadAllLines(args[1])
                   .Where(l => l.Trim().Length > 0 && !l.StartsWith('#'))
                   .Select(l => l.Split('\t'))
                   .Select(p => new Removal(p[0], int.Parse(p[1]), p.Length > 2 ? p[2].Trim() : string.Empty))
                   .GroupBy(r => r.Path, StringComparer.OrdinalIgnoreCase);

var failed = false;

foreach (var file in removals)
{
    var result = SourceFile.Remove(Path.Combine(root, file.Key), [.. file]);

    foreach (var problem in result.Problems)
    {
        Console.WriteLine($"{file.Key}: {problem}");
    }

    if (result.Problems.Count > 0)
    {
        failed = true;

        continue;
    }

    Console.WriteLine($"{file.Key}: removed {string.Join(", ", result.Removed)}");
}

return failed ? 1 : 0;
