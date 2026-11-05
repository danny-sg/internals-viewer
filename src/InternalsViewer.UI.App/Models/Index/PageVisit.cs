using InternalsViewer.Internals.Engine.Address;

namespace InternalsViewer.UI.App.Models.Index;

public readonly record struct PageVisit(long Position, PageAddress Page, int? Offset);
