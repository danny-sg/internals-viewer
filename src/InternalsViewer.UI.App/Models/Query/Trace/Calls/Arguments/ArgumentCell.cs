using InternalsViewer.Query.FullTrace.Locators;


namespace InternalsViewer.UI.App.Models.Query.Trace.Calls.Arguments;

public readonly record struct ArgumentCell(ulong? Value, Locator? Locator, string? Type = null);
