using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace;
using InternalsViewer.UI.App.Services.Query.Trace;
using InternalsViewer.UI.App.ViewModels.Docking;
using InternalsViewer.UI.App.Views.Query.Tabs.Trace;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.ViewModels.Query.Trace;

public sealed partial class TraceTabViewModel
{
    private DocumentViewModel? _stepsDocument;

    private DocumentViewModel? _descriptionDocument;

    private DocumentViewModel? _planDocument;

    private Dictionary<int, DocumentViewModel>? _operatorDocumentsByNode;

    private TabGroupNode? _operatorGroup;

    [ObservableProperty]
    private bool _isNestedLayout;

    /// <summary>
    /// The operator whose tab is open, marked in the plan so a tab can be placed in the tree it came from
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<PlanNode> _activePlanNodes = [];

    public DockLayoutViewModel Dock { get; }

    public PlanNode? ActivePlanNode => ActivePlanNodes.Count > 0 ? ActivePlanNodes[0] : null;

    public void ActivateOperator(PlanNode? node)
    {
        if (node is null)
        {
            return;
        }

        var operatorIds = Operators.Select(o => o.NodeId).ToHashSet();

        var target = node;

        while (target is not null && !operatorIds.Contains(target.NodeId))
        {
            target = FindParent(PlanNode, target);
        }

        var targetId = target?.NodeId ?? (operatorIds.Contains(-1) ? -1 : (int?)null);

        if (targetId is not null)
        {
            SelectOperator(targetId.Value);
        }
    }

    /// <summary>
    /// Selects the operator a click asked for, which is not always one with a tab of its own
    /// </summary>
    /// <remarks>
    /// A join shows its inputs as panes rather than as tabs, so an input clicked there is described in place: the operator selected is the
    /// pane's, while the tab brought forward is the join it sits in.
    /// </remarks>
    public void ActivateOperator(int nodeId)
    {
        if (Layout.Nodes.ContainsKey(nodeId))
        {
            SelectOperator(nodeId);

            return;
        }

        if (_planNodesById.TryGetValue(nodeId, out var node))
        {
            ActivateOperator(node);
        }
    }

    /// <summary>
    /// Lays the trace out flat, one tab per operator beside the panels that describe the walk
    /// </summary>
    /// <remarks>
    /// The definition tree says which operators there are and what each one reads, and nothing more - an operator that reads another shows
    /// that operator's results in the pane it reads them from, while the operator itself is a tab of its own. Nesting the layout instead
    /// buries the inner operators, and the deeper the tree the less of either is left to see.
    /// </remarks>
    private DockLayoutViewModel BuildDock()
    {
        var dock = new DockLayoutViewModel(BuildRoot());

        dock.DocumentActivated += OnDocumentActivated;

        return dock;
    }

    private SplitNode BuildRoot()
    {
        _stepsDocument ??= DocumentViewModel.Create<TraceStepsPanelView>("Trace", this, canClose: false, keepAlive: true, key: "Steps");
        _descriptionDocument ??= DocumentViewModel.Create<TraceDescriptionPanelView>("Description", this, keepAlive: true, key: "Description");
        _planDocument ??= DocumentViewModel.Create<TracePlanPanelView>("Plan", this, keepAlive: true, key: "Plan");

        _operatorDocumentsByNode ??= Operators.ToDictionary(o => o.NodeId, OperatorDocument);

        MarkSelectedDocument();

        var right = new TabGroupNode(_stepsDocument, _descriptionDocument, _planDocument);

        LayoutNode left;

        if (IsNestedLayout && BuildNestedNode(Definition) is { } nested)
        {
            _operatorGroup = null;

            left = nested;
        }
        else
        {
            var group = new TabGroupNode([.. Operators.Select(o => _operatorDocumentsByNode[o.NodeId])]);

            _operatorGroup = group;

            group.PropertyChanged += OnOperatorGroupPropertyChanged;

            left = group;
        }

        UpdateActivePlanNodes();

        return new SplitNode(Orientation.Horizontal, left, right);
    }

    partial void OnIsNestedLayoutChanged(bool value)
    {
        if (_operatorGroup is { } group)
        {
            group.PropertyChanged -= OnOperatorGroupPropertyChanged;

            _operatorGroup = null;
        }

        Dock.SetRoot(BuildRoot());
    }

    private LayoutNode? BuildNestedNode(IteratorDefinition definition)
    {
        var document = _operatorDocumentsByNode?.GetValueOrDefault(definition.NodeId);

        var children = OperatorChildren(definition).Select(BuildNestedNode)
                                                   .OfType<LayoutNode>()
                                                   .ToList();

        var childArea = Combine(children);

        LayoutNode? self = document is null ? null : new TabGroupNode(document);

        if (self is null)
        {
            return childArea;
        }

        if (childArea is null)
        {
            return self;
        }

        var isFixedHeight = OperatorsByNode.GetValueOrDefault(definition.NodeId)
            is { IsJoinLayout: false, MainPane.Kind: TracePaneKind.Empty };

        return new SplitNode(Orientation.Vertical, self, childArea)
        {
            FirstStar = 1,
            SecondStar = isFixedHeight ? 3 : 1,
            FirstPixels = definition is SelectDefinition ? 280 : null
        };
    }

    private static LayoutNode? Combine(List<LayoutNode> nodes)
    {
        if (nodes.Count == 0)
        {
            return null;
        }

        var result = nodes[^1];

        for (var index = nodes.Count - 2; index >= 0; index--)
        {
            result = new SplitNode(Orientation.Horizontal, nodes[index], result)
            {
                FirstStar = 1,
                SecondStar = nodes.Count - 1 - index
            };
        }

        return result;
    }

    private IEnumerable<IteratorDefinition> OperatorChildren(IteratorDefinition definition)
        => DefinitionTreeWalker.ChildrenOf(definition).SelectMany(ResolveChild);

    private IEnumerable<IteratorDefinition> ResolveChild(IteratorDefinition child)
        => HasDocument(child)
            ? [child]
            : DefinitionTreeWalker.ChildrenOf(child).SelectMany(ResolveChild);

    private bool HasDocument(IteratorDefinition definition)
        => _operatorDocumentsByNode?.ContainsKey(definition.NodeId) == true;

    private DocumentViewModel OperatorDocument(TraceOperatorViewModel op)
    {
        var document = DocumentViewModel.Create<TraceOperatorPanelView>(op.Title,
                                                                        op,
                                                                        canClose: false,
                                                                        keepAlive: true,
                                                                        key: $"Operator{op.NodeId}");

        if (Layout.Nodes.TryGetValue(op.NodeId, out var node))
        {
            document.Accent = Layout.Palette.For(op.NodeId, node.Colour.ToWindowsColor());
        }

        return document;
    }

    partial void OnActivePlanNodesChanged(IReadOnlyList<PlanNode> value) 
        => OnPropertyChanged(nameof(ActivePlanNode));

    private void OnOperatorGroupPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabGroupNode.SelectedDocument))
        {
            OnDocumentActivated(this, _operatorGroup?.SelectedDocument);
        }
    }

    /// <summary>
    /// Follows the tab a click landed on, which is what the description panel describes
    /// </summary>
    /// <remarks>
    /// Selection has to be tracked across every group rather than within one, because a nested layout gives each operator a group of its
    /// own and the tab already selected there is still the one being clicked.
    /// </remarks>
    private void OnDocumentActivated(object? sender, DocumentViewModel? document)
    {
        if (document?.Content is TraceOperatorViewModel operatorViewModel)
        {
            SelectedNodeId = operatorViewModel.NodeId;
        }
    }

    private void MarkSelectedDocument()
    {
        if (_operatorDocumentsByNode is not { } documents)
        {
            return;
        }

        var owner = OwningNodeId(SelectedNodeId);

        foreach (var (nodeId, document) in documents)
        {
            document.IsSelected = nodeId == owner;
        }
    }

    /// <summary>
    /// The tab an operator is shown in, which is its own where it has one and an ancestor's where it is a pane of a join
    /// </summary>
    private int OwningNodeId(int nodeId)
    {
        while (_operatorDocumentsByNode?.ContainsKey(nodeId) == false && _parentByNode.TryGetValue(nodeId, out var parent))
        {
            nodeId = parent;
        }

        return nodeId;
    }

    private void UpdateActivePlanNodes()
    {
        ActivePlanNodes = _planNodesById.TryGetValue(SelectedNodeId, out var node)
            ? [node]
            : SelectedNodeId < 0 && PlanNode is { } root ? [root] : [];
    }

    private void SelectOperator(int nodeId)
    {
        ShowDocumentFor(OwningNodeId(nodeId));

        SelectedNodeId = nodeId;
    }

    private void ShowDocumentFor(int nodeId)
    {
        if (_operatorDocumentsByNode?.GetValueOrDefault(nodeId) is not { } document)
        {
            return;
        }

        if (Dock.FindGroup(document) is { } group)
        {
            group.SelectedDocument = document;
        }
    }

    private static PlanNode? FindParent(PlanNode? root, PlanNode target)
    {
        if (root is null)
        {
            return null;
        }

        foreach (var child in root.Children)
        {
            if (ReferenceEquals(child, target))
            {
                return root;
            }

            if (FindParent(child, target) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
