namespace InternalsViewer.Query.Events.EventTypes;

public readonly record struct SymbolKey(string Pdb, string Guid, int Age, uint Rva);