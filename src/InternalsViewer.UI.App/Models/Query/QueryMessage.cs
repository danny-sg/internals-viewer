using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query;

public sealed partial class QueryMessage(string text, double? percentage) : ObservableObject
{
    [ObservableProperty]
    private double? _percentage = percentage;

    public string Text { get; } = text;

    public bool IsProgress { get; } = percentage is not null;
}
