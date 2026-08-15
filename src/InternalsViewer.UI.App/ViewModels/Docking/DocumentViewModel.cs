using System;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.UI.App.Controls.Docking;
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
                             bool persist = true,
                             Func<FrameworkElement>? commandsFactory = null)
    {
        Title = title;
        Content = content;
        ViewFactory = viewFactory;
        CanClose = canClose;
        KeepAlive = keepAlive;
        Key = key ?? title;
        Persist = persist;
        CommandsFactory = commandsFactory;
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
    /// Builds the commands shown in the hosting tab strip while this document is selected, if it has any
    /// </summary>
    public Func<FrameworkElement>? CommandsFactory { get; }

    /// <summary>
    /// When true the single view instance is cached and reused across show/hide and re-layout
    /// </summary>
    public bool KeepAlive { get; }

    public string Title { get; }

    public bool CanClose { get; }

    public Microsoft.UI.Xaml.Media.Brush? Accent { get; set; }

    /// <summary>
    /// The document a click last landed on, which its tab header shows in bold
    /// </summary>
    /// <remarks>
    /// Selection within a group is the tab view's own, and says nothing across groups. This is the owner's notion of which of its
    /// documents is the current one, so a layout that spreads them over several groups still has one.
    /// </remarks>
    [ObservableProperty]
    private bool _isSelected;

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
    /// Fills <paramref name="host"/> with this document's tab strip commands
    /// </summary>
    /// <remarks>
    /// A fresh element every time, from <see cref="CommandsFactory"/> or from the view. Moving one element from strip
    /// to strip is what WinUI rejects, however it is detached first, so nothing is kept to move.
    /// </remarks>
    public void HostCommandsIn(Panel host)
    {
        host.Children.Clear();

        if (BuildCommands() is { } commands)
        {
            host.Children.Add(commands);
        }
    }

    private FrameworkElement? BuildCommands()
    {
        if (CommandsFactory is { } factory)
        {
            var commands = factory();

            commands.DataContext = Content;

            return commands;
        }

        return (_cachedView as IDocumentCommands)?.CreateCommands();
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

    /// <summary>
    /// Convenience factory for a document that contributes <typeparamref name="TCommands"/> to its tab strip
    /// </summary>
    public static DocumentViewModel Create<TView, TCommands>(string title,
                                                             object content,
                                                             bool canClose = true,
                                                             bool keepAlive = false,
                                                             string? key = null)
        where TView : FrameworkElement, new()
        where TCommands : FrameworkElement, new()
        => new(title,
               content,
               static () => new TView(),
               canClose,
               keepAlive,
               key,
               commandsFactory: static () => new TCommands());
}
