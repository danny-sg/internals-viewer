using InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs.Enums;

namespace InternalsViewer.Query.FullTrace.Memory.Layouts.Tabs;

public sealed record MemoryTab(string Header, string Type, MemoryTabLayout Layout, string? Trailing = null);
