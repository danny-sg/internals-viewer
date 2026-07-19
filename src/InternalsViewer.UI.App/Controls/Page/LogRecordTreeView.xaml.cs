using System.Collections.Generic;
using System.Collections.ObjectModel;
using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.Page;

public sealed partial class LogRecordTreeView
{
    private readonly Dictionary<LogRecordItem, TreeViewNode> _nodesByItem = new(ReferenceEqualityComparer.Instance);

    private readonly Dictionary<LogRecordAnnotation, TreeViewNode> _nodesByAnnotation =
        new(ReferenceEqualityComparer.Instance);

    // Same selection echo guard as MarkerTreeView - SelectedRecord is TwoWay-bound so a selection made here can
    // come back through the view model
    private bool _isSyncingSelection;

    // Selection at the point the pointer went down, captured before the click's own selection processing runs -
    // a tap that lands on the record that was selected before the click is a repeat click, which toggles off
    private LogRecordItem? _selectedAtPointerPressed;

    private LogRecordAnnotation? _annotationAtPointerPressed;

    public LogRecordTreeView()
    {
        InitializeComponent();

        // Registered with handledEventsToo as the TreeView's internals mark pointer events handled before they
        // would bubble here
        TreeView.AddHandler(PointerPressedEvent,
                            new PointerEventHandler((_, _) =>
                            {
                                _selectedAtPointerPressed = SelectedRecord;
                                _annotationAtPointerPressed = SelectedAnnotation;
                            }),
                            true);

        TreeView.AddHandler(TappedEvent, new TappedEventHandler(TreeView_Tapped), true);
    }

    public ObservableCollection<LogRecordItem>? Records
    {
        get => (ObservableCollection<LogRecordItem>?)GetValue(RecordsProperty);
        set => SetValue(RecordsProperty, value);
    }

    public static readonly DependencyProperty RecordsProperty = DependencyProperty
        .Register(nameof(Records),
            typeof(ObservableCollection<LogRecordItem>),
            typeof(LogRecordTreeView),
            new PropertyMetadata(null, OnRecordsChanged));

    public LogRecordItem? SelectedRecord
    {
        get => (LogRecordItem?)GetValue(SelectedRecordProperty);
        set => SetValue(SelectedRecordProperty, value);
    }

    public static readonly DependencyProperty SelectedRecordProperty = DependencyProperty
        .Register(nameof(SelectedRecord),
            typeof(LogRecordItem),
            typeof(LogRecordTreeView),
            new PropertyMetadata(null, OnSelectedRecordChanged));

    // Nodes are built directly rather than via ItemsSource for the same container recycling reason as
    // MarkerTreeView (microsoft/microsoft-ui-xaml#7044). Annotations are added as child nodes, so a record with an
    // empty annotation list renders as a plain row and gains an expander once a replay populates it.
    private static void OnRecordsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LogRecordTreeView control)
        {
            return;
        }

        control.TreeView.RootNodes.Clear();
        control._nodesByItem.Clear();
        control._nodesByAnnotation.Clear();

        foreach (var item in (e.NewValue as ObservableCollection<LogRecordItem>) ?? [])
        {
            var node = new TreeViewNode { Content = item, IsExpanded = true };

            control._nodesByItem[item] = node;

            foreach (var annotation in item.Annotations)
            {
                var annotationNode = new TreeViewNode { Content = annotation };

                control._nodesByAnnotation[annotation] = annotationNode;

                node.Children.Add(annotationNode);
            }

            control.TreeView.RootNodes.Add(node);
        }

        if (control.SelectedAnnotation is not null
            && !control._nodesByAnnotation.ContainsKey(control.SelectedAnnotation))
        {
            control.SelectedAnnotation = null;
        }

        control.SyncSelectedNode(control.SelectedRecord);
    }

    public LogRecordAnnotation? SelectedAnnotation
    {
        get => (LogRecordAnnotation?)GetValue(SelectedAnnotationProperty);
        set => SetValue(SelectedAnnotationProperty, value);
    }

    public static readonly DependencyProperty SelectedAnnotationProperty = DependencyProperty
        .Register(nameof(SelectedAnnotation),
            typeof(LogRecordAnnotation),
            typeof(LogRecordTreeView),
            new PropertyMetadata(null, OnSelectedAnnotationChanged));

    private static void OnSelectedAnnotationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LogRecordTreeView control || control._isSyncingSelection)
        {
            return;
        }

        if (e.NewValue is LogRecordAnnotation annotation)
        {
            control._isSyncingSelection = true;

            control.TreeView.SelectedNode = control._nodesByAnnotation.GetValueOrDefault(annotation);

            control._isSyncingSelection = false;
        }
        else
        {
            control.SyncSelectedNode(control.SelectedRecord);
        }
    }

    private static void OnSelectedRecordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LogRecordTreeView control || control._isSyncingSelection)
        {
            return;
        }

        control.SyncSelectedNode(e.NewValue as LogRecordItem);
    }

    private void SyncSelectedNode(LogRecordItem? item)
    {
        _isSyncingSelection = true;

        TreeView.SelectedNode = item is not null && _nodesByItem.TryGetValue(item, out var node)
            ? node
            : null;

        _isSyncingSelection = false;
    }

    // A tap on the record that was already selected when the pointer went down is a repeat click, which toggles
    // the record off and restores the page to its original state. Comparing against the pointer-pressed snapshot
    // makes this immune to whether the click's own selection processing runs before or after Tapped bubbles.
    private void TreeView_Tapped(object sender, TappedRoutedEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;

        TreeViewNode? tappedNode = null;

        while (element is not null)
        {
            if (element is FrameworkElement { DataContext: TreeViewNode node })
            {
                tappedNode = node;
                break;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        if (tappedNode?.Content is not LogRecordItem item)
        {
            return;
        }

        if (!ReferenceEquals(item, _selectedAtPointerPressed) || !ReferenceEquals(item, SelectedRecord))
        {
            return;
        }

        // A click on the record while one of its annotation rows was selected returns selection to the record
        // rather than toggling it off
        if (_annotationAtPointerPressed is not null)
        {
            return;
        }

        _isSyncingSelection = true;

        TreeView.SelectedNode = null;

        _isSyncingSelection = false;

        SelectedAnnotation = null;
        SelectedRecord = null;
    }

    private void TreeView_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        if (_isSyncingSelection)
        {
            return;
        }

        _isSyncingSelection = true;

        // Selecting an annotation keeps the record selection (and therefore the replay state) where it was
        if (sender.SelectedNode?.Content is LogRecordAnnotation annotation)
        {
            SelectedAnnotation = annotation;
        }
        else
        {
            SelectedAnnotation = null;
            SelectedRecord = sender.SelectedNode?.Content as LogRecordItem;
        }

        _isSyncingSelection = false;
    }
}
