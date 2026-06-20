using System;
using System.Collections.Generic;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Replay.Events;
using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.UI.App.Services;
using InternalsViewer.UI.App.ViewModels.QueryReplay;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.QueryReplay;

public sealed partial class EventGridControl : UserControl
{
    public event EventHandler<PageAddressEventArgs>? PageClicked;

    public EventGridViewModel ViewModel { get; }

    public List<EngineEvent> Events
    {
        get => (List<EngineEvent>)GetValue(EventsProperty);
        set => SetValue(EventsProperty, value);
    }

    public static readonly DependencyProperty EventsProperty =
        DependencyProperty.Register(nameof(Events), typeof(List<EngineEvent>), typeof(EventGridControl),
            new PropertyMetadata(null, OnEventsChanged));

    private static void OnEventsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EventGridControl)d;
        control.ViewModel.SetEvents((List<EngineEvent>)(e.NewValue ?? new List<EngineEvent>()));
    }

    public HashSet<int> SystemObjectIds
    {
        get => (HashSet<int>)GetValue(SystemObjectIdsProperty);
        set => SetValue(SystemObjectIdsProperty, value);
    }

    public static readonly DependencyProperty SystemObjectIdsProperty =
        DependencyProperty.Register(nameof(SystemObjectIds), typeof(HashSet<int>), typeof(EventGridControl),
            new PropertyMetadata(null, OnSystemObjectIdsChanged));

    private static void OnSystemObjectIdsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (EventGridControl)d;
        control.ViewModel.SetSystemObjectIds((HashSet<int>)(e.NewValue ?? new HashSet<int>()));
    }

    public EventGridControl()
    {
        ViewModel = new EventGridViewModel(App.GetService<SettingsService>());
        InitializeComponent();
    }

    private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (((HyperlinkButton)sender).Tag is PageAddress pageAddress)
        {
            PageClicked?.Invoke(this, new PageAddressEventArgs(pageAddress.FileId, pageAddress.PageId));
        }
    }
}
