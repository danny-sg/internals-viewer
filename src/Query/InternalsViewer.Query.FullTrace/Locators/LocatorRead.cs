using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.Query.FullTrace.Locators;

internal readonly record struct LocatorRead(ulong Function,
                                            int Register,
                                            bool AtReturn,
                                            LocatorKind Kind,
                                            int Offset = 0,
                                            int[]? Path = null);
