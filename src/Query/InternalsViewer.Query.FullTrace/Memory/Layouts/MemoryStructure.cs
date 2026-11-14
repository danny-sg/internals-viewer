namespace InternalsViewer.Query.FullTrace.Memory.Layouts;

public sealed class MemoryStructure(string name)
{
    public string Name { get; } = name;

    public string? Base { get; internal set; }

    public int Size { get; internal set; }

    public IReadOnlyList<MemoryField> Fields => FieldList;

    private List<MemoryField> FieldList { get; } = [];

    private Dictionary<string, List<MemoryField>> Rows { get; } = [];

    private Dictionary<string, List<MemoryField>> Elements { get; } = [];

    public MemoryField Get(string fieldName)
        => Rows.TryGetValue(fieldName, out var rows) && rows.Count == 1
            ? rows[0]
            : throw new InvalidDataException(rows is null
                                                 ? $"{Name} has no {fieldName} field"
                                                 : $"{Name} has more than one {fieldName} field");

    public IReadOnlyList<MemoryField> GetAll(string fieldName)
        => Elements.TryGetValue(fieldName, out var elements) ? elements : throw new InvalidDataException($"{Name} has no {fieldName} field");

    internal void Add(MemoryField field, int count)
    {
        AddTo(Rows, field.Name, field);

        for (var index = 0; index < count; index++)
        {
            var element = count == 1 ? field : field with { Name = $"{field.Name} {index}", Offset = field.Offset + index * field.Length };

            FieldList.Add(element);

            AddTo(Elements, field.Name, element);
        }
    }

    private static void AddTo(Dictionary<string, List<MemoryField>> lookup, string key, MemoryField field)
    {
        if (!lookup.TryGetValue(key, out var fields))
        {
            fields = [];

            lookup[key] = fields;
        }

        fields.Add(field);
    }
}
