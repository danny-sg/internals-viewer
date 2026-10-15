using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.UI.App.Controls.CallStack;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.Models.Query.CallStack;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.Views.Query.Tabs.CallStack;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using System.Threading;
using System.Threading.Tasks;
using InternalsViewer.Query.FullTrace.RowBuffers;

namespace InternalsViewer.UI.App.ViewModels.Query.CallStack;

public sealed class CallTreeTabViewModel : IDisposable
{
    private const double TreeStar = 3;

    private const double PanelStar = 2;

    public CallTreeTabViewModel(QueryViewModel query, SettingsViewModel settings, ILogger logger)
    {
        Query = query;
        Settings = settings;
        Logger = logger;

        TreeDocument = DocumentViewModel.Create<CallTreePanelView>("Call Tree", this, canClose: false, keepAlive: true, key: "CallTree");
        MembersDocument = DocumentViewModel.Create<CallTreeMembersPanelView>("Members", this, keepAlive: true, key: "Members");
        SymbolsDocument = DocumentViewModel.Create<CallTreeSymbolsPanelView>("Symbols", this, keepAlive: true, key: "Symbols");
        ArgumentsDocument = DocumentViewModel.Create<CallTreeArgumentsPanelView>("Arguments", this, keepAlive: true, key: "Arguments");

        Dock = new DockLayoutViewModel(new TabGroupNode(TreeDocument));

        Dock.DocumentClosed += OnDocumentClosed;

        Dock.LayoutChanged += OnDockLayoutChanged;

        Query.Layout.PropertyChanged += OnLayoutPropertyChanged;

        Settings.PropertyChanged += OnSettingsPropertyChanged;

        Query.PropertyChanged += OnQueryPropertyChanged;

        Query.QueryOptions.PropertyChanged += OnQueryOptionsPropertyChanged;

        Arguments.SourceChanged += OnArgumentsSourceChanged;

        Arguments.SetExclusions(GetExclusions());

        Arguments.SetDecoded(Settings.DecodeValues);

        Tree = new CallTreeViewModel(this, settings);

        var members = Query.Layout.IsCallTreeMembersVisible;

        var symbols = Query.Layout.IsCallTreeSymbolsVisible;

        SetPanelVisible(ArgumentsDocument, Query.Layout.IsTrace);

        SetPanelVisible(MembersDocument, members);

        SetPanelVisible(SymbolsDocument, symbols);
    }

    public event Action? SymbolSearchRequested;

    public QueryViewModel Query { get; }

    public CallTreeViewModel Tree { get; }

    public SymbolsViewModel Symbols => Query.Symbols;

    public ArgumentsViewModel Arguments => Query.Arguments;

    public DockLayoutViewModel Dock { get; }

    private SettingsViewModel Settings { get; }

    private ILogger Logger { get; }

    private DocumentViewModel TreeDocument { get; }

    private DocumentViewModel MembersDocument { get; }

    private DocumentViewModel SymbolsDocument { get; }

    private DocumentViewModel ArgumentsDocument { get; }

    private List<DocumentViewModel> AddedDocuments { get; } = [];

    public void ShowMembers() => ShowPanel(MembersDocument, MembersDocument);

    public void ShowSymbols()
    {
        ShowPanel(SymbolsDocument, SymbolsDocument);

        SymbolSearchRequested?.Invoke();
    }

    public void ShowArguments() => ShowPanel(ArgumentsDocument, ArgumentsDocument);

    public void ListMembers(CallStackNode node)
    {
        _ = Symbols.ListMembersAsync(node);

        ShowMembers();
    }

    public void ListMembers(string typeName)
    {
        _ = Symbols.ListMembersAsync(typeName);

        ShowMembers();
    }

    public void FindReferences(ulong value)
    {
        if (Arguments.Lookup is not { } lookup)
        {
            return;
        }

        var shown = AddedDocuments.FirstOrDefault(d => d.Content is ValueReferencesViewModel { Value: var found } && found == value);

        if (shown is { } existing)
        {
            Dock.Activate(existing);

            return;
        }

        var references = new ValueReferencesViewModel(value, lookup, this, Logger);

        AddDocument(new DocumentViewModel(references.Title,
                                          references,
                                          static () => new CallTreeReferencesPanelView(),
                                          keepAlive: true,
                                          key: $"Find{value:X}",
                                          persist: false));

        _ = references.LoadAsync();
    }

    public void ShowRowBuffers(CallStackNode node)
    {
        if (Arguments.Lookup is not { } lookup || node.Frame is not { } frame)
        {
            return;
        }

        if (AddedDocuments.FirstOrDefault(f => f.Content is RowBuffersViewModel shown && shown.IsShowing(node)) is { } existing)
        {
            Dock.Activate(existing);

            return;
        }

        var rowBuffers = new RowBuffersViewModel(node, lookup, this, Logger, Settings.DecodeValues);

        AddDocument(new DocumentViewModel(rowBuffers.Title,
                                          rowBuffers,
                                          static () => new CallTreeRowBuffersPanelView(),
                                          keepAlive: true,
                                          key: $"RowBuffers{frame.Address:X}{frame.Instance:X}",
                                          persist: false));

        _ = rowBuffers.LoadAsync();
    }

    public void ShowCallStack(CallStackNode node, int call)
    {
        if (AddedDocuments.FirstOrDefault(d => d.Content is CallStackViewModel shown && shown.IsShowing(node, call)) is { } existing)
        {
            Dock.Activate(existing);

            return;
        }

        var stack = new CallStackViewModel(Query);

        _ = stack.OpenAsync(node, call);

        AddDocument(new DocumentViewModel(stack.Title,
                                          stack,
                                          static () => new CallStackControl(),
                                          keepAlive: true,
                                          key: $"CallStack{node.Frame?.Address:X}{node.Frame?.Instance:X}{call}",
                                          persist: false,
                                          commandsFactory: static () => TabCommands.Panel(0, new DecodeToggle())));
    }

    public void OpenLocator(Locator locator, bool detached)
    {
        if (locator.ValueType == LocatorValueType.ColumnStoreObject)
        {
            var target = new ColumnstoreObjectTarget(locator.ObjectType, locator.ColumnId, locator.ObjectId);

            Query.OpenColumnstoreObject(locator.HobtId, target);

            return;
        }

        OpenPage(locator.Page, locator.ValueType == LocatorValueType.Row ? locator.Slot : null, detached);
    }

    public async Task OpenPagePointerAsync(RowBufferPage pointer, bool detached)
    {
        var slot = Arguments.Lookup?.RowBufferPages is { } pages ? await pages.GetSlotAsync(pointer, CancellationToken.None) : null;

        OpenPage(pointer.Page, slot is { } found ? (ushort)found : null, detached);
    }

    public void OpenPage(PageAddress pageAddress, ushort? slot, bool detached)
    {
        if (!detached)
        {
            Query.OpenPage(pageAddress, slot);

            return;
        }

        _ = WeakReferenceMessenger.Default.Send(new OpenPageMessage(new OpenPageRequest(Query.Database, pageAddress)
        {
            Slot = slot,
            LogRecords = Query.GetPageLogRecords(pageAddress)
        }));
    }

    public void Dispose()
    {
        Dock.DocumentClosed -= OnDocumentClosed;

        Dock.LayoutChanged -= OnDockLayoutChanged;

        Query.Layout.PropertyChanged -= OnLayoutPropertyChanged;

        Settings.PropertyChanged -= OnSettingsPropertyChanged;

        Query.PropertyChanged -= OnQueryPropertyChanged;

        Query.QueryOptions.PropertyChanged -= OnQueryOptionsPropertyChanged;

        Arguments.SourceChanged -= OnArgumentsSourceChanged;

        Tree.Dispose();

        foreach (var document in AddedDocuments)
        {
            (document.Content as IDisposable)?.Dispose();
        }

        AddedDocuments.Clear();
    }

    private void AddDocument(DocumentViewModel document)
    {
        var neighbour = AddedDocuments.LastOrDefault(Dock.Contains) ?? ArgumentsDocument;

        AddedDocuments.Add(document);

        ShowPanel(document, neighbour);
    }

    private CallExclusions GetExclusions()
        => new(Settings.CallTreeHiddenCategorySet,
               Settings.CallTreePrunedCategorySet,
               Query.CroppedRoots?.ToHashSet<CallStackNode>(ReferenceEqualityComparer.Instance));

    private void ShowPanel(DocumentViewModel document, DocumentViewModel neighbour)
    {
        if (Dock.Contains(document))
        {
            Dock.Activate(document);

            return;
        }

        var placed = Dock.Contains(neighbour) ? neighbour : PanelDocuments().FirstOrDefault(Dock.Contains);

        if (placed is not null)
        {
            Dock.ShowBeside(document, placed);

            return;
        }

        Dock.SetRoot(new SplitNode(Orientation.Horizontal, Dock.Root, new TabGroupNode(document))
        {
            FirstStar = TreeStar,
            SecondStar = PanelStar
        });
    }

    private IEnumerable<DocumentViewModel> PanelDocuments() => [ArgumentsDocument, SymbolsDocument, MembersDocument, .. AddedDocuments];

    private void SetPanelVisible(DocumentViewModel document, bool isVisible)
    {
        if (isVisible == Dock.Contains(document))
        {
            return;
        }

        if (isVisible)
        {
            ShowPanel(document, document);
        }
        else
        {
            Dock.Close(document);
        }
    }

    private void OnLayoutPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(QueryLayoutViewModel.IsTrace):
                SetPanelVisible(ArgumentsDocument, Query.Layout.IsTrace);
                break;

            case nameof(QueryLayoutViewModel.IsCallTreeMembersVisible):
                SetPanelVisible(MembersDocument, Query.Layout.IsCallTreeMembersVisible);
                break;

            case nameof(QueryLayoutViewModel.IsCallTreeSymbolsVisible):
                SetPanelVisible(SymbolsDocument, Query.Layout.IsCallTreeSymbolsVisible);
                break;
        }
    }

    private void OnDockLayoutChanged(object? sender, EventArgs e)
    {
        Query.Layout.IsCallTreeMembersVisible = Dock.Contains(MembersDocument);

        Query.Layout.IsCallTreeSymbolsVisible = Dock.Contains(SymbolsDocument);
    }

    private void OnDocumentClosed(object? sender, DocumentViewModel document)
    {
        if (AddedDocuments.Remove(document))
        {
            (document.Content as IDisposable)?.Dispose();
        }
    }

    private void OnArgumentsSourceChanged()
    {
        foreach (var document in AddedDocuments.ToList())
        {
            Dock.Close(document);
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SettingsViewModel.DecodeValues):
                Arguments.SetDecoded(Settings.DecodeValues);

                foreach (var rowBuffers in AddedDocuments.Select(d => d.Content).OfType<RowBuffersViewModel>())
                {
                    rowBuffers.SetDecoded(Settings.DecodeValues);
                }

                break;

            case nameof(SettingsViewModel.CallTreeHiddenCategories) or nameof(SettingsViewModel.CallTreePrunedCategories):
                RefreshExclusions();
                break;
        }
    }

    private void OnQueryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueryViewModel.CallStack))
        {
            RefreshExclusions();
        }
    }

    private void OnQueryOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueryOptionsViewModel.CropToQuery))
        {
            RefreshExclusions();
        }
    }

    private void RefreshExclusions() => Arguments.SetExclusions(GetExclusions());
}
