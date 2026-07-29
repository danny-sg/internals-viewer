using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.ViewModels.Docking;

public sealed partial class DocumentViewModel : ObservableObject
{
    private FrameworkElement? _cachedView;

    private ContentControl? _currentHolder;

    public DocumentViewModel(string title,
                             object content,
                             Func<FrameworkElement> viewFactory,
                             bool canClose = true,
                             bool keepAlive = false,
                             string? key = null,
                             bool persist = true)
    {
        Title = title;
        Content = content;
        ViewFactory = viewFactory;
        CanClose = canClose;
        KeepAlive = keepAlive;
        Key = key ?? title;
        Persist = persist;
    }

    /// <summary>Stable identifier used to persist/restore which documents are open and where.</summary>
    public string Key { get; }

    /// <summary>
    /// When false the document is excluded from the saved layout (e.g. dynamically opened index tabs,
    /// which are reopened on demand rather than restored).
    /// </summary>
    public bool Persist { get; }

    /// <summary>
    /// The object set as the view's <c>DataContext</c> (e.g. the shared query view model)
    /// </summary>
    public object Content { get; }

    /// <summary>
    /// Builds a view instance for this document
    /// </summary>
    public Func<FrameworkElement> ViewFactory { get; }

    /// <summary>
    /// When true the single view instance is cached and reused across show/hide and re-layout
    /// </summary>
    public bool KeepAlive { get; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _canClose;

    /// <summary>
    /// Returns the element to host as a tab's content, with the view's <c>DataContext</c> set to
    /// <see cref="Content"/>. For keep-alive documents the cached view is reused: it lives inside a
    /// plain <see cref="ContentControl"/> holder we control, and is moved to a fresh holder on each
    /// (re)build. Moving between our own holders detaches synchronously, which a <c>TabView</c>'s
    /// selection-driven content presenter does not guarantee when reparenting directly.
    /// </summary>
    public FrameworkElement CreateView()
    {
        if (!KeepAlive)
        {
            var view = ViewFactory();
            view.DataContext = Content;
            return view;
        }

        _cachedView ??= ViewFactory();
        _cachedView.DataContext = Content;

        // Detach from the previous holder before re-hosting (an element can only have one parent).
        _currentHolder?.Content = null;

        var holder = new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = _cachedView
        };

        _currentHolder = holder;

        return holder;
    }

    /// <summary>
    /// Disposes and drops the cached view (for keep-alive documents) when the tab is closed
    /// </summary>
    public void DisposeView()
    {
        if (_currentHolder is not null)
        {
            _currentHolder.Content = null;
            _currentHolder = null;
        }

        if (_cachedView is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _cachedView = null;
    }

    /// <summary>
    /// Convenience factory for the common case of a parameterless view bound to <paramref name="content"/>
    /// </summary>
    public static DocumentViewModel Create<TView>(string title,
                                                  object content,
                                                  bool canClose = true,
                                                  bool keepAlive = false,
                                                  string? key = null)
        where TView : FrameworkElement, new()
        => new(title, content, static () => new TView(), canClose, keepAlive, key);
}
