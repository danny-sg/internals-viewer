using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Query.Events.Properties;

public sealed record EventProperty(string Name,
                                   string Value,
                                   EventPropertyType Type = EventPropertyType.Default,
                                   PageAddress? Page = null,
                                   RowIdentifier? Row = null);
