using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading;

namespace InternalsViewer.UI.App.ViewModels.Tabs;

public partial class TabViewModel : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isLoading = true;

    protected TabViewModel()
    {
        DispatcherQueue = UiDispatcher.ForCurrentThread();
    }

    public string TabId { get; } = Guid.NewGuid().ToString();

    protected UiDispatcher DispatcherQueue { get; }

    protected CancellationToken CancellationToken => _cts.Token;

    public virtual void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}