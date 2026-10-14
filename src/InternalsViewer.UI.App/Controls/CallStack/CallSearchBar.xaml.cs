using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using InternalsViewer.Query.CallStack;
using InternalsViewer.UI.App.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
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

    private string? _hiddenCategoriesSetting;

    private string? _prunedCategoriesSetting;

    public CallSearchBar()
    {
        InitializeComponent();

        Loaded += OnLoaded;

        Unloaded += OnUnloaded;
    }

    public event Action<string>? SearchTextChanged;

    public string Text
    {
        get => SearchBox.Text;
        set => SearchBox.Text = value;
    }

    private SettingsViewModel Settings => field ??= App.GetService<SettingsViewModel>();

    private DispatcherQueueTimer SearchTimer => field ??= CreateSearchTimer();

    private string PublishedSearch { get; set; } = string.Empty;

    private HashSet<string> HiddenCategories
    {
        get
        {
            var setting = Settings.CallTreeHiddenCategories;

            if (!string.Equals(setting, _hiddenCategoriesSetting, StringComparison.Ordinal))
            {
                _hiddenCategoriesSetting = setting;

                field = SettingsViewModel.SplitCategories(setting);
            }

            return field;
        }
    } = new(StringComparer.Ordinal);

    private HashSet<string> PrunedCategories
    {
        get
        {
            var setting = Settings.CallTreePrunedCategories;

            if (!string.Equals(setting, _prunedCategoriesSetting, StringComparison.Ordinal))
            {
                _prunedCategoriesSetting = setting;

                field = SettingsViewModel.SplitCategories(setting);
            }

            return field;
        }
    } = new(StringComparer.Ordinal);

    public void FocusSearch() => SearchBox.Focus(FocusState.Programmatic);

    public void Clear()
    {
        SearchTimer.Stop();

        PublishedSearch = string.Empty;

        SearchBox.Text = string.Empty;
    }

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
            SetCategory(category, hide: !IsHidden(category), withCallees: false);
        }
    }

    private void OnCategoryBadgeRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not Border { Tag: string category } badge)
        {
            return;
        }

        var menu = new MenuFlyout();

        menu.Items.Add(CategoryMenuItem(category, "Show", hide: false, withCallees: false));
        menu.Items.Add(CategoryMenuItem(category, "Hide", hide: true, withCallees: false));
        menu.Items.Add(CategoryMenuItem(category, "Hide With Callees", hide: true, withCallees: true));

        menu.ShowAt(badge, e.GetPosition(badge));

        e.Handled = true;
    }

    private RadioMenuFlyoutItem CategoryMenuItem(string category, string text, bool hide, bool withCallees)
    {
        var item = new RadioMenuFlyoutItem
        {
            Text = text,
            GroupName = nameof(CategoryMenuItem),
            IsChecked = IsHidden(category) == hide && PrunedCategories.Contains(category) == withCallees
        };

        item.Click += (_, _) => SetCategory(category, hide, withCallees);

        return item;
    }

    private void SetCategory(string category, bool hide, bool withCallees)
    {
        var hidden = HiddenCategories;

        var pruned = PrunedCategories;

        hidden.Remove(category);
        pruned.Remove(category);

        if (hide)
        {
            (withCallees ? pruned : hidden).Add(category);
        }

        SaveCategories(hidden, pruned);
    }

    private void OnAllCategoriesClick(object sender, RoutedEventArgs e)
    {
        var hidden = HiddenCategories;

        var pruned = PrunedCategories;

        pruned.Clear();

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
        _hiddenCategoriesSetting = string.Join(SettingsViewModel.CategorySeparator, hidden.Order(StringComparer.Ordinal));

        _prunedCategoriesSetting = string.Join(SettingsViewModel.CategorySeparator, pruned.Order(StringComparer.Ordinal));

        Settings.CallTreeHiddenCategories = _hiddenCategoriesSetting;

        Settings.CallTreePrunedCategories = _prunedCategoriesSetting;

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
        var converter = (IValueConverter)Application.Current.Resources["HexColorToBrushConverter"];

        var brush = converter.Convert(colour, typeof(Brush), null!, string.Empty) as Brush;

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
