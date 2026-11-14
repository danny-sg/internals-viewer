using System.Collections.Generic;
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

    public static readonly DependencyProperty FooterProperty =
        DependencyProperty.Register(nameof(Footer),
                                    typeof(UIElement),
                                    typeof(MemoryObjectTabsView),
                                    new PropertyMetadata(null));

    public UIElement? Footer
    {
        get => (UIElement?)GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    public MemoryObjectTabsView()
    {
        InitializeComponent();

        Loaded += (_, _) => SelectFirst();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => SelectFirst();

    private void SelectFirst()
    {
        if (Strip.SelectedIndex < 0 && Tabs is { Count: > 0 })
        {
            Strip.SelectedIndex = 0;
        }
    }

    private static void OnTabsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (MemoryObjectTabsView)d;

        view.DispatcherQueue.TryEnqueue(view.SelectFirst);
    }
}
