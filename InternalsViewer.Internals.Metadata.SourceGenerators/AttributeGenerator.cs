﻿using Microsoft.CodeAnalysis;
using System;

namespace InternalsViewer.Internals.Metadata.SourceGenerators;

[Generator]
public class AttributeGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(i =>
        {
            var attributeSource = @"using System;
using System.Data;

namespace InternalsViewer.Internals.Generators;

[AttributeUsage(AttributeTargets.Class)]
public class InternalsMetadataAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public class InternalsMetadataColumnAttribute(string Name, int ColumnId, SqlDbType DataType, int DataLength, short LeafOffset, short NullBit) 
    : Attribute;
";

            i.AddSource("InternalsMetadataAttribute.g.cs", attributeSource);
        });
    }
}
