using InternalsViewer.Query.FullTrace.Hooks;
using InternalsViewer.Query.FullTrace.Locators.Enums;

namespace InternalsViewer.Query.FullTrace.Locators;

internal readonly record struct LocatorRead(ulong Function,
                                            int Register,
                                            bool IsReadAtReturn,
                                            LocatorValueType ValueType,
                                            int Offset = 0,
                                            int[]? Path = null,
                                            int CountRegister = -1) : IArgumentRead;
