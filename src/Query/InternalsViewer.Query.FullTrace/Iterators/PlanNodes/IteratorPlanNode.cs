namespace InternalsViewer.Query.FullTrace.Iterators.PlanNodes;

internal readonly partial record struct IteratorPlanNode(ulong Iterator,
                                                         ulong PlanObject,
                                                         ulong BatchPlanObject,
                                                         ulong Query,
                                                         int NodeId,
                                                         TracePoint? Constructed = null,
                                                         bool IsBatchHelper = false);
