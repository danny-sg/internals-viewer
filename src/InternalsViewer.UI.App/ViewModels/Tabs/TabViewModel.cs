using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading;

namespace InternalsViewer.UI.App.ViewModels.Tabs;

public partial class TabViewModel : ObservableObject, IDisposable
{
    public string TabId { get; } = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isLoading = true;

    private readonly CancellationTokenSource _cts = new();

    protected UiDispatcher DispatcherQueue { get; }

    protected CancellationToken CancellationToken => _cts.Token;

    protected TabViewModel()
    {
        DispatcherQueue = UiDispatcher.ForCurrentThread();
    }

    public virtual void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}