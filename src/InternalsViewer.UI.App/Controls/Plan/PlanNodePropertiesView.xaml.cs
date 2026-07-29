using InternalsViewer.Query.Parsing.Plans;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Plan;

public sealed partial class PlanNodePropertiesView : UserControl
{
    public static readonly DependencyProperty NodeProperty =
        DependencyProperty.Register(nameof(Node),
                                    typeof(PlanNode),
                                    typeof(PlanNodePropertiesView),
                                    new PropertyMetadata(null, OnNodeChanged));

    public PlanNode? Node
    {
        get => (PlanNode?)GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    public PlanNodePropertiesView()
    {
        InitializeComponent();
    }

    private static void OnNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((PlanNodePropertiesView)d).Rebuild();
    }

    private void Rebuild()
    {
        TreeView.RootNodes.Clear();

        PlaceholderText.Visibility = Node is null ? Visibility.Visible : Visibility.Collapsed;

        if (Node is null)
        {
            return;
        }

        foreach (var property in PlanNodePropertyBuilder.Build(Node))
        {
            TreeView.RootNodes.Add(ToTreeNode(property, 0));
        }
    }

    private static TreeViewNode ToTreeNode(PlanNodeProperty property, int depth)
    {
        var node = new TreeViewNode
        {
            Content = property with { Depth = depth },
            IsExpanded = property.IsExpanded
        };

        foreach (var child in property.Children)
        {
            node.Children.Add(ToTreeNode(child, depth + 1));
        }

        return node;
    }
}
