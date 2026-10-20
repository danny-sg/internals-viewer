namespace InternalsViewer.Query.FullTrace.Locators.Parsers;

internal readonly record struct LocatorTypeName(string Name, int Offset = 0, int[]? Path = null);
