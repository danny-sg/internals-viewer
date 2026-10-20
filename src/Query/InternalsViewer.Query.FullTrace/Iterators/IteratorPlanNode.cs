namespace InternalsViewer.Query.FullTrace.Iterators;

internal readonly partial record struct IteratorPlanNode(ulong Iterator,
                                                         ulong PlanObject,
                                                         ulong BatchPlanObject,
                                                         ulong Query,
                                                         int NodeId,
                                                         TracePoint? Constructed = null,
                                                         bool IsBatchHelper = false);
