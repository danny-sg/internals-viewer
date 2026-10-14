using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.UI.App.Controls.CallStack;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.CallStack;
using InternalsViewer.UI.App.ViewModels.Query.CallStack;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed partial class CallStackTreePanelView : UserControl, IDocumentCommands, ISignatureNavigator, IDisposable
{
    private const int ExpandBudget = 2000;

    private const int ExpandAllBudget = 10000;

    private const int ListedLocators = 20;

    private const string BackGlyph = "";

    private const string ForwardGlyph = "";

    public CallStackTreePanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => OnViewModelChanged();

        AddHandler(PointerPressedEvent, new PointerEventHandler(OnNavigationPointerPressed), handledEventsToo: true);

        PositionActivitySplitter();

        SearchBar.SearchTextChanged += OnSearchTextChanged;

        NodeTemplates.IsRevealed = node => RowsByNode.TryGetValue(node, out var row) && row.IsRevealed;

        Loaded += OnLoaded;
    }

    public CallStackTabViewModel? ViewModel => DataContext as CallStackTabViewModel;

    private CallTreeViewModel? CallTree { get; set; }

    private ActivityColumnLayout ActivityColumn => (ActivityColumnLayout)Resources["ActivityColumn"];

    private CallTreeHighlight TreeHighlight => (CallTreeHighlight)Resources["TreeHighlight"];

    private MenuFlyout TreeBackgroundMenu => (MenuFlyout)Resources["TreeBackgroundMenu"];

    private CallstackNodeTemplateSelector NodeTemplates => (CallstackNodeTemplateSelector)Resources["NodeTemplateSelector"];

    private Dictionary<CallTreeRow, TreeViewNode> Nodes { get; } = new();

    private Dictionary<TreeViewNode, CallTreeRow> RowsByNode { get; } = new();

    private Button? BackButton { get; set; }

    private Button? ForwardButton { get; set; }

    private ToggleButton? FocusToggle { get; set; }

    private ToggleButton? ActivityToggle { get; set; }

    private ToggleButton? SignatureToggle { get; set; }

    private TreeViewNode? ContextNode { get; set; }

    private bool IsLayoutPending { get; set; }

    /// <summary>
    /// Builds the history and focus controls for a tab strip to host
    /// </summary>
    /// <remarks>
    /// Rebuilt per strip rather than moved between them, so the view keeps hold of the current buttons to drive their
    /// enabled state as the history moves.
    /// </remarks>
    public FrameworkElement CreateCommands()
    {
        BackButton = new Button
        {
            Style = (Style)Application.Current.Resources["TabCommandButtonStyle"],
            Content = new FontIcon { Glyph = BackGlyph, FontSize = 12 },
            IsEnabled = CallTree?.CanGoBack == true
        };

        ToolTipService.SetToolTip(BackButton, "Back");

        BackButton.Click += (_, _) => CallTree?.GoBack();

        ForwardButton = new Button
        {
            Style = (Style)Application.Current.Resources["TabCommandButtonStyle"],
            Content = new FontIcon { Glyph = ForwardGlyph, FontSize = 12 },
            IsEnabled = CallTree?.CanGoForward == true
        };

        ToolTipService.SetToolTip(ForwardButton, "Forward");

        ForwardButton.Click += (_, _) => CallTree?.GoForward();

        FocusToggle = Toggle("Focus", CallTree?.IsFocused ?? true);

        ToolTipService.SetToolTip(FocusToggle,
                                  "Show the selection's own call tree on its own: an operator from the frame where it "
                                  + "starts executing down to the ones it hands off to, an event from where its work "
                                  + "begins. With nothing selected, the plan's operators. Off shows every stack the "
                                  + "query captured merged into one tree.");

        FocusToggle.Click += (_, _) => CallTree?.SetFocused(FocusToggle.IsChecked == true);

        ActivityToggle = Toggle("Activity", CallTree?.IsActivityShown ?? true);

        ToolTipService.SetToolTip(ActivityToggle,
                                  "Show each frame's activity over the query window as a histogram: taller is hotter, "
                                  + "with the selection's time highlighted.");

        ActivityToggle.Click += (_, _) => CallTree?.SetActivityShown(ActivityToggle.IsChecked == true);

        SignatureToggle = Toggle("Signature", CallTree?.IsSignatureShown ?? false);

        ToolTipService.SetToolTip(SignatureToggle, "Show each frame's function signature, with its parameter types, from the symbols.");

        SignatureToggle.Click += OnSignatureClick;

        var commands = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Spacing = 2
        };

        commands.Children.Add(BackButton);
        commands.Children.Add(ForwardButton);
        commands.Children.Add(FocusToggle);
        commands.Children.Add(ActivityToggle);
        commands.Children.Add(SignatureToggle);

        return commands;
    }

    public void OnTypeInvoked(string typeName) => ViewModel?.ListMembers(typeName);

    public void Dispose()
    {
        SearchBar.SearchTextChanged -= OnSearchTextChanged;

        Detach();
    }

    private static ToggleButton Toggle(string text, bool isChecked) => new()
    {
        Style = (Style)Application.Current.Resources["TabCommandToggleStyle"],
        Content = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center },
        Margin = new Thickness(2, 0, 0, 0),
        IsChecked = isChecked
    };

    private static bool IsShiftDown()
        => InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

    private static void Collapse(TreeViewNode node)
    {
        var pending = new Stack<TreeViewNode>();

        pending.Push(node);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            if (current.IsExpanded)
            {
                current.IsExpanded = false;
            }

            foreach (var child in current.Children)
            {
                pending.Push(child);
            }
        }
    }

    private void OnViewModelChanged()
    {
        Detach();

        CallTree = ViewModel?.Tree;

        if (CallTree is { } callTree)
        {
            callTree.PropertyChanged += OnCallTreePropertyChanged;
            callTree.RowSelectionRequested += OnRowSelectionRequested;
            callTree.SearchRequested += OnSearchRequested;

            TreeHighlight.Text = callTree.Highlight;

            NodeTemplates.ShowSignatures = callTree.IsSignatureShown;
        }

        Bindings.Update();

        ApplyLayout();
    }

    private void Detach()
    {
        if (CallTree is not { } callTree)
        {
            return;
        }

        callTree.PropertyChanged -= OnCallTreePropertyChanged;
        callTree.RowSelectionRequested -= OnRowSelectionRequested;
        callTree.SearchRequested -= OnSearchRequested;

        CallTree = null;
    }

    private void OnCallTreePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (CallTree is not { } callTree)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(CallTreeViewModel.Layout):
                ApplyLayout();
                break;

            case nameof(CallTreeViewModel.CanGoBack):
                BackButton?.IsEnabled = callTree.CanGoBack;
                break;

            case nameof(CallTreeViewModel.CanGoForward):
                ForwardButton?.IsEnabled = callTree.CanGoForward;
                break;

            case nameof(CallTreeViewModel.IsFocused):
                FocusToggle?.IsChecked = callTree.IsFocused;
                break;

            case nameof(CallTreeViewModel.IsActivityShown):
            {
                Tree.ItemContainerStyle = (Style)Resources[callTree.IsActivityShown ? "ActivityItemStyle" : "PlainItemStyle"];

                ActivityHeader.Visibility = callTree.IsActivityShown ? Visibility.Visible : Visibility.Collapsed;

                break;
            }

            case nameof(CallTreeViewModel.IsSignatureShown):
                NodeTemplates.ShowSignatures = callTree.IsSignatureShown;
                break;

            case nameof(CallTreeViewModel.Highlight):
                TreeHighlight.Text = callTree.Highlight;
                break;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (IsLayoutPending)
        {
            ApplyLayout();
        }
    }

    private void ApplyLayout()
    {
        if (!IsLoaded)
        {
            IsLayoutPending = true;

            return;
        }

        IsLayoutPending = false;

        var layout = CallTree?.Layout ?? CallTreeLayout.Empty;

        // Drop the selection BEFORE the nodes it points into: TreeView holds SelectedNode itself, so clearing RootNodes
        // while it still references a node from the previous tree leaves the control unable to show the new one — which is
        // why the first query renders (nothing selected yet) and every one after it does not.
        Tree.SelectedNode = null;

        Nodes.Clear();

        RowsByNode.Clear();

        Tree.RootNodes.Clear();

        foreach (var root in layout.Roots)
        {
            Tree.RootNodes.Add(Realize(root));
        }

        foreach (var row in layout.Expanded)
        {
            Expand(row);
        }

        if (layout.ExpandRoots)
        {
            ExpandWithin(layout.Roots, ExpandBudget);
        }

        if (layout.Selected is { } selected)
        {
            Select(selected);
        }

        ShowFocusHeader(layout.FocusParent, layout.FocusCurrent);

        ShowFilterHeader(layout.FilterCurrent);
    }

    private void ShowFocusHeader(OperatorLink? parent, object? current)
    {
        HeaderParent.Content = parent is null ? null : new TreeViewNode { Content = parent };

        HeaderParent.Visibility = parent is null ? Visibility.Collapsed : Visibility.Visible;

        HeaderCurrent.Content = current is null ? null : new TreeViewNode { Content = current };

        FocusHeader.Visibility = current is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowFilterHeader(object? current)
    {
        FilterCurrent.Content = current is null ? null : new TreeViewNode { Content = current };

        FilterHeader.Visibility = current is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private TreeViewNode Realize(CallTreeRow row)
    {
        if (Nodes.TryGetValue(row, out var existing))
        {
            return existing;
        }

        var node = new TreeViewNode { Content = row.Content, HasUnrealizedChildren = row.Children.Count > 0 };

        Nodes[row] = node;

        RowsByNode[node] = row;

        return node;
    }

    private void RealizeChildren(CallTreeRow row)
    {
        if (!Nodes.TryGetValue(row, out var node) || !node.HasUnrealizedChildren)
        {
            return;
        }

        foreach (var child in row.Children)
        {
            node.Children.Add(Realize(child));
        }

        node.HasUnrealizedChildren = false;
    }

    private void Expand(CallTreeRow row)
    {
        if (row.Parent is { } parent)
        {
            Expand(parent);
        }

        RealizeChildren(row);

        if (Nodes.TryGetValue(row, out var node))
        {
            node.IsExpanded = true;
        }
    }

    private void ExpandWithin(IEnumerable<CallTreeRow> rows, int budget)
    {
        var pending = new Queue<CallTreeRow>(rows);

        var shown = 0;

        while (pending.Count > 0 && shown < budget)
        {
            var row = pending.Dequeue();

            if (row.Children.Count == 0)
            {
                continue;
            }

            Expand(row);

            shown += row.Children.Count;

            foreach (var child in row.Children)
            {
                pending.Enqueue(child);
            }
        }
    }

    private void Select(CallTreeRow row)
    {
        if (row.Parent is { } parent)
        {
            Expand(parent);
        }

        if (!Nodes.TryGetValue(row, out var node))
        {
            return;
        }

        Tree.SelectedNode = node;

        DispatcherQueue.TryEnqueue(() =>
        {
            if (Tree.ContainerFromNode(node) is FrameworkElement container)
            {
                container.StartBringIntoView();
            }
        });
    }

    private async void OnSignatureClick(object sender, RoutedEventArgs e)
    {
        if (CallTree is { } callTree)
        {
            await callTree.SetSignatureShownAsync(SignatureToggle?.IsChecked == true);
        }
    }

    private void OnRowSelectionRequested(CallTreeRow row) => Select(row);

    private void OnSearchRequested(string text) => SearchBar.Text = text;

    private void OnSearchTextChanged(string text) => CallTree?.SetSearch(text);

    private void OnTreeExpanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        if (RowsByNode.TryGetValue(args.Node, out var row))
        {
            RealizeChildren(row);
        }
    }

    private void OnFrameInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode { Content: { } content })
        {
            CallTree?.Invoke(content);
        }
    }

    private void OnHeaderParentTapped(object sender, TappedRoutedEventArgs e) => CallTree?.FollowParent();

    private void OnNavigationPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;

        if (properties.IsXButton1Pressed)
        {
            CallTree?.GoBack();

            e.Handled = true;
        }
        else if (properties.IsXButton2Pressed)
        {
            CallTree?.GoForward();

            e.Handled = true;
        }
    }

    private void OnActivitySplitterDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        ActivityColumn.Width = Math.Clamp(ActivityColumn.Width + e.Delta.Translation.X,
                                          ActivityColumnLayout.MinimumWidth,
                                          ActivityColumnLayout.MaximumWidth);

        PositionActivitySplitter();
    }

    private void PositionActivitySplitter()
        => ActivitySplitter.Margin = new Thickness(ActivityColumn.Width + 7, 0, 0, 0);

    private void OnNodeRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        ContextNode = (sender as FrameworkElement)?.DataContext as TreeViewNode;

        if ((sender as FrameworkElement)?.ContextFlyout is not MenuFlyout flyout)
        {
            return;
        }

        foreach (var item in flyout.Items.OfType<MenuFlyoutItem>())
        {
            switch (item.Tag)
            {
                case "ClearFilter":
                {
                    item.IsEnabled = CallTree?.Layout.FilterCurrent is not null;

                    break;
                }
                case "ShowArguments":
                {
                    item.IsEnabled = ContextNode?.Content is CallStackNode node && CallTree?.CanShowArguments(node) == true;

                    break;
                }
            }
        }
    }

    private void OnTreeContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (LayoutHelpers.FindParent<TreeViewItem>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        e.Handled = true;

        var options = new FlyoutShowOptions();

        if (e.TryGetPosition(Tree, out var position))
        {
            options.Position = position;
        }

        TreeBackgroundMenu.ShowAt(Tree, options);
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        SearchBar.Clear();

        CallTree?.Reset();
    }

    private void OnFilterToClick(object sender, RoutedEventArgs e)
    {
        if (ContextNode?.Content is CallStackNode node)
        {
            CallTree?.FilterTo(node);
        }
    }

    private void OnClearFilterClick(object sender, RoutedEventArgs e) => CallTree?.ClearFilter();

    private void OnShowArgumentsClick(object sender, RoutedEventArgs e)
    {
        if (ContextNode?.Content is CallStackNode node)
        {
            CallTree?.ShowArguments(node);
        }
    }

    private void OnListMembersClick(object sender, RoutedEventArgs e)
    {
        if (ContextNode?.Content is CallStackNode node)
        {
            CallTree?.ListMembers(node);
        }
    }

    private void OnExpandAllClick(object sender, RoutedEventArgs e)
    {
        if (ContextNode is not null && RowsByNode.TryGetValue(ContextNode, out var row))
        {
            ExpandWithin([row], ExpandAllBudget);
        }
    }

    private void OnCollapseAllClick(object sender, RoutedEventArgs e)
    {
        if (ContextNode is not null)
        {
            Collapse(ContextNode);
        }
    }

    private void OnCopyCallTreeClick(object sender, RoutedEventArgs e)
    {
        if (ContextNode?.Content is CallStackNode node)
        {
            ClipboardText.Copy(CallStackTree.Render(node));
        }
    }

    private void OnCopySymbolClick(object sender, RoutedEventArgs e)
    {
        if (ContextNode?.Content is CallStackNode { Frame: { } frame })
        {
            ClipboardText.Copy(QualifiedSymbol.Of(frame));
        }
    }

    private void OnNodeLocatorsClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TreeViewNode { Content: CallStackNode { Locators: { } locators } } } element)
        {
            return;
        }

        if (locators.Count == 1)
        {
            ViewModel?.OpenLocator(locators.Ordered().First(), IsShiftDown());

            return;
        }

        var menu = new MenuFlyout();

        foreach (var locator in locators.Ordered().Take(ListedLocators))
        {
            var item = new MenuFlyoutItem { Text = locator.ToString(), Tag = locator };

            item.Click += OnListedLocatorClick;

            menu.Items.Add(item);
        }

        if (locators.Count > ListedLocators)
        {
            menu.Items.Add(new MenuFlyoutItem { Text = $"And {locators.Count - ListedLocators:N0} More", IsEnabled = false });
        }

        menu.ShowAt(element);
    }

    private void OnListedLocatorClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: Locator locator })
        {
            ViewModel?.OpenLocator(locator, IsShiftDown());
        }
    }
}
