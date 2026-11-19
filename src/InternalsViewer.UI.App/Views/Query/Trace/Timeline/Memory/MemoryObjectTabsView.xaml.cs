using System.Collections.Generic;
using InternalsViewer.UI.App.Controls.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Trace.Timeline.Memory;

public sealed partial class MemoryObjectTabsView : UserControl
{
    public static readonly DependencyProperty TabsProperty =
        DependencyProperty.Register(nameof(Tabs),
                                    typeof(List<MemoryObjectTab>),
                                    typeof(MemoryObjectTabsView),
                                    new PropertyMetadata(null, OnTabsChanged));

    public List<MemoryObjectTab>? Tabs
    {
        get => (List<MemoryObjectTab>?)GetValue(TabsProperty);
        set => SetValue(TabsProperty, value);
    }

    public static readonly DependencyProperty ParentHeaderProperty =
        DependencyProperty.Register(nameof(ParentHeader),
                                    typeof(string),
                                    typeof(MemoryObjectTabsView),
                                    new PropertyMetadata(null, OnParentHeaderChanged));

    public string? ParentHeader
    {
        get => (string?)GetValue(ParentHeaderProperty);
        set => SetValue(ParentHeaderProperty, value);
    }

    public MemoryObjectTabsView()
    {
        InitializeComponent();

        Loaded += (_, _) => SelectFirst();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => SelectFirst();

    private void OnCellMenuOpening(object? sender, ResultCellMenuEventArgs e) => MenuItems.AddCopy(e.Menu, e.Cell);

    private void SelectFirst()
    {
        if (Strip.SelectedIndex < 0 && Tabs is { Count: > 0 })
        {
            Strip.SelectedIndex = 0;
        }
    }

    private void ApplySingleTab()
    {
        var single = Tabs is [{ HasTable: true } tab] && tab.Header == ParentHeader ? tab : null;

        SingleTab.Content = single;

        SingleTab.Visibility = single is null ? Visibility.Collapsed : Visibility.Visible;

        Strip.Visibility = single is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void OnTabsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (MemoryObjectTabsView)d;

        view.ApplySingleTab();

        view.DispatcherQueue.TryEnqueue(view.SelectFirst);
    }

    private static void OnParentHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MemoryObjectTabsView)d).ApplySingleTab();
}
