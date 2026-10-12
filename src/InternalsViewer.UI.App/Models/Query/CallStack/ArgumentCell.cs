using InternalsViewer.Query.FullTrace.Locators;
using InternalsViewer.Query.CallStack.Locators;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public readonly record struct ArgumentCell(ulong? Value, Locator? Locator, string? Type = null);
