using System;
using System.Collections.Generic;
using System.ComponentModel;
using CommunityToolkit.WinUI.Controls;
using InternalsViewer.UI.App.Controls.Instructions;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

public sealed partial class QueryInstructionsTabView : UserControl
{
    private static readonly Dictionary<string, (Func<QueryOptionsViewModel, bool> Get,
                                                Action<QueryOptionsViewModel> Toggle)> OptionLinks = new()
    {
        ["ShowWaits"] = (o => o.ShowWaits, o => o.ShowWaits = !o.ShowWaits),
        ["ShowLatches"] = (o => o.ShowLatches, o => o.ShowLatches = !o.ShowLatches),
        ["IncludeMemory"] = (o => o.IncludeMemory, o => o.IncludeMemory = !o.IncludeMemory),
        ["IncludeCallStack"] = (o => o.IncludeCallStack, o => o.IncludeCallStack = !o.IncludeCallStack),
        ["CropToQuery"] = (o => o.CropToQuery, o => o.CropToQuery = !o.CropToQuery),
        ["IncludeSystemObjects"] = (o => o.IncludeSystemObjects, o => o.IncludeSystemObjects = !o.IncludeSystemObjects)
    };

    private static readonly Dictionary<string, Action<QueryLayoutViewModel>> ViewLinks = new()
    {
        ["SqlEditor"] = l => l.IsSqlEditorVisible = true,
        ["Allocations"] = l => l.IsAllocationsVisible = true,
        ["ExecutionPlan"] = l => l.IsExecutionPlanVisible = true,
        ["Events"] = l => l.IsEventsVisible = true,
        ["Callstack"] = l => l.IsCallstackVisible = true,
        ["Timeline"] = l => l.IsTimelineVisible = true
    };

    private QueryOptionsViewModel? _subscribedOptions;

    private string _currentPage = "GettingStarted";

    public QueryInstructionsTabView()
    {
        InitializeComponent();

        MarkdownTextBlock.Config = CreateConfig();

        Loaded += (_, _) => RenderCurrentPage();
        Unloaded += (_, _) => Unsubscribe();
        DataContextChanged += (_, _) => Subscribe();
    }

    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private MarkdownConfig CreateConfig() => new()
    {
        Themes = new MarkdownThemes
        {
            ParagraphMargin = new Thickness(0, 0, 0, 12),
            H1Margin = new Thickness(0, 0, 0, 16),
            H2Margin = new Thickness(0, 12, 0, 8),
            H3Margin = new Thickness(0, 10, 0, 6),
            InlineCodeBackground = (Brush)Resources["InstructionsInlineCodeBackgroundBrush"],
            InlineCodeBorderBrush = (Brush)Resources["InstructionsInlineCodeBorderBrush"],
            InlineCodeBorderThickness = new Thickness(1),
            InlineCodeCornerRadius = new CornerRadius(4),
            InlineCodePadding = new Thickness(4, 0, 4, 2),
            QuoteBackground = (Brush)Resources["InstructionsQuoteBackgroundBrush"],
            QuoteBorderBrush = (Brush)Resources["InstructionsQuoteBorderBrush"],
            QuoteBorderThickness = new Thickness(3, 0, 0, 0),
            QuotePadding = new Thickness(12, 8, 12, 8),
            QuoteMargin = new Thickness(0, 8, 0, 8)
        }
    };

    private void Subscribe()
    {
        var options = ViewModel?.QueryOptions;

        if (ReferenceEquals(_subscribedOptions, options))
        {
            return;
        }

        Unsubscribe();

        _subscribedOptions = options;

        if (_subscribedOptions is not null)
        {
            _subscribedOptions.PropertyChanged += OnOptionsPropertyChanged;
        }

        RenderCurrentPage();
    }

    private void Unsubscribe()
    {
        if (_subscribedOptions is not null)
        {
            _subscribedOptions.PropertyChanged -= OnOptionsPropertyChanged;
            _subscribedOptions = null;
        }
    }

    private void OnOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e) => RenderCurrentPage();

    private void TopicsNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem { Tag: string page })
        {
            _currentPage = page;

            RenderCurrentPage();
        }
    }

    private void MarkdownTextBlock_OnLinkClicked(object sender, LinkClickedEventArgs e)
    {
        if (!InstructionsLink.TryParse(e.Uri, out var link))
        {
            return;
        }

        e.Handled = true;

        switch (link.Kind)
        {
            case InstructionsLinkKind.Page:
                SelectPage(link.Target);
                break;

            case InstructionsLinkKind.ToggleOption:
                if (ViewModel is not null && OptionLinks.TryGetValue(link.Target, out var option))
                {
                    option.Toggle(ViewModel.QueryOptions);
                }

                break;

            case InstructionsLinkKind.OpenView:
                if (ViewModel is not null && ViewLinks.TryGetValue(link.Target, out var open))
                {
                    open(ViewModel.Layout);
                }

                break;

            case InstructionsLinkKind.External:
                _ = Launcher.LaunchUriAsync(new Uri(link.Target));
                break;
        }
    }

    private void SelectPage(string page)
    {
        foreach (var item in TopicsNavigation.MenuItems)
        {
            if (item is NavigationViewItem navigationItem && Equals(navigationItem.Tag, page))
            {
                TopicsNavigation.SelectedItem = navigationItem;
                return;
            }
        }

        _currentPage = page;

        RenderCurrentPage();
    }

    private void RenderCurrentPage()
    {
        if (MarkdownTextBlock is null)
        {
            return;
        }

        var markdown = InstructionsPageProvider.GetPage(_currentPage);

        if (markdown is null)
        {
            return;
        }

        MarkdownTextBlock.Text = InstructionsPageProvider.Render(markdown, GetOptionState());
    }

    private Dictionary<string, bool> GetOptionState()
    {
        var state = new Dictionary<string, bool>();

        var options = ViewModel?.QueryOptions;

        if (options is null)
        {
            return state;
        }

        foreach (var (name, accessors) in OptionLinks)
        {
            state[name] = accessors.Get(options);
        }

        return state;
    }
}
