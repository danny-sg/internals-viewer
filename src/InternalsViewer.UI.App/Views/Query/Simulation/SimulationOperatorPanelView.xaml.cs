using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Models.Query.Simulation;
using InternalsViewer.UI.App.ViewModels.Query.Simulation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace InternalsViewer.UI.App.Views.Query.Simulation;

/// <summary>
/// One operator, each of its two inputs above the rows that input holds
/// </summary>
/// <remarks>
/// An operator is a single document, so the panes are laid out here rather than as dock nodes - they belong to the operator and cannot be
/// moved or closed on their own. An input that is another operator shows that operator's results alone, its own inputs being a tab of
/// their own.
/// </remarks>
public sealed partial class SimulationOperatorPanelView : UserControl, IDocumentCommands
{
    private bool _isOutputVisible = true;

    private bool _isHashTableVisible = true;

    private SimulationOperatorViewModel? _appliedViewModel;

    private ToggleButton? _zoomToPageToggle;

    public SimulationOperatorPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => ApplyPanes();
    }

    public SimulationOperatorViewModel? ViewModel => DataContext as SimulationOperatorViewModel;

    public FrameworkElement? CreateCommands()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Spacing = 2
        };

        if (ViewModel?.OuterBottom.Kind == SimulationPaneKind.HashTable)
        {
            var hashTableToggle = new ToggleButton
            {
                Style = (Style)Application.Current.Resources["TabCommandToggleStyle"],
                Content = new TextBlock { Text = "Hash Table", VerticalAlignment = VerticalAlignment.Center },
                IsChecked = _isHashTableVisible
            };

            hashTableToggle.Click += (_, _) =>
            {
                _isHashTableVisible = hashTableToggle.IsChecked == true;

                ApplyHashTableVisibility();
            };

            panel.Children.Add(hashTableToggle);
        }

        if (ViewModel is { HasZoomToPage: true } zoomViewModel)
        {
            var zoomToPageToggle = new ToggleButton
            {
                Style = (Style)Application.Current.Resources["TabCommandToggleStyle"],
                Content = new TextBlock { Text = "Zoom to Page", VerticalAlignment = VerticalAlignment.Center }
            };

            // The strip is rebuilt on every tab switch, so drop the binding the discarded toggle holds on the view model.
            _zoomToPageToggle?.ClearValue(ToggleButton.IsCheckedProperty);

            _zoomToPageToggle = zoomToPageToggle;

            // Bound rather than set, because a nested layout shows a strip per operator and the setting is the whole trace's.
            zoomToPageToggle.SetBinding(ToggleButton.IsCheckedProperty,
                                        new Binding
                                        {
                                            Source = zoomViewModel,
                                            Path = new PropertyPath(nameof(SimulationOperatorViewModel.IsZoomToPage)),
                                            Mode = BindingMode.TwoWay
                                        });

            panel.Children.Add(zoomToPageToggle);
        }

        if (ViewModel is { HasOutputPane: true, IsBatchMode: false })
        {
            var outputToggle = new ToggleButton
            {
                Style = (Style)Application.Current.Resources["TabCommandToggleStyle"],
                Content = new TextBlock { Text = "Output", VerticalAlignment = VerticalAlignment.Center },
                IsChecked = _isOutputVisible
            };

            outputToggle.Click += (_, _) =>
            {
                _isOutputVisible = outputToggle.IsChecked == true;

                ApplyOutputVisibility();
            };

            panel.Children.Add(outputToggle);
        }

        return panel;
    }

    private void ApplyOutputVisibility()
    {
        var visibility = _isOutputVisible && _appliedViewModel?.HasOutputPane != false
            ? Visibility.Visible
            : Visibility.Collapsed;

        OutputHeader.Visibility = visibility;
        OutputHost.Visibility = visibility;
    }

    private void ApplyHashTableVisibility()
    {
        if (_appliedViewModel?.OuterBottom.Kind != SimulationPaneKind.HashTable)
        {
            return;
        }

        var isVisible = _isHashTableVisible;

        OuterBottomRow.Height = isVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        OuterSplitter.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Builds the panes once for an operator, leaving the sizes its splitters have since been dragged to
    /// </summary>
    /// <remarks>
    /// The view is kept alive and rehosted each time tabs are switched, which raises DataContextChanged again. Building again on that would
    /// put fresh views in the panes, losing the index map each had loaded, and return every row and column to the share of the space it
    /// started with.
    /// </remarks>
    private void ApplyPanes()
    {
        if (ViewModel is not { } viewModel || ReferenceEquals(viewModel, _appliedViewModel))
        {
            return;
        }

        _appliedViewModel = viewModel;

        _isOutputVisible = viewModel.IsOutputDefaultVisible;

        OperatorIcon.Source = viewModel.Icon is { } operatorIcon ? new SvgImageSource(operatorIcon) : null;
        OperatorIcon.Visibility = viewModel.Icon is null ? Visibility.Collapsed : Visibility.Visible;

        OperatorName.Text = viewModel.Heading.Length > 0 ? viewModel.Heading : viewModel.Title;

        OperatorBadge.Decision = viewModel.JoinRule;
        OperatorBadge.Visibility = viewModel.JoinRule is null ? Visibility.Collapsed : Visibility.Visible;

        OutputHeader.Text = $"Output ({viewModel.NodeId})";

        OutputHost.Content = new SimulationRowStreamPanelView { DataContext = viewModel.Output };

        if (viewModel.IsJoinLayout)
        {
            JoinGrid.Visibility = Visibility.Visible;
            MainHost.Visibility = Visibility.Collapsed;
            InputsList.Visibility = Visibility.Collapsed;

            StateList.ItemsSource = viewModel.StateItems;
            StateArea.Visibility = viewModel.StateItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            Fill(OuterTopHost, OuterHeader, viewModel.OuterTop);
            Fill(InnerTopHost, InnerHeader, viewModel.InnerTop);

            Fill(OuterBottomHost, header: null, viewModel.OuterBottom);
            Fill(InnerBottomHost, header: null, viewModel.InnerBottom);

            Collapse(OuterBottomRow, OuterSplitter, viewModel.OuterBottom);
            Collapse(InnerBottomRow, InnerSplitter, viewModel.InnerBottom);

            var hasInner = viewModel.InnerTop.Kind != SimulationPaneKind.Empty;

            InnerColumn.Width = hasInner ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            InputSplitter.Visibility = hasInner ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            JoinGrid.Visibility = Visibility.Collapsed;

            InputsList.ItemsSource = viewModel.InputRows;
            InputsList.Visibility = viewModel.InputRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            StateList.ItemsSource = viewModel.StateItems;
            StateArea.Visibility = viewModel.StateItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            MainHost.Visibility = viewModel.MainPane.Kind == SimulationPaneKind.Empty ? Visibility.Collapsed : Visibility.Visible;

            Fill(MainHost, header: null, viewModel.MainPane);
        }

        var isFixedHeight = !viewModel.IsJoinLayout && viewModel.MainPane.Kind == SimulationPaneKind.Empty;

        PanelScroller.VerticalScrollMode = isFixedHeight ? ScrollMode.Auto : ScrollMode.Disabled;
        PanelScroller.VerticalScrollBarVisibility = isFixedHeight ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;

        ApplyOutputVisibility();
        ApplyHashTableVisibility();
    }

    private void OnCurrentPageClicked(object sender, RoutedEventArgs e)
    {
        _appliedViewModel?.RequestPageOpen();
    }

    private void OnOperatorBarTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (_appliedViewModel is { } viewModel)
        {
            viewModel.RequestActivation(viewModel.NodeId);
        }
    }

    private void OnInputRowTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SimulationInputRow row })
        {
            _appliedViewModel?.RequestActivation(row.SourceNodeId);
        }
    }

    private void Fill(ContentControl host, StackPanel? header, SimulationPane pane)
    {
        host.Content = CreateView(pane);

        if (header is null)
        {
            return;
        }

        header.Children.Clear();

        header.Tag = pane;

        header.Tapped -= OnHeaderTapped;
        header.Tapped += OnHeaderTapped;

        if (pane.Title.Length == 0 && pane.Heading.Length == 0)
        {
            header.Visibility = Visibility.Collapsed;

            return;
        }

        header.Visibility = Visibility.Visible;

        if (pane.Title.Length > 0)
        {
            header.Children.Add(HeaderText(pane.Title, isSecondary: true));
        }

        if (pane.AccentColour is { } accent)
        {
            var blob = pane.SourceNodeId is { } sourceNodeId && _appliedViewModel?.BlobPalette is { } palette
                ? palette.For(sourceNodeId, accent)
                : (Brush)new SolidColorBrush(accent);

            header.Children.Add(new Border
            {
                Width = 9,
                Height = 9,
                CornerRadius = new CornerRadius(2),
                VerticalAlignment = VerticalAlignment.Center,
                Background = blob
            });
        }

        if (pane.Icon is { } icon)
        {
            header.Children.Add(new Image
            {
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
                Source = new SvgImageSource(icon)
            });
        }

        if (pane.Heading.Length > 0)
        {
            header.Children.Add(HeaderText(pane.Heading, isSecondary: false));
        }

        if (pane.Subheading.Length > 0)
        {
            header.Children.Add(HeaderText(pane.Subheading, isSecondary: true));
        }
    }

    private void OnHeaderTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is StackPanel { Tag: SimulationPane { SourceNodeId: { } sourceNodeId } })
        {
            _appliedViewModel?.RequestActivation(sourceNodeId);
        }
    }

    private static TextBlock HeaderText(string text, bool isSecondary)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        if (isSecondary)
        {
            block.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        }
        else
        {
            block.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        }

        return block;
    }

    private static void Collapse(RowDefinition row, FrameworkElement splitter, SimulationPane pane)
    {
        var isVisible = pane.Kind != SimulationPaneKind.Empty;

        row.Height = isVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        splitter.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private static FrameworkElement? CreateView(SimulationPane pane)
    {
        FrameworkElement? view = pane.Kind switch
        {
            SimulationPaneKind.Visual 
                => new SimulationVisualPanelView(),
            SimulationPaneKind.RowStream 
                => new SimulationRowStreamPanelView(),
            SimulationPaneKind.HeldRows 
                => new SimulationHeldRowsPanelView(),
            SimulationPaneKind.HashTable 
                => new SimulationHashTablePanelView(),
            SimulationPaneKind.HashTables
                => new SimulationHashTablePairView(),
            SimulationPaneKind.Aggregates 
                => new SimulationAggregatePanelView(),
            SimulationPaneKind.Segment 
                => new SimulationSegmentPanelView(),
            _ => null
        };

        view?.DataContext = pane.Content;

        return view;
    }
}
