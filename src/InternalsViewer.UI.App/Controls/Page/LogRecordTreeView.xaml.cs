using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using LogRecordItem = InternalsViewer.UI.App.Models.Logging.LogRecordItem;

namespace InternalsViewer.UI.App.Controls.Page;

public sealed partial class LogRecordTreeView
{
    public static readonly DependencyProperty RecordsProperty = DependencyProperty
        .Register(nameof(Records),
            typeof(ObservableCollection<LogRecordItem>),
            typeof(LogRecordTreeView),
            new PropertyMetadata(null, OnRecordsChanged));

    public ObservableCollection<LogRecordItem>? Records
    {
        get => (ObservableCollection<LogRecordItem>?)GetValue(RecordsProperty);
        set => SetValue(RecordsProperty, value);
    }

    public static readonly DependencyProperty SelectedRecordProperty = DependencyProperty
        .Register(nameof(SelectedRecord),
            typeof(LogRecordItem),
            typeof(LogRecordTreeView),
            new PropertyMetadata(null, OnSelectedRecordChanged));

    public LogRecordItem? SelectedRecord
    {
        get => (LogRecordItem?)GetValue(SelectedRecordProperty);
        set => SetValue(SelectedRecordProperty, value);
    }

    public static readonly DependencyProperty SelectedAnnotationProperty = DependencyProperty
        .Register(nameof(SelectedAnnotation),
            typeof(LogRecordAnnotation),
            typeof(LogRecordTreeView),
            new PropertyMetadata(null, OnSelectedAnnotationChanged));

    public LogRecordAnnotation? SelectedAnnotation
    {
        get => (LogRecordAnnotation?)GetValue(SelectedAnnotationProperty);
        set => SetValue(SelectedAnnotationProperty, value);
    }

    private readonly Dictionary<LogRecordAnnotation, TreeViewNode> _nodesByAnnotation =
        new(ReferenceEqualityComparer.Instance);

    // Same selection echo guard as MarkerTreeView - SelectedRecord and SelectedAnnotation are TwoWay-bound so a
    // change made here can come back through the view model
    private bool _isSyncingSelection;

    public LogRecordTreeView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Raised when a log record row is clicked, so its page slot can be selected
    /// </summary>
    public event System.Action<LogRecordItem>? RecordClicked;

    /// <summary>
    /// Brings the selected record's node to the top of the view, with its annotation children visible below it
    /// </summary>
    /// <remarks>
    /// A replay reassigns the records collection, rebuilding every node and resetting the tree's scroll position -
    /// deferred at low priority so the rebuilt containers have been realised by the layout pass first
    /// </remarks>
    private void ScrollToSelectedRecord()
    {
        if (SelectedRecord is null)
        {
            return;
        }

        var node = TreeView.RootNodes.FirstOrDefault(n => ReferenceEquals(n.Content, SelectedRecord));

        if (node is null)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (TreeView.ContainerFromNode(node) is UIElement container)
            {
                container.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0 });
            }
            else
            {
                // Virtualised out of view - scroll via the tree's underlying list so the container realises
                FindDescendant<ListView>(TreeView)?.ScrollIntoView(node, ScrollIntoViewAlignment.Leading);
            }
        });
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    /// <summary>
    /// Reflects the selected record into the item checkboxes
    /// </summary>
    /// <remarks>
    /// The target record and everything above it show checked, since a replay applies all earlier records too.
    /// The earlier records are disabled - only the target itself can be unchecked (restoring the page) or a later
    /// record checked (moving the replay forward); moving backwards is uncheck-then-check.
    /// </remarks>
    private void SyncCheckedStates()
    {
        _isSyncingSelection = true;

        var records = Records ?? [];

        var targetIndex = SelectedRecord is null ? -1 : records.IndexOf(SelectedRecord);

        for (var i = 0; i < records.Count; i++)
        {
            records[i].IsSelected = targetIndex >= 0 && i <= targetIndex;

            records[i].IsEnabled = targetIndex < 0 || i >= targetIndex;
        }

        _isSyncingSelection = false;
    }

    private void RecordCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (_isSyncingSelection)
        {
            return;
        }

        if ((sender as FrameworkElement)?.DataContext is TreeViewNode { Content: LogRecordItem item })
        {
            SelectedRecord = item;
        }
    }

    private void RecordCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_isSyncingSelection)
        {
            return;
        }

        if ((sender as FrameworkElement)?.DataContext is TreeViewNode { Content: LogRecordItem item }
            && ReferenceEquals(item, SelectedRecord))
        {
            SelectedRecord = null;
        }
    }

    private void TreeView_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        if (_isSyncingSelection)
        {
            return;
        }

        _isSyncingSelection = true;

        // Row selection only drives the annotation highlight - the record a page is replayed to is driven by the
        // checkboxes
        SelectedAnnotation = sender.SelectedNode?.Content as LogRecordAnnotation;

        _isSyncingSelection = false;

        if (sender.SelectedNode?.Content is LogRecordItem item)
        {
            RecordClicked?.Invoke(item);
        }
    }

    // Nodes are built directly rather than via ItemsSource for the same container recycling reason as
    // MarkerTreeView (microsoft/microsoft-ui-xaml#7044). Annotations are added as child nodes, so a record with an
    // empty annotation list renders as a plain row and gains children once a replay populates it.
    private static void OnRecordsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LogRecordTreeView control)
        {
            return;
        }

        control.TreeView.RootNodes.Clear();
        control._nodesByAnnotation.Clear();

        foreach (var item in (e.NewValue as ObservableCollection<LogRecordItem>) ?? [])
        {
            var node = new TreeViewNode { Content = item, IsExpanded = true };

            if (item.Annotations.Count > 0)
            {
                node.Children.Add(new TreeViewNode { Content = AnnotationHeader.Instance });
            }

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

        control.SyncCheckedStates();

        control.ScrollToSelectedRecord();
    }

    private static void OnSelectedRecordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LogRecordTreeView control)
        {
            control.SyncCheckedStates();
        }
    }

    private static void OnSelectedAnnotationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LogRecordTreeView control || control._isSyncingSelection)
        {
            return;
        }

        control._isSyncingSelection = true;

        control.TreeView.SelectedNode = e.NewValue is LogRecordAnnotation annotation
            ? control._nodesByAnnotation.GetValueOrDefault(annotation)
            : null;

        control._isSyncingSelection = false;
    }
}
