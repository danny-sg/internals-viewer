using System;
using System.Linq;
using InternalsViewer.Internals.Columnstore.Metadata;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.Views.Columnstore.Tabs;

namespace InternalsViewer.UI.App.ViewModels.Columnstore;

public sealed partial class ColumnstoreTabViewModel
{
    public DockLayoutViewModel Dock { get; }

    /// <summary>
    /// Structure and row groups open with the index, everything else opens from a click on one of them
    /// </summary>
    private DockLayoutViewModel BuildDock()
    {
        _structureDocument = DocumentViewModel.Create<ColumnstoreStructureTabView>("Structure",
                                                                                   this,
                                                                                   canClose: false,
                                                                                   keepAlive: true,
                                                                                   key: "Structure");

        _rowGroupsDocument = DocumentViewModel.Create<ColumnstoreMetadataTabView>("Metadata",
                                                                                   this,
                                                                                   canClose: false,
                                                                                   keepAlive: true,
                                                                                   key: "RowGroups");

        var dock = new DockLayoutViewModel(new TabGroupNode(_structureDocument, _rowGroupsDocument));

        dock.DocumentClosed += OnDocumentClosed;

        return dock;
    }

    private void OnDocumentClosed(object? sender, DocumentViewModel document) => DisposeDocument(document);

    private void DisposeDocument(DocumentViewModel document)
    {
        document.DisposeView();

        if (document.Content is IDisposable disposable && !ReferenceEquals(document.Content, this))
        {
            disposable.Dispose();
        }
    }

    public override void Dispose()
    {
        Dock.DocumentClosed -= OnDocumentClosed;

        foreach (var document in Dock.Groups().SelectMany(g => g.Documents).ToList())
        {
            DisposeDocument(document);
        }

        base.Dispose();
    }

    private DocumentViewModel? _structureDocument;

    private DocumentViewModel? _rowGroupsDocument;

    public void OpenSegment(SegmentSummary segment)
        => Open($"Segment {segment.RowGroupId}:{segment.ColumnId} ({segment.ColumnName})",
                $"Segment:{segment.RowGroupId}:{segment.ColumnId}",
                () => new ColumnstoreSegmentTabView(),
                new SegmentTabViewModel(ColumnstoreService, Database, segment, OpenDictionary));

    public void OpenDictionary(SegmentSummary segment)
    {
        var dictionary = segment.LocalDictionary ?? segment.GlobalDictionary;

        if (dictionary is not null)
        {
            OpenDictionary(dictionary);
        }
    }

    public void OpenDictionary(SegmentDictionary dictionary)
        => Open($"Dictionary {dictionary.ColumnId}:{dictionary.DictionaryId}",
                $"Dictionary:{dictionary.ColumnId}:{dictionary.DictionaryId}",
                () => new ColumnstoreDictionaryTabView(),
                new DictionaryTabViewModel(ColumnstoreService,
                                           Database,
                                           dictionary,
                                           Index?.Columns.FirstOrDefault(c => c.ColumnStoreColumnId == dictionary.ColumnId)));

    public void OpenDeleteBitmap()
        => Open("Delete bitmap", "DeleteBitmap", () => new ColumnstoreDeleteBitmapTabView(), this);

    public void OpenDeltaStore(RowGroupSummary rowGroup)
        => Open($"Delta store {rowGroup.RowGroupId}",
                $"DeltaStore:{rowGroup.RowGroupId}",
                () => new ColumnstoreDeltaStoreTabView(),
                rowGroup);

    /// <summary>
    /// Adds a document to the group the structure drawing sits in, or selects it if it is already open
    /// </summary>
    private void Open(string title, string key, System.Func<FrameworkElement> viewFactory, object content)
    {
        var group = Dock.Groups()
                        .FirstOrDefault(g => g.Documents.Any(d => d.Key == key))
                    ?? Dock.Groups().FirstOrDefault()
                    ?? new TabGroupNode();

        var existing = group.Documents.FirstOrDefault(d => d.Key == key);

        if (existing is not null)
        {
            group.SelectedDocument = existing;

            Dock.NotifySelectionChanged();

            return;
        }

        var document = new DocumentViewModel(title, content, viewFactory, keepAlive: true, key: key, persist: false);

        group.Documents.Add(document);
        group.SelectedDocument = document;

        Dock.NotifySelectionChanged();
    }
}
