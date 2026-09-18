using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.Events.Latches;
using InternalsViewer.Query.Events.Locks;
using InternalsViewer.Query.Events.Operators;
using InternalsViewer.Query.Events;
using InternalsViewer.Query.Interfaces.Events;
using InternalsViewer.Query.Plans.Model;
using InternalsViewer.UI.App.Controls.CallStack;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Models.Query.CallStack;
using InternalsViewer.UI.App.Services.Query.Debugging;
using InternalsViewer.UI.App.ViewModels.Query;
using InternalsViewer.UI.App.ViewModels.Query.CallStack;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed partial class QueryCallStackTabView : UserControl, IDocumentCommands, ISignatureNavigator
{
    private readonly Dictionary<CallStackNode, TreeViewNode> _nodes = new();

    private readonly List<EngineEvent> _history = [];

    private readonly List<TreeViewNode> _operatorNodes = [];

    private Button? _backButton;

    private Button? _forwardButton;

    private ToggleButton? _focusToggle;

    private ToggleButton? _activityToggle;

    private ToggleButton? _symbolsToggle;

    private HashSet<CallStackNode>? _visible;

    private bool _revealInfrastructure;

    private bool _focus = true;

    private bool _activity = true;

    private HashSet<int> _highlightBuckets = [];

    private bool _isSelectingFromTree;

    private int _historyIndex = -1;

    private bool _navigatingHistory;

    private string _search = string.Empty;

    private Dictionary<CallStackNode, ExecutionOperatorEvent> _nextOperator = new();

    private OperatorHierarchy _hierarchy = OperatorHierarchy.Build([]);

    private TreeViewNode? _contextNode;

    private ClassMemberRow? _contextMember;

    private SymbolModuleRow? _contextModule;

    private QueryViewModel? _viewModel;

    public QueryCallStackTabView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => OnViewModelChanged();

        PositionActivitySplitter();
    }

    private ActivityColumnLayout ActivityColumn => (ActivityColumnLayout)Resources["ActivityColumn"];

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private Dictionary<CallStackNode, bool> HiddenOnly { get; } = new();

    public bool IsMembersPaneVisible
    {
        get => ViewModel?.Symbols.IsPaneVisible == true;
        set
        {
            if (ViewModel is { } viewModel)
            {
                viewModel.Symbols.IsPaneVisible = value;
            }

            RefreshDetailPane();
        }
    }

    public bool IsMembersPaneDockedBottom
    {
        get => ViewModel?.Symbols.IsPaneDockedBottom == true;
        set
        {
            if (ViewModel is { } viewModel)
            {
                viewModel.Symbols.IsPaneDockedBottom = value;
            }

            RefreshDetailPane();
        }
    }

    public GridLength BodyColumnWidth
        => IsMembersPaneVisible && !IsMembersPaneDockedBottom
            ? new GridLength(6, GridUnitType.Star)
            : new GridLength(1, GridUnitType.Star);

    public GridLength DetailColumnWidth
        => IsMembersPaneVisible && !IsMembersPaneDockedBottom ? new GridLength(4, GridUnitType.Star) : new GridLength(0);

    public GridLength BodyRowHeight
        => IsMembersPaneVisible && IsMembersPaneDockedBottom
            ? new GridLength(6, GridUnitType.Star)
            : new GridLength(1, GridUnitType.Star);

    public GridLength DetailRowHeight
        => IsMembersPaneVisible && IsMembersPaneDockedBottom ? new GridLength(4, GridUnitType.Star) : new GridLength(0);

    public Visibility DetailPaneVisibility
        => IsMembersPaneVisible ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ColumnSplitterVisibility
        => IsMembersPaneVisible && !IsMembersPaneDockedBottom ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RowSplitterVisibility
        => IsMembersPaneVisible && IsMembersPaneDockedBottom ? Visibility.Visible : Visibility.Collapsed;

    public Thickness DetailPaneBorderThickness
        => IsMembersPaneDockedBottom ? new Thickness(0, 1, 0, 0) : new Thickness(1, 0, 0, 0);

    public string DockToggleGlyph => IsMembersPaneDockedBottom ? "" : "";

    public string DockToggleTooltip => IsMembersPaneDockedBottom ? "Dock Right" : "Dock Bottom";

    private WinDbgService WinDbg => field ??= App.GetService<WinDbgService>();

    /// <summary>
    /// Builds the history and focus controls for a tab strip to host
    /// </summary>
    /// <remarks>
    /// Rebuilt per strip rather than moved between them, so the view keeps hold of the current buttons to drive their
    /// enabled state as the history moves.
    /// </remarks>
    public FrameworkElement CreateCommands()
    {
        _backButton = new Button
        {
            Style = (Style)Application.Current.Resources["TabCommandButtonStyle"],
            Content = new FontIcon { Glyph = "", FontSize = 12 },
            IsEnabled = _historyIndex > 0
        };

        ToolTipService.SetToolTip(_backButton, "Back");

        _backButton.Click += OnBackClick;

        _forwardButton = new Button
        {
            Style = (Style)Application.Current.Resources["TabCommandButtonStyle"],
            Content = new FontIcon { Glyph = "", FontSize = 12 },
            IsEnabled = _historyIndex >= 0 && _historyIndex < _history.Count - 1
        };

        ToolTipService.SetToolTip(_forwardButton, "Forward");

        _forwardButton.Click += OnForwardClick;

        _focusToggle = new ToggleButton
        {
            Style = (Style)Application.Current.Resources["TabCommandToggleStyle"],
            Content = new TextBlock { Text = "Focus", VerticalAlignment = VerticalAlignment.Center },
            Margin = new Thickness(2, 0, 0, 0),
            IsChecked = _focus
        };

        ToolTipService.SetToolTip(_focusToggle,
                                  "Show the selection's own call tree on its own: an operator from the frame where it "
                                  + "starts executing down to the ones it hands off to, an event from where its work "
                                  + "begins. With nothing selected, the plan's operators. Off shows every stack the "
                                  + "query captured merged into one tree.");

        _focusToggle.Click += OnFocusChanged;

        _activityToggle = new ToggleButton
        {
            Style = (Style)Application.Current.Resources["TabCommandToggleStyle"],
            Content = new TextBlock { Text = "Activity", VerticalAlignment = VerticalAlignment.Center },
            Margin = new Thickness(2, 0, 0, 0),
            IsChecked = _activity
        };

        ToolTipService.SetToolTip(_activityToggle,
                                  "Show each frame's activity over the query window as a histogram: taller is hotter, "
                                  + "with the selection's time highlighted.");

        _activityToggle.Click += OnActivityChanged;

        _symbolsToggle = new ToggleButton
        {
            Style = (Style)Application.Current.Resources["TabCommandToggleStyle"],
            Content = new TextBlock { Text = "Symbols", VerticalAlignment = VerticalAlignment.Center },
            Margin = new Thickness(2, 0, 0, 0),
            IsChecked = IsMembersPaneVisible
        };

        _symbolsToggle.Click += OnSymbolsToggleChanged;

        var commands = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            Spacing = 2
        };

        commands.Children.Add(_backButton);
        commands.Children.Add(_forwardButton);
        commands.Children.Add(_focusToggle);
        commands.Children.Add(_activityToggle);
        commands.Children.Add(_symbolsToggle);

        return commands;
    }

    private async void RunWinDbg(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            var dialog = new ContentDialog
            {
                Title = "WinDbg",
                Content = exception.Message,
                CloseButtonText = "OK",
                XamlRoot = XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };

            await dialog.ShowAsync();
        }
    }

    private void OnFocusChanged(object sender, RoutedEventArgs e)
    {
        _focus = _focusToggle?.IsChecked == true;

        ApplyFocus(_viewModel?.SelectedEvent);
    }

    private void OnActivityChanged(object sender, RoutedEventArgs e)
    {
        _activity = _activityToggle?.IsChecked == true;

        Tree.ItemContainerStyle = (Style)Resources[_activity ? "ActivityItemStyle" : "PlainItemStyle"];

        ActivityHeader.Visibility = _activity ? Visibility.Visible : Visibility.Collapsed;

        ApplyFocus(_viewModel?.SelectedEvent);
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

    private void OnSymbolsToggleChanged(object sender, RoutedEventArgs e)
    {
        IsMembersPaneVisible = _symbolsToggle?.IsChecked ?? false;
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => GoTo(_historyIndex - 1);

    private void OnForwardClick(object sender, RoutedEventArgs e) => GoTo(_historyIndex + 1);

    private void GoTo(int index)
    {
        if (_viewModel is null || index < 0 || index >= _history.Count)
        {
            return;
        }

        _historyIndex = index;

        _navigatingHistory = true;

        try
        {
            _viewModel.SelectedEvent = _history[index];
        }
        finally
        {
            _navigatingHistory = false;
        }

        UpdateHistoryButtons();
    }

    /// <summary>
    /// Records a selection as a place that can be returned to
    /// </summary>
    private void RecordHistory(EngineEvent? selected)
    {
        if (selected is null || _navigatingHistory)
        {
            return;
        }

        if (_historyIndex >= 0 && ReferenceEquals(_history[_historyIndex], selected))
        {
            return;
        }

        _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);

        _history.Add(selected);

        _historyIndex = _history.Count - 1;

        UpdateHistoryButtons();
    }

    private void ClearHistory()
    {
        _history.Clear();

        _historyIndex = -1;

        UpdateHistoryButtons();
    }

    private void UpdateHistoryButtons()
    {
        _backButton?.IsEnabled = _historyIndex > 0;

        _forwardButton?.IsEnabled = _historyIndex >= 0 && _historyIndex < _history.Count - 1;
    }

    /// <summary>
    /// Shows what the tree below is a segment of, and the way back up to the operator that drove it
    /// </summary>
    /// <remarks>
    /// Both rows are wrapped in a TreeViewNode because the header borrows the tree's templates, which bind to one.
    /// </remarks>
    private void ShowFocusHeader(ExecutionOperatorEvent? parent, object current)
    {
        // A plan root has nowhere above it, so the link goes rather than showing an arrow that leads nowhere.
        var hasParent = parent is not null;

        HeaderParent.Content = hasParent ? new TreeViewNode { Content = new OperatorLink(parent!, Back: true) } : null;

        HeaderParent.Visibility = hasParent ? Visibility.Visible : Visibility.Collapsed;

        HeaderCurrent.Content = new TreeViewNode { Content = current };

        FocusHeader.Visibility = Visibility.Visible;
    }

    private void HideFocusHeader()
    {
        HeaderParent.Content = null;

        HeaderCurrent.Content = null;

        FocusHeader.Visibility = Visibility.Collapsed;
    }

    // Following the header's parent link, like following one in the tree: a request to go there, so it rebuilds around
    // the parent rather than leaving the current segment on screen with the selection moved out from under it.
    private void OnHeaderParentTapped(object sender, TappedRoutedEventArgs e)
    {
        if (_viewModel is null || HeaderParent.Content is not TreeViewNode { Content: OperatorLink link })
        {
            return;
        }

        _viewModel.SelectedEvent = link.Operator;
    }

    private void OnNodeRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        _contextNode = (sender as FrameworkElement)?.DataContext as TreeViewNode;

        EnableWinDbgMenu(sender);
    }

    private void OnExpandAllClick(object sender, RoutedEventArgs e) => SetExpanded(_contextNode, expanded: true);

    private void OnCollapseAllClick(object sender, RoutedEventArgs e) => SetExpanded(_contextNode, expanded: false);

    private void OnCopyCallTreeClick(object sender, RoutedEventArgs e)
    {
        if (_contextNode?.Content is CallStackNode node)
        {
            CopyText(CallStackTree.Render(node));
        }
    }

    private void OnCopySymbolClick(object sender, RoutedEventArgs e)
    {
        if (_contextNode?.Content is CallStackNode { Frame: { } frame })
        {
            CopyText(WinDbgCommands.Symbol(frame));
        }
    }

    private void OnSendWinDbgClick(object sender, RoutedEventArgs e)
    {
        if (_contextNode?.Content is not CallStackNode { Frame: { } frame } || sender is not MenuFlyoutItem { Tag: string command })
        {
            return;
        }

        if (command is "DumpArguments" or "DumpArgumentsAndBreak")
        {
            RunWinDbg(async () => await WinDbg.SendAsync(await FrameArgumentsCommand(command, frame), CancellationToken.None));

            return;
        }

        if (FrameCommand(command, frame) is { } text)
        {
            RunWinDbg(() => WinDbg.SendAsync(text, CancellationToken.None));
        }
    }

    private async Task<string> FrameArgumentsCommand(string command, CallstackFrame frame)
    {
        var signature = _viewModel is { } viewModel ? await viewModel.Symbols.ResolveFrameSignatureAsync(frame) : null;

        return command == "DumpArgumentsAndBreak"
            ? WinDbgCommands.DumpArgumentsAndBreak(frame, signature)
            : WinDbgCommands.DumpArguments(frame, signature);
    }

    private static string? FrameCommand(string command, CallstackFrame frame) =>
        command switch
        {
            "Breakpoint" => WinDbgCommands.Breakpoint(frame),
            "BreakpointWithStack" => WinDbgCommands.BreakpointWithStack(frame),
            "BreakpointAtFrame" => WinDbgCommands.BreakpointAtFrame(frame),
            "ExamineSymbol" => WinDbgCommands.ExamineSymbol(frame),
            "DisplayType" => WinDbgCommands.DisplayType(frame),
            "ListClassSymbols" => WinDbgCommands.ListClassSymbols(frame),
            _ => null
        };

    private void OnListMembersClick(object sender, RoutedEventArgs e)
    {
        if (_contextNode?.Content is not CallStackNode node || _viewModel is null)
        {
            return;
        }

        _ = _viewModel.Symbols.ListMembersAsync(node);
    }

    private void OnMemberRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        _contextMember = (sender as FrameworkElement)?.DataContext as ClassMemberRow;

        EnableWinDbgMenu(sender);
    }

    private void EnableWinDbgMenu(object sender)
    {
        if ((sender as FrameworkElement)?.ContextFlyout is not MenuFlyout flyout)
        {
            return;
        }

        foreach (var item in flyout.Items.OfType<MenuFlyoutSubItem>().Where(i => i.Text == "WinDbg"))
        {
            item.IsEnabled = WinDbg.IsConnected;
        }
    }

    private void OnCopySignatureClick(object sender, RoutedEventArgs e)
    {
        if (_contextMember is not null)
        {
            CopyText(_contextMember.Signature);
        }
    }

    private void OnCopyMemberSymbolClick(object sender, RoutedEventArgs e)
    {
        if (_contextMember is not null)
        {
            CopyText(WinDbgCommands.Symbol(_contextMember));
        }
    }

    private void OnSendMemberWinDbgClick(object sender, RoutedEventArgs e)
    {
        if (_contextMember is { } member
            && sender is MenuFlyoutItem { Tag: string command }
            && MemberCommand(command, member) is { } text)
        {
            RunWinDbg(() => WinDbg.SendAsync(text, CancellationToken.None));
        }
    }

    private static string? MemberCommand(string command, ClassMemberRow member) =>
        command switch
        {
            "Breakpoint" => WinDbgCommands.Breakpoint(member),
            "BreakpointWithStack" => WinDbgCommands.BreakpointWithStack(member),
            "DumpArguments" => WinDbgCommands.DumpArguments(member),
            "DumpArgumentsAndBreak" => WinDbgCommands.DumpArgumentsAndBreak(member),
            "BreakpointOnAllOverloads" => WinDbgCommands.BreakpointOnAllOverloads(member),
            "ExamineSymbol" => WinDbgCommands.ExamineSymbol(member),
            "DisplayType" => WinDbgCommands.DisplayType(member),
            "ListClassSymbols" => WinDbgCommands.ListClassSymbols(member),
            _ => null
        };

    private static void CopyText(string text)
    {
        var package = new DataPackage();

        package.SetText(text);

        Clipboard.SetContent(package);
    }

    private void OnMembersBackClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _ = _viewModel.Symbols.GoBackMembersAsync();
        }
    }

    public void OnTypeInvoked(string typeName)
    {
        if (_viewModel is not null)
        {
            _ = _viewModel.Symbols.ListMembersAsync(typeName);
        }
    }

    private void OnMembersSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _viewModel?.Symbols.MembersFilter = sender.Text;
    }

    private void OnSymbolSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            sender.ItemsSource = _viewModel?.Symbols.SuggestionsFor(sender.Text);
        }

        _viewModel?.Symbols.SymbolSearchText = sender.Text;
    }

    private void OnSymbolSearchGotFocus(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && sender is AutoSuggestBox { Text.Length: 0 } box)
        {
            box.ItemsSource = _viewModel.Symbols.SuggestionsFor(string.Empty);

            box.IsSuggestionListOpen = true;
        }
    }

    private void OnDetailTabChanged(object sender, SelectionChangedEventArgs args)
    {
        _viewModel?.Symbols.IsSymbolSearchSelected = sender is TabView { SelectedIndex: 1 };
    }

    private void OnModuleRightTapped(object sender, RightTappedRoutedEventArgs e) =>
        _contextModule = ((sender as FrameworkElement)?.DataContext as TreeViewNode)?.Content as SymbolModuleRow;

    private void OnExcludeModuleClick(object sender, RoutedEventArgs e)
    {
        if (_contextModule is { } module)
        {
            _viewModel?.Symbols.ExcludeModule(module.Module);
        }
    }

    private void OnClearModuleExclusionsClick(object sender, RoutedEventArgs e) => _viewModel?.Symbols.ClearModuleExclusions();

    private void OnExpandSymbolsClick(object sender, RoutedEventArgs e) => ExpandSymbolTree(true);

    private void OnCollapseSymbolsClick(object sender, RoutedEventArgs e) => ExpandSymbolTree(false);

    private void ExpandSymbolTree(bool expanded)
    {
        foreach (var node in SymbolTree.RootNodes)
        {
            SetExpanded(node, expanded);
        }
    }

    private void BuildSymbolTree()
    {
        SymbolTree.RootNodes.Clear();

        foreach (var item in _viewModel?.Symbols.SymbolSearchItems ?? [])
        {
            var node = new TreeViewNode { Content = item };

            if (item is SymbolModuleRow module)
            {
                node.IsExpanded = module.IsExpanded;

                foreach (var group in module.Classes)
                {
                    var groupNode = new TreeViewNode { Content = group, IsExpanded = group.IsExpanded };

                    foreach (var member in group.Members)
                    {
                        groupNode.Children.Add(new TreeViewNode { Content = member });
                    }

                    node.Children.Add(groupNode);
                }
            }

            SymbolTree.RootNodes.Add(node);
        }
    }

    private void OnSymbolFieldToggled(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not ToggleButton { Tag: string field } toggle)
        {
            return;
        }

        var on = toggle.IsChecked == true;

        switch (field)
        {
            case "Module":
                _viewModel.Symbols.SymbolSearchModule = on;

                break;

            case "Class":
                _viewModel.Symbols.SymbolSearchClass = on;

                break;

            case "Signature":
                _viewModel.Symbols.SymbolSearchSignature = on;

                break;
        }
    }

    private void CloseDetailPane()
    {
        IsMembersPaneVisible = false;
    }

    private void ToggleDetailPaneDock()
    {
        IsMembersPaneDockedBottom = !IsMembersPaneDockedBottom;
    }

    private void RefreshDetailPane()
    {
        Grid.SetRow(DetailPane, IsMembersPaneDockedBottom ? 2 : 0);
        Grid.SetColumn(DetailPane, IsMembersPaneDockedBottom ? 0 : 2);

        Bindings.Update();
    }

    private static void SetExpanded(TreeViewNode? node, bool expanded)
    {
        if (node is null)
        {
            return;
        }

        node.IsExpanded = expanded;

        foreach (var child in node.Children)
        {
            SetExpanded(child, expanded);
        }
    }

    private void OnViewModelChanged()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnPropertyChanged;
            _viewModel.Symbols.PropertyChanged -= OnSymbolsPropertyChanged;
            _viewModel.QueryOptions.PropertyChanged -= OnQueryOptionsPropertyChanged;
            _viewModel.QueryOptions.FilterChanged -= OnQueryFilterChanged;
        }

        _viewModel = ViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnPropertyChanged;
            _viewModel.Symbols.PropertyChanged += OnSymbolsPropertyChanged;
            _viewModel.QueryOptions.PropertyChanged += OnQueryOptionsPropertyChanged;
            _viewModel.QueryOptions.FilterChanged += OnQueryFilterChanged;
        }

        RefreshDetailPane();

        BuildSymbolTree();

        ApplyFocus(_viewModel?.SelectedEvent);
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A new query's events are new objects, so nowhere the history points at exists any more.
        if (e.PropertyName == nameof(QueryViewModel.CallStack))
        {
            ClearHistory();

            ApplyFocus(_viewModel?.SelectedEvent);
        }
        else if (e.PropertyName == nameof(QueryViewModel.SelectedEvent) && !_isSelectingFromTree)
        {
            RecordHistory(_viewModel?.SelectedEvent);

            ApplyFocus(_viewModel?.SelectedEvent);
        }
    }

    private void OnQueryOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueryOptionsViewModel.ShowLatches))
        {
            ApplyFocus(_viewModel?.SelectedEvent);
        }
    }

    private void OnQueryFilterChanged() => ApplyFocus(_viewModel?.SelectedEvent);

    private void OnSymbolsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel?.Symbols is not { } symbols)
        {
            return;
        }

        if (e.PropertyName is nameof(SymbolsViewModel.IsPaneVisible) or nameof(SymbolsViewModel.IsPaneDockedBottom))
        {
            RefreshDetailPane();
        }
        else if (e.PropertyName == nameof(SymbolsViewModel.SymbolSearchItems))
        {
            BuildSymbolTree();
        }
        else if (e.PropertyName == nameof(SymbolsViewModel.MembersFilter) && MembersSearchBox.Text != symbols.MembersFilter)
        {
            MembersSearchBox.Text = symbols.MembersFilter;
        }
        else if (e.PropertyName == nameof(SymbolsViewModel.IsSymbolSearchSelected))
        {
            DetailTabs.SelectedIndex = symbols.IsSymbolSearchSelected ? 1 : 0;

            if (symbols.IsSymbolSearchSelected)
            {
                DispatcherQueue.TryEnqueue(() => SymbolSearchBox.Focus(FocusState.Programmatic));
            }
        }
        else if (e.PropertyName == nameof(SymbolsViewModel.SymbolSearchText) && SymbolSearchBox.Text != symbols.SymbolSearchText)
        {
            SymbolSearchBox.Text = symbols.SymbolSearchText;
        }
    }

    private void ApplyFocus(EngineEvent? selected)
    {
        SetHighlightBuckets(selected);

        if (_focus)
        {
            if (selected is not null and not ExecutionOperatorEvent)
            {
                BuildEventTree(selected);
            }
            else
            {
                BuildPlanTree(SelectedNodeId(selected));
            }

            ApplyActivityBands();

            return;
        }

        var events = selected is null ? [] : ScopeEvents(selected).ToList();

        var leaves = events.Where(e => e.CallStack is not null).Select(e => e.CallStack!).Distinct().ToList();

        _visible = leaves.Count == 0 ? null : VisibleFrom(leaves);

        _revealInfrastructure = false;

        BuildTree();

        if (_visible is not null && _nodes.Count == 0)
        {
            _revealInfrastructure = true;

            BuildTree();
        }

        if (_visible is not null)
        {
            foreach (var treeNode in _nodes.Values)
            {
                treeNode.IsExpanded = true;
            }
        }

        var target = leaves.Count switch
        {
            0 => null,
            1 => leaves[0],
            _ => CommonAncestor(leaves),
        };

        SelectNode(target);

        ApplyActivityBands();
    }

    private void ApplyActivityBands()
    {
        var band = _activity ? new ActivityBand(_highlightBuckets) : null;

        foreach (var root in Tree.RootNodes)
        {
            ApplyActivityBands(root, band);
        }
    }

    private static void ApplyActivityBands(TreeViewNode treeNode, ActivityBand? band)
    {
        if (treeNode.Content is CallStackNode node)
        {
            node.Activity = band;
        }

        foreach (var child in treeNode.Children)
        {
            ApplyActivityBands(child, band);
        }
    }

    private ActivitySpan? OperatorSpan(ExecutionOperatorEvent op)
    {
        var tree = _viewModel?.CallStack;

        if (tree is null || tree.ActivityMaxUs <= tree.ActivityMinUs)
        {
            return null;
        }

        var startUs = op.TimeUs;

        var endUs = op.DurationUs > 0 ? op.TimeUs + op.DurationUs : startUs;

        if (op.DurationUs <= 0)
        {
            var events = ScopeEvents(op).ToList();

            if (events.Count == 0)
            {
                return null;
            }

            startUs = events.Min(e => e.TimeUs);
            endUs = events.Max(e => e.TimeUs);
        }

        var window = (double)(tree.ActivityMaxUs - tree.ActivityMinUs);

        var start = Math.Clamp((startUs - tree.ActivityMinUs) / window, 0, 1);

        var end = Math.Clamp((endUs - tree.ActivityMinUs) / window, start, 1);

        return new ActivitySpan(start, Math.Max(end, start + 1d / tree.ActivityBuckets));
    }

    private void SetHighlightBuckets(EngineEvent? selected)
    {
        var tree = _viewModel?.CallStack;

        if (selected is null or ExecutionOperatorEvent || tree is null)
        {
            _highlightBuckets = [];

            return;
        }

        _highlightBuckets = ScopeEvents(selected).Select(e => tree.BucketOf(e.TimeUs)).ToHashSet();
    }

    private static HashSet<CallStackNode> VisibleFrom(List<CallStackNode> leaves)
    {
        var visible = new HashSet<CallStackNode>();

        foreach (var leaf in leaves)
        {
            for (var node = leaf; node is { IsRoot: false }; node = node.Parent)
            {
                visible.Add(node);
            }
        }

        return visible;
    }

    private static CallStackNode? CommonAncestor(List<CallStackNode> leaves)
    {
        var common = AncestorsAndSelf(leaves[0]).ToHashSet();

        for (var i = 1; i < leaves.Count; i++)
        {
            common.IntersectWith(AncestorsAndSelf(leaves[i]));
        }

        return common.OrderByDescending(DepthOf).FirstOrDefault();
    }

    private static IEnumerable<CallStackNode> AncestorsAndSelf(CallStackNode node)
    {
        for (var current = node; current is { IsRoot: false }; current = current.Parent)
        {
            yield return current;
        }
    }

    private static int DepthOf(CallStackNode node)
    {
        var depth = 0;

        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            depth++;
        }

        return depth;
    }

    // Clicking a frame selects the event it came from, so the grid, details and timeline all answer "what is this frame
    // doing here?" — the question the tree cannot answer on its own, and the one every diagnosis of the scoping has
    // needed. A frame with no events of its own reports the earliest beneath it: that is still the work it led to.
    private void OnFrameInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (_viewModel is null || args.InvokedItem is not TreeViewNode invoked)
        {
            return;
        }

        var selected = invoked.Content switch
        {
            OperatorLink link => link.Operator,
            OperatorRow row => row.Operator,
            CallStackNode frame => EarliestEvent(frame),
            _ => null,
        };

        if (selected is null)
        {
            return;
        }

        // A hand-off link is a request to GO somewhere, so it must rebuild — that is the whole of what it does. Every
        // other row is just reporting what it already shows, and rebuilding under the click would throw away the
        // expansion the user opened to get there.
        var navigating = invoked.Content is OperatorLink;

        _isSelectingFromTree = !navigating;

        try
        {
            _viewModel.SelectedEvent = selected;
        }
        finally
        {
            _isSelectingFromTree = false;
        }
    }

    private static EngineEvent? EarliestEvent(CallStackNode node)
    {
        EngineEvent? earliest = null;

        var pending = new Stack<CallStackNode>();

        pending.Push(node);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            foreach (var engineEvent in current.Events)
            {
                if (earliest is null || engineEvent.SequenceId < earliest.SequenceId)
                {
                    earliest = engineEvent;
                }
            }

            foreach (var child in current.ChildNodes)
            {
                pending.Push(child);
            }
        }

        return earliest;
    }

    private CallStackNode? SelectNode(CallStackNode? target)
    {
        for (var node = target; node is { IsRoot: false }; node = node.Parent)
        {
            if (_nodes.TryGetValue(node, out var treeNode))
            {
                Tree.SelectedNode = treeNode;

                BringIntoView(treeNode);

                return node;
            }
        }

        return null;
    }

    private void BringIntoView(TreeViewNode node) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (Tree.ContainerFromNode(node) is FrameworkElement container)
            {
                container.StartBringIntoView();
            }
        });

    private IEnumerable<EngineEvent> ScopeEvents(EngineEvent selected)
    {
        if (selected is ExecutionOperatorEvent { PlanNodeIdentifier: { } id })
        {
            return (_viewModel?.Events ?? []).Where(e => e.PlanNodeIdentifier == id);
        }

        // Any consolidated group (a read, a lock group) scopes to the raw events it owns, merging their stacks into one
        // tree — the group itself carries no call stack, its members do.
        if (selected is IEventGroup group)
        {
            return group.Events;
        }

        return [selected];
    }

    private void BuildTree()
    {
        ClearTree();

        // Merged: the tree is the whole query, which is not a segment of anything and so heads nothing.
        HideFocusHeader();

        foreach (var root in _viewModel?.CallStackRoots ?? [])
        {
            foreach (var treeNode in BuildVisible(root))
            {
                Tree.RootNodes.Add(treeNode);
            }
        }

        ExpandForSearch();
    }

    // A filtered tree left collapsed hides the very rows the search kept, so a search opens what it found. Attached
    // first: WinUI can drop a subtree expanded while detached.
    private void ExpandForSearch()
    {
        if (_search.Length == 0)
        {
            return;
        }

        foreach (var root in Tree.RootNodes)
        {
            SetExpanded(root, expanded: true);
        }
    }

    // Drop the selection BEFORE the nodes it points into: TreeView holds SelectedNode itself, so clearing RootNodes
    // while it still references a node from the previous tree leaves the control unable to show the new one — which is
    // why the first query renders (nothing selected yet) and every one after it does not.
    private void ClearTree()
    {
        Tree.SelectedNode = null;

        _nodes.Clear();

        HiddenOnly.Clear();

        Tree.RootNodes.Clear();
    }

    private IEnumerable<TreeViewNode> BuildVisible(CallStackNode node)
    {
        var children = node.ChildNodes
                           .OrderBy(child => child.Order)
                           .SelectMany(BuildVisible)
                           .ToList();

        var outOfScope = _visible is not null && !_visible.Contains(node);

        var infrastructureHidden = node.IsInfrastructure && !_revealInfrastructure;

        if (outOfScope || infrastructureHidden)
        {
            foreach (var child in children)
            {
                yield return child;
            }

            yield break;
        }

        // Dropped outright rather than promoting its children, unlike the filters above: those hide a row that is in the
        // way, this one has already established there is nothing under it worth showing.
        if (Filtered(node, children.Count))
        {
            yield break;
        }

        var treeNode = new TreeViewNode { Content = node };

        _nodes[node] = treeNode;

        foreach (var child in children)
        {
            treeNode.Children.Add(child);
        }

        yield return treeNode;
    }

    // The plan node a selection scopes the tree to: an operator names itself, and any other event names the operator it
    // was matched to — so selecting a read in the grid or timeline isolates the operator that issued it.
    private static PlanNodeIdentifier? SelectedNodeId(EngineEvent? selected) => selected?.PlanNodeIdentifier;

    // Scoped: root the tree at the plan's operators. Each operator row carries the call tree of its own events, then its
    // child operators; an operator whose whole subtree captured no call stacks is dropped.
    //
    // With a selection, only that operator is shown — the point of scoping is to see one node's stack in isolation, so
    // the rest of the plan is not built. With nothing selected there is nothing to isolate, so the whole plan is.
    private void BuildPlanTree(PlanNodeIdentifier? selectedNode)
    {
        ClearTree();

        // The whole plan is on screen, so nothing is isolated for the header to name; an isolated operator puts it back.
        HideFocusHeader();

        _operatorNodes.Clear();

        _hierarchy = OperatorHierarchy.Build(_viewModel?.Events ?? []);

        if (_hierarchy.Operators.Count == 0)
        {
            return;
        }

        if (selectedNode is not null
            && _hierarchy.Operators.FirstOrDefault(o => o.PlanNodeIdentifier == selectedNode) is { } selectedOperator)
        {
            BuildIsolatedOperator(selectedOperator);

            return;
        }

        foreach (var root in _hierarchy.Roots)
        {
            if (BuildOperatorNode(root) is { } node)
            {
                Tree.RootNodes.Add(node);
            }
        }

        // Expand the operator hierarchy once the nodes are attached (WinUI can drop a subtree expanded while detached);
        // the call frames under each operator stay collapsed so the plan stays readable.
        foreach (var operatorNode in _operatorNodes)
        {
            operatorNode.IsExpanded = true;
        }

        ExpandForSearch();
    }

    /// <summary>
    /// One event's own work: its stack cut at the nearest barrier above it, with the operator's frames subtracted
    /// </summary>
    /// <remarks>
    /// The event, not its kind — two reads through the same GetPageWithKey stay apart, because the scope is this
    /// instance and the barrier only says where to stop climbing. A group merges: scoping it to what it owns puts every
    /// member's stack in one tree, which for a read group is its latches and waits together.
    /// </remarks>
    private void BuildEventTree(EngineEvent selected)
    {
        ClearTree();

        _operatorNodes.Clear();

        if (_viewModel?.CallStack is not { } tree)
        {
            return;
        }

        _hierarchy = OperatorHierarchy.Build(_viewModel.Events ?? []);

        // An event hands off to nothing: it is the bottom of the plan.
        _nextOperator = new Dictionary<CallStackNode, ExecutionOperatorEvent>();

        // The way back up. Without it an event reached from the timeline is a dead end — there is no history to step
        // back through and the plan around it is gone.
        var op = selected.PlanNodeIdentifier is { } id
            ? _hierarchy.Operators.FirstOrDefault(o => o.PlanNodeIdentifier == id)
            : null;

        ShowFocusHeader(op, new EventRow(selected));

        var scope = selected.SelfAndOwned().ToHashSet(ReferenceEqualityComparer.Instance);

        // Cut at the barrier; failing that at the operator's own entry, so the stack is still bounded by something
        // rather than running back to the thread start. A barrier list will never cover every path.
        var projected = tree.Project(include: scope.Contains, cutAt: frame => frame.IsAccessBarrier);

        if (!projected.Root.ChildNodes.Any() && op is { EntryFrames.Count: > 0 })
        {
            projected = tree.Project(include: scope.Contains, cutAt: op.EntryFrames.Contains);
        }

        var nodes = ProjectedCallNodes(projected, revealInfrastructure: false);

        foreach (var node in nodes.Count > 0 ? nodes : ProjectedCallNodes(projected, revealInfrastructure: true))
        {
            Tree.RootNodes.Add(node);
        }

        // Attached first: WinUI can drop a subtree expanded while detached.
        foreach (var node in Tree.RootNodes)
        {
            SetExpanded(node, expanded: true);
        }
    }

    // One operator on its own, with its call tree expanded: the isolated stack for the selected node. No child operators
    // — their frames are a different node's work, which is exactly what isolating it means to exclude.
    //
    // The operator heads the view from the header rather than as a row the frames hang off, so the tree is only the
    // frames. The way back out lives there too: isolation is exactly where the plan around the operator is gone, so
    // without it the only route to the caller is the history, and there is none when the operator came from the timeline.
    private void BuildIsolatedOperator(ExecutionOperatorEvent op)
    {
        ShowFocusHeader(_hierarchy.Parent(op), new OperatorRow(op, Unsegmented: !Segmented(op)));

        foreach (var callNode in BuildOperatorCallTree(op))
        {
            Tree.RootNodes.Add(callNode);
        }

        // Attached first: WinUI can drop a subtree expanded while detached.
        foreach (var node in Tree.RootNodes)
        {
            SetExpanded(node, expanded: true);
        }
    }

    // A tree node for an operator: its own call tree followed by its child operators, or null when neither it nor any
    // descendant captured a call stack (so empty operators do not clutter the plan).
    private TreeViewNode? BuildOperatorNode(ExecutionOperatorEvent op)
    {
        var callNodes = BuildOperatorCallTree(op);

        var childOperatorNodes = _hierarchy.Children(op)
            .OrderBy(child => child.PlanNodeIdentifier!.NodeId)
            .Select(BuildOperatorNode)
            .OfType<TreeViewNode>()
            .ToList();

        // An operator the search names survives even with nothing under it: the row is itself the answer.
        var searched = _search.Length > 0 && Matches(op);

        if (callNodes.Count == 0 && childOperatorNodes.Count == 0 && !searched)
        {
            return null;
        }

        var operatorNode = new TreeViewNode { Content = new OperatorRow(op, Unsegmented: !Segmented(op), OperatorSpan(op)) };

        foreach (var callNode in callNodes)
        {
            operatorNode.Children.Add(callNode);
        }

        foreach (var childOperatorNode in childOperatorNodes)
        {
            operatorNode.Children.Add(childOperatorNode);
        }

        _operatorNodes.Add(operatorNode);

        return operatorNode;
    }

    // An operator's call tree (infrastructure hidden), or an empty list when it has no frames of its own. Falls back to
    // revealing infrastructure if hiding it would leave the operator's paths empty.
    private List<TreeViewNode> BuildOperatorCallTree(ExecutionOperatorEvent op)
    {
        if (_viewModel?.CallStack is not { } tree)
        {
            return [];
        }

        // No entry frame means no segment. Borrowing the enclosing operator's bounds instead would render ITS segment a
        // second time under this name — SELECT and the Compute Scalar beneath it showing the same frames — which reads
        // as the operator having done that work. Empty is what was actually found, and the row still holds its place in
        // the plan, so SELECT -> Compute Scalar -> Stream Aggregate stays intact with the middle link simply carrying
        // nothing.
        if (!Segmented(op))
        {
            return [];
        }

        var scope = _hierarchy.ScopeOf(op, _viewModel?.Events ?? []);

        if (scope.Count == 0)
        {
            return [];
        }

        // A projection rather than a scope set over the shared tree: a shared node holds every event that reached that
        // function, so its event count and category would be the whole query's, not this operator's. The projected
        // nodes carry only the events in scope, which is the point of scoping to it.
        //
        // Cut top and bottom: ExitFrames drops the operators nested inside this one, and a barrier drops the storage
        // work below it — a read's descent is that read's own detail, reachable by selecting it, and inlining it here
        // buries the operator under the same hundred frames repeated for every page it touched.
        //
        // Which operator each exit frame hands off to, so the segment can name what it stopped for rather than just
        // ending. Keyed on the frame because that is what Project records having cut. A barrier hands off to no
        // operator, so it is absent here and simply ends the branch.
        _nextOperator = _hierarchy.Descendants(op)
            .SelectMany(descendant => descendant.EntryFrames.Select(frame => (Frame: frame, Operator: descendant)))
            .GroupBy(link => link.Frame)
            .ToDictionary(link => link.Key, link => link.First().Operator);

        var projected = tree.Project(include: scope.Contains,
                                     cutAt: op.EntryFrames.Contains,
                                     stopBelow: frame => op.ExitFrames.Contains(frame) || frame.IsAccessBarrier);

        var nodes = ProjectedCallNodes(projected, revealInfrastructure: false);

        return nodes.Count > 0 ? nodes : ProjectedCallNodes(projected, revealInfrastructure: true);
    }

    // Whether this operator has frames of its own. When it does not, OperatorRow.Unsegmented says so and the row renders
    // empty rather than claiming somebody else's.
    private static bool Segmented(ExecutionOperatorEvent op) => op.EntryFrames.Count > 0;

    private List<TreeViewNode> ProjectedCallNodes(CallStackTree projected, bool revealInfrastructure)
    {
        var nodes = new List<TreeViewNode>();

        foreach (var root in projected.Root.ChildNodes.OrderBy(child => child.Order))
        {
            nodes.AddRange(BuildProjectedCall(root, revealInfrastructure));
        }

        return nodes;
    }

    // Renders a projected node's subtree, hiding infrastructure and promoting its visible children. No scope set: the
    // projection already contains only the operator's frames.
    private IEnumerable<TreeViewNode> BuildProjectedCall(CallStackNode node, bool revealInfrastructure)
    {
        var children = node.ChildNodes
                           .OrderBy(child => child.Order)
                           .SelectMany(child => BuildProjectedCall(child, revealInfrastructure))
                           .ToList();

        // Where the segment stopped, name what it stopped for: a link per operator the work continues in, so the plan
        // is still walkable from inside the stack instead of the call trailing off into nothing. Searched like any other
        // row — and counted as one below, so a frame whose only answer to the search is where it led still shows.
        foreach (var next in node.CutBelow
                                 .Select(frame => _nextOperator.GetValueOrDefault(frame))
                                 .OfType<ExecutionOperatorEvent>()
                                 .Where(Matches)
                                 .DistinctBy(next => next.PlanNodeIdentifier)
                                 .OrderBy(next => next.PlanNodeIdentifier!.NodeId))
        {
            children.Add(new TreeViewNode { Content = new OperatorLink(next) });
        }

        if (node.IsInfrastructure && !revealInfrastructure)
        {
            foreach (var child in children)
            {
                yield return child;
            }

            yield break;
        }

        if (Filtered(node, children.Count))
        {
            yield break;
        }

        var treeNode = new TreeViewNode { Content = node };

        foreach (var child in children)
        {
            treeNode.Children.Add(child);
        }

        yield return treeNode;
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _search = sender.Text?.Trim() ?? string.Empty;

        ApplyFocus(_viewModel?.SelectedEvent);
    }

    /// <summary>
    /// Whether a frame answers the search, across everything it shows
    /// </summary>
    /// <remarks>
    /// Every field the row displays, because there is no telling which one the reader has in mind — a class, a category,
    /// an operator badge and a module all look like plausible things to type.
    /// </remarks>
    private bool Matches(CallStackNode node)
        => _search.Length == 0
           || Contains(node.Symbol)
           || Contains(node.Category)
           || Contains(node.Operator)
           || Contains(node.Frame?.Module);

    private bool Matches(ExecutionOperatorEvent op)
        => _search.Length == 0 || Contains(op.OperatorDescription) || Contains(op.TargetLabel) || Contains(op.Name);

    private bool Contains(string? value)
        => value is not null && value.Contains(_search, StringComparison.OrdinalIgnoreCase);

    // A row survives a search by matching it, or by being on the way to something that does — a hit is unreadable
    // without the calls that led to it, so the ancestors come too.
    private bool Filtered(CallStackNode node, int survivingChildren)
        => survivingChildren == 0 && ((_search.Length > 0 && !Matches(node)) || HoldsOnlyHiddenEvents(node));

    private bool HoldsOnlyHiddenEvents(CallStackNode node)
    {
        if (_viewModel?.QueryOptions is not { } options)
        {
            return false;
        }

        if (HiddenOnly.TryGetValue(node, out var cached))
        {
            return cached;
        }

        var result = node.Events.All(e => IsHiddenKind(e, options)) && node.ChildNodes.All(HoldsOnlyHiddenEvents);

        HiddenOnly[node] = result;

        return result;
    }

    private static bool IsHiddenKind(EngineEvent engineEvent, QueryOptionsViewModel options) => engineEvent switch
    {
        LatchEvent => !options.ShowLatches,
        LockEvent lockEvent => !options.Includes(LockModeClassifier.Categorise(lockEvent.LockMode)),
        _ => false,
    };
}
