using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.UI.App.Helpers.Converters.Page;
using InternalsViewer.UI.App.Models.Query.CallStack;
using InternalsViewer.UI.App.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;

namespace InternalsViewer.UI.App.Controls.CallStack;

public sealed partial class CallSearchBar : UserControl
{
    private const double PrunedBadgeOpacity = 0.5;

    private const int SearchDelayMilliseconds = 300;

    public static readonly DependencyProperty CallStackProperty =
        DependencyProperty.Register(nameof(CallStack),
                                    typeof(CallStackTree),
                                    typeof(CallSearchBar),
                                    new PropertyMetadata(null, OnCallStackChanged));

    public CallStackTree? CallStack
    {
        get => (CallStackTree?)GetValue(CallStackProperty);
        set => SetValue(CallStackProperty, value);
    }

    public static readonly DependencyProperty CommandsProperty =
        DependencyProperty.Register(nameof(Commands), typeof(object), typeof(CallSearchBar), new PropertyMetadata(null));

    public object? Commands
    {
        get => GetValue(CommandsProperty);
        set => SetValue(CommandsProperty, value);
    }

    public static readonly DependencyProperty IsDataFilterVisibleProperty =
        DependencyProperty.Register(nameof(IsDataFilterVisible), typeof(bool), typeof(CallSearchBar), new PropertyMetadata(false));

    public bool IsDataFilterVisible
    {
        get => (bool)GetValue(IsDataFilterVisibleProperty);
        set => SetValue(IsDataFilterVisibleProperty, value);
    }

    public CallSearchBar()
    {
        InitializeComponent();

        Loaded += OnLoaded;

        Unloaded += OnUnloaded;
    }

    public event Action<string>? SearchTextChanged;

    public event Action<CallTreeDataFilter>? DataFilterChanged;

    public string Text
    {
        get => SearchBox.Text;
        set => SearchBox.Text = value;
    }

    private SettingsViewModel Settings => field ??= App.GetService<SettingsViewModel>();

    private DispatcherQueueTimer SearchTimer => field ??= CreateSearchTimer();

    private string PublishedSearch { get; set; } = string.Empty;

    private IReadOnlySet<string> HiddenCategories => Settings.CallTreeHiddenCategorySet;

    private IReadOnlySet<string> PrunedCategories => Settings.CallTreePrunedCategorySet;

    public void FocusSearch() => SearchBox.Focus(FocusState.Programmatic);

    public void Clear()
    {
        SearchTimer.Stop();

        PublishedSearch = string.Empty;

        SearchBox.Text = string.Empty;

        BatchesFilterButton.IsChecked = false;

        RowsFilterButton.IsChecked = false;
    }

    private Visibility GetCommandsVisibility(object? commands) => commands is null ? Visibility.Collapsed : Visibility.Visible;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Settings.PropertyChanged += OnSettingsPropertyChanged;

        StyleCategoryBadges();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Settings.PropertyChanged -= OnSettingsPropertyChanged;

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsViewModel.CallTreeHiddenCategories) or nameof(SettingsViewModel.CallTreePrunedCategories))
        {
            StyleCategoryBadges();
        }
    }

    private DispatcherQueueTimer CreateSearchTimer()
    {
        var timer = DispatcherQueue.CreateTimer();

        timer.Interval = TimeSpan.FromMilliseconds(SearchDelayMilliseconds);
        timer.IsRepeating = false;

        timer.Tick += OnSearchTimerTick;

        return timer;
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            SearchTimer.Start();

            return;
        }

        PublishSearch();
    }

    private void OnSearchQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args) => PublishSearch();

    private void OnSearchTimerTick(DispatcherQueueTimer sender, object args) => PublishSearch();

    private void PublishSearch()
    {
        SearchTimer.Stop();

        var text = SearchBox.Text ?? string.Empty;

        if (string.Equals(text, PublishedSearch, StringComparison.Ordinal))
        {
            return;
        }

        PublishedSearch = text;

        SearchTextChanged?.Invoke(text);
    }

    private void OnDataFilterClick(object sender, RoutedEventArgs e)
    {
        var filter = CallTreeDataFilter.None;

        if (BatchesFilterButton.IsChecked == true)
        {
            filter |= CallTreeDataFilter.Batches;
        }

        if (RowsFilterButton.IsChecked == true)
        {
            filter |= CallTreeDataFilter.Rows;
        }

        DataFilterChanged?.Invoke(filter);
    }

    private void OnCategoryFilterClick(object sender, RoutedEventArgs e)
    {
        var open = CategoryFilterButton.IsChecked == true;

        CategoryPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;

        if (open)
        {
            BuildCategoryBadges();
        }
    }

    private void OnCategoryBadgeTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Border { Tag: string category })
        {
            SetCategory(category, isHidden: !IsHidden(category), withCallees: false);
        }
    }

    private void OnCategoryBadgeRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not Border { Tag: string category } badge)
        {
            return;
        }

        var menu = new MenuFlyout();

        menu.Items.Add(CategoryMenuItem(category, "Show", isHidden: false, withCallees: false));
        menu.Items.Add(CategoryMenuItem(category, "Hide", isHidden: true, withCallees: false));
        menu.Items.Add(CategoryMenuItem(category, "Hide With Callees", isHidden: true, withCallees: true));

        menu.ShowAt(badge, e.GetPosition(badge));

        e.Handled = true;
    }

    private RadioMenuFlyoutItem CategoryMenuItem(string category, string text, bool isHidden, bool withCallees)
    {
        var item = new RadioMenuFlyoutItem
        {
            Text = text,
            GroupName = nameof(CategoryMenuItem),
            IsChecked = IsHidden(category) == isHidden && PrunedCategories.Contains(category) == withCallees
        };

        item.Click += (_, _) => SetCategory(category, isHidden, withCallees);

        return item;
    }

    private void SetCategory(string category, bool isHidden, bool withCallees)
    {
        var hidden = new HashSet<string>(HiddenCategories, StringComparer.Ordinal);

        var pruned = new HashSet<string>(PrunedCategories, StringComparer.Ordinal);

        hidden.Remove(category);
        pruned.Remove(category);

        if (isHidden)
        {
            (withCallees ? pruned : hidden).Add(category);
        }

        SaveCategories(hidden, pruned);
    }

    private void OnAllCategoriesClick(object sender, RoutedEventArgs e)
    {
        var hidden = new HashSet<string>(HiddenCategories, StringComparer.Ordinal);

        var pruned = new HashSet<string>(StringComparer.Ordinal);

        if (AllCategoriesToggle.IsChecked == true)
        {
            hidden.Clear();
        }
        else
        {
            foreach (var badge in CategoryBadges.Children.OfType<Border>())
            {
                if (badge.Tag is string category)
                {
                    hidden.Add(category);
                }
            }
        }

        SaveCategories(hidden, pruned);
    }

    private void OnDefaultCategoriesClick(object sender, RoutedEventArgs e) => Settings.ResetCallTreeCategories();

    private void SaveCategories(HashSet<string> hidden, HashSet<string> pruned)
    {
        Settings.CallTreeHiddenCategories = string.Join(SettingsViewModel.CategorySeparator, hidden.Order(StringComparer.Ordinal));

        Settings.CallTreePrunedCategories = string.Join(SettingsViewModel.CategorySeparator, pruned.Order(StringComparer.Ordinal));

        StyleCategoryBadges();
    }

    private bool IsHidden(string category) => HiddenCategories.Contains(category) || PrunedCategories.Contains(category);

    private void BuildCategoryBadges()
    {
        CategoryBadges.Children.Clear();

        var categories = (CallStack?.Nodes() ?? [])
                         .Where(n => n.Frame is not null)
                         .GroupBy(n => n.Category)
                         .Select(g => (Category: g.Key, Colour: g.First().CategoryColour))
                         .OrderBy(c => c.Category, StringComparer.OrdinalIgnoreCase);

        foreach (var (category, colour) in categories)
        {
            CategoryBadges.Children.Add(CreateCategoryBadge(category, colour));
        }

        UpdateAllCategoriesToggle();
    }

    private Border CreateCategoryBadge(string category, string colour)
    {
        var brush = HexColorToBrushConverter.ToBrush(colour);

        var badge = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 2, 5, 3),
            BorderThickness = new Thickness(1),
            BorderBrush = brush,
            Tag = category,
            Child = new TextBlock { Text = category, FontSize = 9, FontWeight = FontWeights.SemiBold }
        };

        badge.Tapped += OnCategoryBadgeTapped;

        badge.RightTapped += OnCategoryBadgeRightTapped;

        StyleCategoryBadge(badge);

        return badge;
    }

    private void StyleCategoryBadges()
    {
        foreach (var badge in CategoryBadges.Children.OfType<Border>())
        {
            StyleCategoryBadge(badge);
        }

        UpdateAllCategoriesToggle();
    }

    private void StyleCategoryBadge(Border badge)
    {
        var pruned = badge.Tag is string prunedCategory && PrunedCategories.Contains(prunedCategory);

        var hidden = pruned || (badge.Tag is string category && HiddenCategories.Contains(category));

        badge.Background = hidden ? new SolidColorBrush(Microsoft.UI.Colors.Transparent) : badge.BorderBrush;

        badge.Opacity = pruned ? PrunedBadgeOpacity : 1;

        if (badge.Child is TextBlock text)
        {
            text.Foreground = hidden ? badge.BorderBrush : new SolidColorBrush(Microsoft.UI.Colors.White);

            text.TextDecorations = hidden ? TextDecorations.Strikethrough : TextDecorations.None;
        }

        ToolTipService.SetToolTip(badge, pruned ? "Hidden with its callees. Click to show"
                                         : hidden ? "Hidden, its callees move up to its caller. Click to show"
                                         : "Hide, keeping its callees. Right-click to hide its callees too");
    }

    private void UpdateAllCategoriesToggle()
        => AllCategoriesToggle.IsChecked = CategoryBadges.Children
                                                         .OfType<Border>()
                                                         .All(b => b.Tag is not string category || !IsHidden(category));

    private static void OnCallStackChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (CallSearchBar)d;

        if (control.CategoryPanel.Visibility == Visibility.Visible)
        {
            control.BuildCategoryBadges();
        }
    }
}
