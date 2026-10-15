namespace InternalsViewer.Query.FullTrace.CustomHooks;

public sealed record CustomHookDefinition(string Name,
                                          string Module,
                                          string Guid,
                                          int Age,
                                          uint Rva,
                                          string Function,
                                          string Argument,
                                          int Register,
                                          bool IsReadAtReturn,
                                          int Offset,
                                          int Length);
