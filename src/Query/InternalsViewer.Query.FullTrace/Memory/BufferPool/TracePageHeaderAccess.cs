using System.Reflection;
using InternalsViewer.Internals.Annotations;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.Internals.Services.Pages.Parsers;

namespace InternalsViewer.Query.FullTrace.Memory.BufferPool;

public readonly record struct TracePageHeaderAccess(TracePageAccess Access, UInt128 Bytes)
{
    private static string?[] FieldNames { get; } = GetFieldNames();

    public static UInt128 GetBytes(int offset, int length)
    {
        var count = Math.Min(length, PageHeader.Size - offset);

        return ((UInt128.One << count) - UInt128.One) << offset;
    }

    public List<string> GetFields()
    {
        var fields = new List<string>();

        var isUnnamedRun = false;

        for (var offset = 0; offset < FieldNames.Length; offset++)
        {
            if (!IsByteRead(offset))
            {
                isUnnamedRun = false;

                continue;
            }

            if (FieldNames[offset] is { } name)
            {
                isUnnamedRun = false;

                if (!fields.Contains(name))
                {
                    fields.Add(name);
                }
            }
            else if (!isUnnamedRun)
            {
                isUnnamedRun = true;

                fields.Add($"Offset 0x{offset:X}");
            }
        }

        return fields;
    }

    private bool IsByteRead(int offset) => ((Bytes >> offset) & UInt128.One) == UInt128.One;

    private static string?[] GetFieldNames()
    {
        var names = new string?[PageHeader.Size];

        foreach (var item in PageHeaderParser.Parse(new byte[PageHeader.Size]).MarkItems.OfType<PropertyItem>().Where(i => i.Length > 0))
        {
            Array.Fill(names, GetFieldName(item.PropertyName), item.Offset, item.Length);
        }

        return names;
    }

    private static string GetFieldName(string property)
        => typeof(PageHeader).GetProperty(property)?.GetCustomAttribute<DataStructureItemAttribute>() is { Name.Length: > 0 } attribute
            ? attribute.Name
            : property.SplitCamelCase();
}
