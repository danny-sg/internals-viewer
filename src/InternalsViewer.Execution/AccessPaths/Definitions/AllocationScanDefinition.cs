using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.Execution.AccessPaths.Definitions;

/// <summary>
/// Describes a scan that follows an IAM chain and reads pages in allocation order
/// </summary>
public sealed record AllocationScanDefinition(PageAddress FirstIamPage) : IteratorDefinition;
