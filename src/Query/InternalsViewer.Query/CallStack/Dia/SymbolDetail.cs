namespace InternalsViewer.Query.CallStack.Dia;

/// <summary>
/// A public symbol with its undecorated signature, address and kind
/// </summary>
internal readonly record struct SymbolDetail(string Name, string Signature, uint Rva, bool IsFunction);
