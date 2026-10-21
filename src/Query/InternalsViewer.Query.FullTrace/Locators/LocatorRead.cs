using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.FullTrace.Hooks;

namespace InternalsViewer.Query.FullTrace.Locators;

internal readonly record struct LocatorRead(ulong Function,
                                            int Register,
                                            bool IsReadAtReturn,
                                            LocatorValueType ValueType,
                                            int Offset = 0,
                                            int[]? Path = null) : IArgumentRead;
