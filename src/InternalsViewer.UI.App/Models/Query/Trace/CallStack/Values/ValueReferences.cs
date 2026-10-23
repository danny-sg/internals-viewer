using System.Collections.Generic;
using InternalsViewer.Query.FullTrace.Iterators;

namespace InternalsViewer.UI.App.Models.Query.Trace.CallStack.Values;

public sealed record ValueReferences(string? Type, IteratorTarget? Iterator, IReadOnlyList<ValueUseGroup> Groups);
