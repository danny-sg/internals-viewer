using InternalsViewer.Query.CallStack.TimeTravel.Locators;

namespace InternalsViewer.UI.App.Models.Query.CallStack;

public readonly record struct ArgumentCell(ulong? Value, TimeTravelLocator? Locator, string? Type = null);
