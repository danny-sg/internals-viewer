using System;
using System.Collections.Generic;
using CommunityToolkit.WinUI.Controls;
using InternalsViewer.UI.App.ViewModels.Docking;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.Docking;

/// <summary>
/// Hosts a <see cref="DockLayoutViewModel"/> and renders its layout tree, rebuilding the visual
/// tree whenever the structure changes. Splitter positions are preserved across rebuilds by
/// capturing the current star sizes back into the model before each rebuild.
/// </summary>
public sealed partial class DockHost : UserControl
{
    private const double SplitterSize = 4;

    public static readonly DependencyProperty LayoutProperty =
        DependencyProperty.Register(nameof(Layout), typeof(DockLayoutViewModel), typeof(DockHost),
            new PropertyMetadata(null, OnLayoutPropertyChanged));

    public DockLayoutViewModel? Layout
    {
        get => (DockLayoutViewModel?)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    public static readonly DependencyProperty FixedContentProperty =
        DependencyProperty.Register(nameof(FixedContent), typeof(FrameworkElement), typeof(DockHost),
            new PropertyMetadata(null, OnFixedContentPropertyChanged));

    public FrameworkElement? FixedContent
    {
        get => (FrameworkElement?)GetValue(FixedContentProperty);
        set => SetValue(FixedContentProperty, value);
    }

    public DockHost()
    {
        InitializeComponent();
    }

    private static Brush SplitterBrush =>
        Application.Current.Resources["ControlStrokeColorDefaultBrush"] as Brush
        ?? new SolidColorBrush(Microsoft.UI.Colors.Gray);

    private FixedPaneView? FixedPane { get; set; }

    private Dictionary<SplitNode, (Grid Grid, bool Horizontal)> Grids { get; set; } = [];

    private Dictionary<TabGroupNode, TabGroupView> GroupViews { get; set; } = [];

    public void CaptureSizes()
    {
        foreach (var (node, (grid, horizontal)) in Grids)
        {
            if (horizontal && grid.ColumnDefinitions.Count == 3)
            {
                var a = grid.ColumnDefinitions[0].Width;
                var b = grid.ColumnDefinitions[2].Width;

                if (a.IsStar)
                {
                    node.FirstStar = a.Value;
                }

                if (b.IsStar)
                {
                    node.SecondStar = b.Value;
                }
            }
            else if (!horizontal && grid.RowDefinitions.Count == 3)
            {
                var a = grid.RowDefinitions[0].Height;
                var b = grid.RowDefinitions[2].Height;

                if (a.IsStar)
                {
                    node.FirstStar = a.Value;
                }

                if (b.IsStar)
                {
                    node.SecondStar = b.Value;
                }
            }
        }
    }

    // Defer so we never mutate the visual tree from inside a drop handler that lives within it.
    private void OnLayoutChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(Rebuild);

    private void Rebuild()
    {
        CaptureSizes();

        var grids = Grids;

        var groupViews = GroupViews;

        Grids = [];

        GroupViews = [];

        var root = Layout is null ? null : BuildNode(Layout.Root, grids, groupViews);

        foreach (var view in groupViews.Values)
        {
            view.Release();
        }

        if (ReferenceEquals(RootHost.Child, root))
        {
            return;
        }

        if (root is not null)
        {
            Detach(root);
        }

        RootHost.Child = root;
    }

    private FrameworkElement BuildNode(LayoutNode node,
                                       Dictionary<SplitNode, (Grid Grid, bool Horizontal)> grids,
                                       Dictionary<TabGroupNode, TabGroupView> groupViews)
    {
        if (node is TabGroupNode group)
        {
            if (!groupViews.Remove(group, out var view))
            {
                view = new TabGroupView();

                view.Initialize(group, Layout!);
            }

            GroupViews[group] = view;

            return view;
        }

        if (node is FixedNode fixedNode)
        {
            FixedPane ??= new FixedPaneView();

            fixedNode.MinWidth = FixedContent?.MinWidth ?? 0;

            FixedPane.Initialize(fixedNode, Layout!, FixedContent);

            return FixedPane;
        }

        var split = (SplitNode)node;

        var horizontal = split.Orientation == Orientation.Horizontal;

        var first = BuildNode(split.First, grids, groupViews);

        var second = BuildNode(split.Second, grids, groupViews);

        var grid = grids.TryGetValue(split, out var built) && built.Horizontal == horizontal
            ? built.Grid
            : CreateGrid(split, horizontal);

        PlaceChildren(grid, first, second, horizontal);

        if (horizontal)
        {
            grid.ColumnDefinitions[0].MinWidth = split.First.GetMinWidth(SplitterSize);

            grid.ColumnDefinitions[2].MinWidth = split.Second.GetMinWidth(SplitterSize);
        }

        Grids[split] = (grid, horizontal);

        return grid;
    }

    private static Grid CreateGrid(SplitNode split, bool horizontal)
    {
        var grid = new Grid();

        var splitter = new GridSplitter
        {
            Background = SplitterBrush,
            ResizeBehavior = GridSplitter.GridResizeBehavior.PreviousAndNext
        };

        var firstLength = split.FirstPixels is { } pixels
            ? new GridLength(pixels)
            : new GridLength(split.FirstStar, GridUnitType.Star);

        var secondLength = new GridLength(split.SecondStar, GridUnitType.Star);

        if (horizontal)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = firstLength });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = secondLength });

            Grid.SetColumn(splitter, 1);

            splitter.Width = SplitterSize;
            splitter.ResizeDirection = GridSplitter.GridResizeDirection.Columns;
            splitter.HorizontalAlignment = HorizontalAlignment.Center;
            splitter.VerticalAlignment = VerticalAlignment.Stretch;
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = firstLength });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = secondLength });

            Grid.SetRow(splitter, 1);

            splitter.Height = SplitterSize;
            splitter.ResizeDirection = GridSplitter.GridResizeDirection.Rows;
            splitter.VerticalAlignment = VerticalAlignment.Center;
            splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        grid.Children.Add(splitter);

        return grid;
    }

    private static void PlaceChildren(Grid grid, FrameworkElement first, FrameworkElement second, bool horizontal)
    {
        for (var index = grid.Children.Count - 1; index >= 0; index--)
        {
            var child = grid.Children[index];

            if (child is not GridSplitter && !ReferenceEquals(child, first) && !ReferenceEquals(child, second))
            {
                grid.Children.RemoveAt(index);
            }
        }

        Place(grid, first, 0, horizontal);

        Place(grid, second, 2, horizontal);
    }

    private static void Place(Grid grid, FrameworkElement element, int cell, bool horizontal)
    {
        if (!ReferenceEquals(element.Parent, grid))
        {
            Detach(element);

            grid.Children.Add(element);
        }

        if (horizontal)
        {
            Grid.SetColumn(element, cell);
        }
        else
        {
            Grid.SetRow(element, cell);
        }
    }

    private static void Detach(FrameworkElement element)
    {
        if (element.Parent is Panel panel)
        {
            panel.Children.Remove(element);
        }
        else if (element.Parent is Border border)
        {
            border.Child = null;
        }
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var host = (DockHost)d;

        if (e.OldValue is DockLayoutViewModel oldLayout)
        {
            oldLayout.LayoutChanged -= host.OnLayoutChanged;
        }

        if (e.NewValue is DockLayoutViewModel newLayout)
        {
            newLayout.LayoutChanged += host.OnLayoutChanged;
        }

        host.Rebuild();
    }

    private static void OnFixedContentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((DockHost)d).Rebuild();
}
