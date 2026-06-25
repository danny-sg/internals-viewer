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
    private readonly Dictionary<SplitNode, (Grid Grid, bool Horizontal)> grids = new();

    public DockHost()
    {
        InitializeComponent();
    }

    public DockLayoutViewModel? Layout
    {
        get => (DockLayoutViewModel?)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    public static readonly DependencyProperty LayoutProperty =
        DependencyProperty.Register(nameof(Layout), typeof(DockLayoutViewModel), typeof(DockHost),
            new PropertyMetadata(null, OnLayoutPropertyChanged));

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

    // Defer so we never mutate the visual tree from inside a drop handler that lives within it.
    private void OnLayoutChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(Rebuild);

    private void Rebuild()
    {
        if (Layout is null)
        {
            RootHost.Child = null;
            return;
        }

        CaptureSizes();
        grids.Clear();
        RootHost.Child = BuildNode(Layout.Root);
    }

    private FrameworkElement BuildNode(LayoutNode node)
    {
        if (node is TabGroupNode group)
        {
            var view = new TabGroupView();
            view.Initialize(group, Layout!);
            return view;
        }

        var split = (SplitNode)node;
        var horizontal = split.Orientation == Orientation.Horizontal;

        var grid = new Grid();
        var first = BuildNode(split.First);
        var second = BuildNode(split.Second);

        var splitter = new GridSplitter
        {
            Background = SplitterBrush,
            ResizeBehavior = GridSplitter.GridResizeBehavior.PreviousAndNext
        };

        if (horizontal)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(split.FirstStar, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(split.SecondStar, GridUnitType.Star) });

            Grid.SetColumn(first, 0);
            Grid.SetColumn(splitter, 1);
            Grid.SetColumn(second, 2);

            splitter.Width = 6;
            splitter.ResizeDirection = GridSplitter.GridResizeDirection.Columns;
            splitter.HorizontalAlignment = HorizontalAlignment.Center;
            splitter.VerticalAlignment = VerticalAlignment.Stretch;
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(split.FirstStar, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(split.SecondStar, GridUnitType.Star) });

            Grid.SetRow(first, 0);
            Grid.SetRow(splitter, 1);
            Grid.SetRow(second, 2);

            splitter.Height = 6;
            splitter.ResizeDirection = GridSplitter.GridResizeDirection.Rows;
            splitter.VerticalAlignment = VerticalAlignment.Center;
            splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        grid.Children.Add(first);
        grid.Children.Add(splitter);
        grid.Children.Add(second);

        grids[split] = (grid, horizontal);

        return grid;
    }

    /// <summary>Writes the live splitter star sizes back into the model (e.g. before persisting the layout).</summary>
    public void CaptureSizes()
    {
        foreach (var (node, (grid, horizontal)) in grids)
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

    private static Brush SplitterBrush =>
        Application.Current.Resources["ControlStrokeColorDefaultBrush"] as Brush
        ?? new SolidColorBrush(Microsoft.UI.Colors.Gray);
}
