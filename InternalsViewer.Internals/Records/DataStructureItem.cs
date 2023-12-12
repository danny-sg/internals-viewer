using System;

namespace InternalsViewer.Internals.Records;

/// <summary>
/// Custom attribute to store mark properties
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class DataStructureItem(DataStructureItemType dataStructureItemType, string description) : Attribute
{
    public string Description { get; set; } = description;

    public DataStructureItemType DataStructureItemType { get; set; } = dataStructureItemType;

    public DataStructureItem(DataStructureItemType dataStructureItemType) : this(dataStructureItemType, null)
    {
    }

    public bool Visible { get; set; }
}