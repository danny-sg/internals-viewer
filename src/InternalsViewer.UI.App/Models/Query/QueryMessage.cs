using CommunityToolkit.Mvvm.ComponentModel;

namespace InternalsViewer.UI.App.Models.Query;

public sealed partial class QueryMessage(string text, double? percentage, bool isStep = false) : ObservableObject
{
    [ObservableProperty]
    private double? _percentage = percentage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFinished))]
    private string? _outcome;

    public string Text { get; } = text;

    public bool IsProgress { get; } = isStep || percentage is not null;

    public bool IsFinished => Outcome is not null;
}
