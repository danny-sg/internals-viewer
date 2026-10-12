using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using InternalsViewer.Internals.Engine.Database;
using InternalsViewer.Query.FullTrace.Export.Converters;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events;
using InternalsViewer.TransactionLog.LogRecords;

namespace InternalsViewer.Query.FullTrace.Export;

internal static class ExportPackage
{
    public const string EventIdProperty = "EventId";

    public const string TopLevelProperty = "TopLevel";

    public const string LogRecordIdProperty = "LogRecordId";

    private const string TypeProperty = "$type";

    private static readonly HashSet<string> ReadableProperties = ["Description", "ObjectName"];

    private static readonly HashSet<Type> Collections =
    [
        typeof(IEnumerable<>), typeof(IReadOnlyList<>), typeof(IReadOnlyCollection<>), typeof(IList<>), typeof(ICollection<>), typeof(List<>)
    ];

    public static JsonSerializerOptions Plain { get; } = Create(writeIndented: false);

    public static JsonSerializerOptions Indented { get; } = Create(writeIndented: true);

    public static JsonSerializerOptions Populating { get; } = new(Create(writeIndented: false))
    {
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate
    };

    private static IReadOnlyList<Type> EventTypes { get; } = DerivedTypes(typeof(EngineEvent));

    private static IReadOnlyList<Type> LogRecordTypes { get; } = DerivedTypes(typeof(LogRecord));

    private static ConcurrentDictionary<Type, PropertyInfo[]> ReferenceProperties { get; } = new();

    public static JsonSerializerOptions For(ExportReferences references)
    {
        var resolver = new DefaultJsonTypeInfoResolver();

        resolver.Modifiers.Add(info => Modify(info, references));

        return new JsonSerializerOptions(Plain) { TypeInfoResolver = resolver };
    }

    public static IEnumerable<object> ReferencesOf(object value)
    {
        foreach (var property in ReferenceProperties.GetOrAdd(value.GetType(), FindReferenceProperties))
        {
            var current = property.GetValue(value);

            if (current is EngineEvent or LogRecord or CallStackNode)
            {
                yield return current;
            }
            else if (current is System.Collections.IEnumerable items)
            {
                foreach (var item in items)
                {
                    if (item is not null)
                    {
                        yield return item;
                    }
                }
            }
        }
    }

    private static JsonSerializerOptions Create(bool writeIndented) => new()
    {
        WriteIndented = writeIndented,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters =
        {
            new JsonStringEnumConverter(),
            new PageAddressConverter(),
            new RowIdentifierConverter(),
            new LogSequenceNumberConverter()
        }
    };

    private static void Modify(JsonTypeInfo info, ExportReferences references)
    {
        if (info.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        if (info.Type == typeof(EngineEvent))
        {
            info.PolymorphismOptions = Polymorphism(EventTypes);
        }
        else if (info.Type == typeof(LogRecord))
        {
            info.PolymorphismOptions = Polymorphism(LogRecordTypes);
        }

        foreach (var property in info.Properties.ToList())
        {
            if (property.Set is null)
            {
                if (!ReadableProperties.Contains(property.Name) || property.Get is not { } get)
                {
                    info.Properties.Remove(property);

                    continue;
                }

                property.Get = Readable(get);
            }

            if (ConverterFor(property.PropertyType, references) is { } converter)
            {
                property.CustomConverter = converter;
            }
        }

        if (typeof(EngineEvent).IsAssignableFrom(info.Type))
        {
            AddEventIdentity(info, references);
        }
        else if (typeof(LogRecord).IsAssignableFrom(info.Type))
        {
            AddLogRecordIdentity(info, references);
        }
    }

    private static void AddEventIdentity(JsonTypeInfo info, ExportReferences references)
    {
        var id = info.CreateJsonPropertyInfo(typeof(int), EventIdProperty);

        id.Get = o => references.Events.IdOf((EngineEvent)o);

        id.Set = (o, value) => references.Events.Register((int)value!, (EngineEvent)o);

        id.Order = int.MinValue;

        info.Properties.Add(id);

        var topLevel = info.CreateJsonPropertyInfo(typeof(bool?), TopLevelProperty);

        topLevel.Get = o => references.TopLevel.Contains((EngineEvent)o) ? true : null;

        topLevel.Set = (o, value) =>
        {
            if (value is true)
            {
                references.TopLevelOrder.Add((EngineEvent)o);
            }
        };

        topLevel.Order = int.MinValue + 1;

        info.Properties.Add(topLevel);
    }

    private static void AddLogRecordIdentity(JsonTypeInfo info, ExportReferences references)
    {
        var id = info.CreateJsonPropertyInfo(typeof(int), LogRecordIdProperty);

        id.Get = o => references.LogRecords.IdOf((LogRecord)o);

        id.Set = (o, value) => references.LogRecords.Register((int)value!, (LogRecord)o);

        id.Order = int.MinValue;

        info.Properties.Add(id);
    }

    private static Func<object, object?> Readable(Func<object, object?> get) => o =>
    {
        try
        {
            return get(o);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or NullReferenceException)
        {
            return null;
        }
    };

    private static JsonConverter? ConverterFor(Type type, ExportReferences references)
    {
        if (type == typeof(AllocationUnit))
        {
            return new AllocationUnitConverter(references.AllocationUnitOf);
        }

        if (Base(type) is { } single)
        {
            return Reference(typeof(ReferenceConverter<,>).MakeGenericType(type, single), single, references);
        }

        if (ElementOf(type) is { } element && Base(element) is { } elementBase)
        {
            return Reference(typeof(ReferenceListConverter<,,>).MakeGenericType(type, element, elementBase), elementBase, references);
        }

        return null;
    }

    private static JsonConverter Reference(Type converter, Type baseType, ExportReferences references)
    {
        object table = baseType == typeof(CallStackNode)
            ? references.Nodes
            : baseType == typeof(EngineEvent)
                ? references.Events
                : references.LogRecords;

        return (JsonConverter)Activator.CreateInstance(converter, table)!;
    }

    private static PropertyInfo[] FindReferenceProperties(Type type)
        => [.. type.GetProperties().Where(p => p.CanWrite && p.GetIndexParameters().Length == 0 && IsReferenceType(p.PropertyType))];

    private static bool IsReferenceType(Type type) => Base(type) is not null || (ElementOf(type) is { } element && Base(element) is not null);

    private static Type? Base(Type type)
    {
        if (type == typeof(CallStackNode))
        {
            return typeof(CallStackNode);
        }

        if (typeof(EngineEvent).IsAssignableFrom(type))
        {
            return typeof(EngineEvent);
        }

        return typeof(LogRecord).IsAssignableFrom(type) ? typeof(LogRecord) : null;
    }

    private static Type? ElementOf(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        return type.IsGenericType && Collections.Contains(type.GetGenericTypeDefinition()) ? type.GetGenericArguments()[0] : null;
    }

    private static JsonPolymorphismOptions Polymorphism(IEnumerable<Type> types)
    {
        var options = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = TypeProperty,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization
        };

        foreach (var type in types)
        {
            options.DerivedTypes.Add(new JsonDerivedType(type, type.Name));
        }

        return options;
    }

    private static List<Type> DerivedTypes(Type root)
        => [.. root.Assembly.GetTypes().Where(t => t != root && !t.IsAbstract && root.IsAssignableFrom(t)).OrderBy(t => t.Name)];
}
