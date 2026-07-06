using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using System;
using System.Threading;

namespace InternalsViewer.UI.App.ViewModels.Tabs;

public partial class TabViewModel : ObservableObject, IDisposable
{
    [ObservableProperty]
    private string _tabId = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isLoading = true;

    private readonly CancellationTokenSource _cts = new();

    protected DispatcherQueue DispatcherQueue { get; }

    protected CancellationToken CancellationToken => _cts.Token;

    protected TabViewModel()
    {
        DispatcherQueue = DispatcherQueue.GetForCurrentThread()
                          ?? throw new InvalidOperationException(
                              $"{GetType().Name} must be constructed on the UI thread.");

        TabId = Guid.NewGuid().ToString();
    }

    public virtual void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}