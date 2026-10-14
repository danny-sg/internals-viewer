using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.Models.Query.CallStack;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.Views.Query.Tabs.CallStack;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;

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
        Exclusions = GetExclusions();

        TreeDocument = DocumentViewModel.Create<CallTreePanelView>("Call Tree", this, canClose: false, keepAlive: true, key: "CallTree");
        MembersDocument = DocumentViewModel.Create<CallTreeMembersPanelView>("Members", this, keepAlive: true, key: "Members");
        SymbolsDocument = DocumentViewModel.Create<CallTreeSymbolsPanelView>("Symbols", this, keepAlive: true, key: "Symbols");
        ArgumentsDocument = DocumentViewModel.Create<CallTreeArgumentsPanelView>("Arguments", this, keepAlive: true, key: "Arguments");

        Dock = new DockLayoutViewModel(new SplitNode(Orientation.Horizontal,
                                                     new TabGroupNode(TreeDocument),
                                                     new TabGroupNode(MembersDocument, SymbolsDocument, ArgumentsDocument))
        {
            FirstStar = TreeStar,
            SecondStar = PanelStar
        });

        Dock.DocumentClosed += OnDocumentClosed;

        Settings.PropertyChanged += OnSettingsPropertyChanged;

        Query.PropertyChanged += OnQueryPropertyChanged;

        Query.QueryOptions.PropertyChanged += OnQueryOptionsPropertyChanged;

        Arguments.SourceChanged += OnArgumentsSourceChanged;

        Arguments.SetExclusions(Exclusions);

        Arguments.SetDecoded(Settings.DecodeValues);

        Tree = new CallTreeViewModel(this, settings);
    }

    public event Action? SymbolSearchRequested;

    public QueryViewModel Query { get; }

    public CallTreeViewModel Tree { get; }

    public SymbolsViewModel Symbols => Query.Symbols;

    public ArgumentsViewModel Arguments => Query.Arguments;

    public DockLayoutViewModel Dock { get; }

    public CallExclusions Exclusions { get; private set; }

    private SettingsViewModel Settings { get; }

    private ILogger Logger { get; }

    private DocumentViewModel TreeDocument { get; }

    private DocumentViewModel MembersDocument { get; }

    private DocumentViewModel SymbolsDocument { get; }

    private DocumentViewModel ArgumentsDocument { get; }

    private List<DocumentViewModel> Finds { get; } = [];

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

    public void Find(ulong value)
    {
        if (Arguments.Lookup is not { } lookup)
        {
            return;
        }

        if (Finds.FirstOrDefault(f => f.Content is ValueReferencesViewModel { Value: var found } && found == value) is { } existing)
        {
            Dock.Activate(existing);

            return;
        }

        var references = new ValueReferencesViewModel(value, lookup, this, Logger);

        var document = new DocumentViewModel(references.Title,
                                             references,
                                             static () => new CallTreeReferencesPanelView(),
                                             keepAlive: true,
                                             key: $"Find{value:X}",
                                             persist: false);

        var neighbour = Finds.LastOrDefault(Dock.Contains) ?? ArgumentsDocument;

        Finds.Add(document);

        ShowPanel(document, neighbour);

        _ = references.LoadAsync();
    }

    public void OpenLocator(Locator locator, bool detached)
    {
        if (locator.Kind == LocatorKind.ColumnStoreObject)
        {
            Query.OpenColumnstoreObject(locator.HobtId, new ColumnstoreObjectTarget(locator.ObjectType, locator.ColumnId, locator.ObjectId));

            return;
        }

        OpenPage(locator.Page, locator.Kind == LocatorKind.Row ? locator.Slot : null, detached);
    }

    public void OpenPage(PageAddress pageAddress, ushort? slot, bool detached)
    {
        if (!detached)
        {
            Query.OpenPage(pageAddress, slot);

            return;
        }

        WeakReferenceMessenger.Default.Send(new OpenPageMessage(new OpenPageRequest(Query.Database, pageAddress)
        {
            Slot = slot,
            LogRecords = Query.GetPageLogRecords(pageAddress)
        }));
    }

    public void Dispose()
    {
        Dock.DocumentClosed -= OnDocumentClosed;

        Settings.PropertyChanged -= OnSettingsPropertyChanged;

        Query.PropertyChanged -= OnQueryPropertyChanged;

        Query.QueryOptions.PropertyChanged -= OnQueryOptionsPropertyChanged;

        Arguments.SourceChanged -= OnArgumentsSourceChanged;

        Tree.Dispose();

        foreach (var find in Finds)
        {
            (find.Content as IDisposable)?.Dispose();
        }

        Finds.Clear();
    }

    private CallExclusions GetExclusions()
        => new(SettingsViewModel.SplitCategories(Settings.CallTreeHiddenCategories),
               SettingsViewModel.SplitCategories(Settings.CallTreePrunedCategories),
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

    private IEnumerable<DocumentViewModel> PanelDocuments() => [ArgumentsDocument, SymbolsDocument, MembersDocument, .. Finds];

    private void OnDocumentClosed(object? sender, DocumentViewModel document)
    {
        if (Finds.Remove(document))
        {
            (document.Content as IDisposable)?.Dispose();
        }
    }

    private void OnArgumentsSourceChanged()
    {
        foreach (var find in Finds.ToList())
        {
            Dock.Close(find);
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.DecodeValues))
        {
            Arguments.SetDecoded(Settings.DecodeValues);

            return;
        }

        if (e.PropertyName is not (nameof(SettingsViewModel.CallTreeHiddenCategories) or nameof(SettingsViewModel.CallTreePrunedCategories)))
        {
            return;
        }

        RefreshExclusions();
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

    private void RefreshExclusions()
    {
        Exclusions = GetExclusions();

        Arguments.SetExclusions(Exclusions);

        Arguments.SetDecoded(Settings.DecodeValues);

        foreach (var find in Finds)
        {
            if (find.Content is ValueReferencesViewModel references)
            {
                _ = references.LoadAsync();
            }
        }
    }
}
