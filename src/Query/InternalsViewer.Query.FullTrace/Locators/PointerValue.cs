namespace InternalsViewer.Query.FullTrace.Locators;

internal readonly record struct PointerValue(ulong Function,
                                             ulong Instance,
                                             ulong Value,
                                             uint Call,
                                             int Register,
                                             uint Offset = 0,
                                             uint IsReadAtReturn = 0,
                                             uint Mode = 0,
                                             uint Part = 0);
