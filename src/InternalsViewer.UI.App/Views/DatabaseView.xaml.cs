using System;
using System.Collections;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Controls;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels.Database;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views;

public sealed partial class DatabaseView : IDisposable
{
    public DatabaseView()
    {
        InitializeComponent();

        AllocationItemRepeater.SizeChanged += OnParentSizeChanged;
        AllocationLayerGrid.ViewIndexClicked += OnViewIndexClicked;
        AllocationLayerGrid.ViewColumnstoreClicked += OnViewColumnstoreClicked;
        PageAddressTextBox.AddressChanged += OnPageSelected;
        AllocationLayerGrid.PageClicked += OnPageSelected;

        AllocationInfoAppBarToggleButton.Checked += AppBarToggleButton_Changed;
        AllocationInfoAppBarToggleButton.Unchecked += AppBarToggleButton_Changed;

        AllocationTabView.Loaded += AllocationTabView_Loaded;
    }

    public DatabaseTabViewModel TabViewModel => (DatabaseTabViewModel)DataContext;

    public double AllocationMapHeight { get; set; }

    public Visibility ToInverseVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public void Dispose()
    {
        foreach (var child in this.FindChildren())
        {
            if (child is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        AllocationItemRepeater.SizeChanged -= OnParentSizeChanged;
        AllocationLayerGrid.PageClicked -= OnPageSelected;
        AllocationLayerGrid.ViewIndexClicked -= OnViewIndexClicked;
        AllocationLayerGrid.ViewColumnstoreClicked -= OnViewColumnstoreClicked;
        PageAddressTextBox.AddressChanged -= OnPageSelected;
        AllocationInfoAppBarToggleButton.Checked -= AppBarToggleButton_Changed;
        AllocationInfoAppBarToggleButton.Unchecked -= AppBarToggleButton_Changed;
        AllocationTabView.Loaded -= AllocationTabView_Loaded;

        // Releases the connection, and with it the backup's file handles, decode window and page map
        _ = (DataContext as DatabaseTabViewModel)?.DisposeAsync().AsTask();
    }

    private void AllocationTabView_Loaded(object sender, RoutedEventArgs e)
    {
        EnsureTabSelected();
    }

    private void EnsureTabSelected()
    {
        if (AllocationTabView.SelectedIndex < 0 && TabViewModel.DatabaseFiles.Length > 0)
        {
            AllocationTabView.SelectedIndex = 0;
        }
    }

    private void OnSwitchToTabsClick(object sender, RoutedEventArgs e)
    {
        TabViewModel.IsTabbedView = true;
    }

    private void OnSwitchToStackedClick(object sender, RoutedEventArgs e)
    {
        TabViewModel.IsTabbedView = false;
    }

    private void AllocationTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        EnsureTabSelected();
    }

    private void OnPageSelected(object? sender, PageAddressEventArgs e)
    {
        var pageAddress = new PageAddress(e.FileId, e.PageId);

        WeakReferenceMessenger.Default
                              .Send(new OpenPageMessage(new OpenPageRequest(TabViewModel.Database, pageAddress)));
    }

    private void AppBarToggleButton_Changed(object sender, RoutedEventArgs e)
    {
        var isChecked = sender is AppBarToggleButton { IsChecked: true };

        //AllocationLayerGridRow.Height = isChecked ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        if (isChecked)
        {
            AllocationLayerGrid.Height = Height / 2;
        }
    }

    private void OnParentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (AllocationItemRepeater.ItemsSource is IList items)
        {
            var itemCount = items.Count;

            if (itemCount > 0)
            {
                var itemHeight = AllocationItemRepeater.ActualHeight / itemCount;

                TabViewModel.AllocationMapHeight = itemHeight;
            }
        }
    }

#pragma warning disable VSTHRD100
    private async void OnViewIndexClicked(object? sender, PageAddressEventArgs e)
    {
        try
        {
            var pageAddress = new PageAddress(e.FileId, e.PageId);

            await WeakReferenceMessenger.Default
                                        .Send(new OpenIndexMessage(
                                            new OpenIndexRequest(TabViewModel.Database, pageAddress)));
        }
        catch (Exception exception)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(exception));
        }
    }
#pragma warning restore VSTHRD100

#pragma warning disable VSTHRD100
    private async void OnViewColumnstoreClicked(object? sender, long allocationUnitId)
    {
        try
        {
            await WeakReferenceMessenger.Default
                                        .Send(new OpenColumnstoreMessage(
                                            new OpenColumnstoreRequest(TabViewModel.Database, allocationUnitId)));
        }
        catch (Exception exception)
        {
            await WeakReferenceMessenger.Default.Send(new ExceptionMessage(exception));
        }
    }
#pragma warning restore VSTHRD100
}
