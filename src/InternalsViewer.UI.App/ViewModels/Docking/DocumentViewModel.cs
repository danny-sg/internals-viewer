using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.ViewModels.Docking;

/// <summary>
/// A single tab in the dock. The docking layer is content-agnostic: a document carries the
/// <see cref="Content"/> object used as its view's <c>DataContext</c> and a <see cref="ViewFactory"/>
/// that builds the view on demand. This keeps the dock infrastructure decoupled from any specific
/// view or view model.
/// <para>
/// When <see cref="KeepAlive"/> is set, the view instance is created once and reused — so hiding the
/// tab (or moving/splitting groups) preserves its live state (e.g. a SQL editor's text) rather than
/// rebuilding it.
/// </para>
/// </summary>
public sealed partial class DocumentViewModel : ObservableObject
{
    private FrameworkElement? cachedView;
    private ContentControl? currentHolder;

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

    /// <summary>The object set as the view's <c>DataContext</c> (e.g. the shared query view model).</summary>
    public object Content { get; }

    /// <summary>Builds a view instance for this document.</summary>
    public Func<FrameworkElement> ViewFactory { get; }

    /// <summary>When true the single view instance is cached and reused across show/hide and re-layout.</summary>
    public bool KeepAlive { get; }

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private bool canClose;

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

        cachedView ??= ViewFactory();
        cachedView.DataContext = Content;

        // Detach from the previous holder before re-hosting (an element can only have one parent).
        if (currentHolder is not null)
        {
            currentHolder.Content = null;
        }

        var holder = new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = cachedView
        };

        currentHolder = holder;

        return holder;
    }

    /// <summary>Disposes and drops the cached view (for keep-alive documents) when the tab is closed.</summary>
    public void DisposeView()
    {
        if (currentHolder is not null)
        {
            currentHolder.Content = null;
            currentHolder = null;
        }

        if (cachedView is IDisposable disposable)
        {
            disposable.Dispose();
        }

        cachedView = null;
    }

    /// <summary>Convenience factory for the common case of a parameterless view bound to <paramref name="content"/>.</summary>
    public static DocumentViewModel Create<TView>(string title,
                                                  object content,
                                                  bool canClose = true,
                                                  bool keepAlive = false,
                                                  string? key = null)
        where TView : FrameworkElement, new()
        => new(title, content, static () => new TView(), canClose, keepAlive, key);
}
