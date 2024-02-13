using System.Collections;
using InternalsViewer.UI.App.Controls.Allocation;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels.Database;
using Microsoft.UI.Xaml.Controls;
using InternalsViewer.Internals.Engine.Address;
using System;
using CommunityToolkit.WinUI;

namespace InternalsViewer.UI.App.Views;

public sealed partial class DatabaseView : IDisposable
{
    public DatabaseTabViewModel TabViewModel => (DatabaseTabViewModel)DataContext;

    public double AllocationMapHeight { get; set; }

    public DatabaseView()
    {
        InitializeComponent();

        AllocationItemRepeater.SizeChanged += OnParentSizeChanged;
        AllocationLayerGrid.ViewIndexClicked += OnViewIndexClicked;
        PageAddressTextBox.AddressChanged += OnPageSelected;
        AllocationLayerGrid.PageClicked += OnPageSelected;

        AllocationInfoAppBarToggleButton.Checked += AppBarToggleButton_Changed;
        AllocationInfoAppBarToggleButton.Unchecked += AppBarToggleButton_Changed;
    }

    private void OnPageSelected(object? sender, PageAddressEventArgs e)
    {
        var pageAddress = new PageAddress(e.FileId, e.PageId);

        WeakReferenceMessenger.Default.Send(new OpenPageMessage(new OpenPageRequest(TabViewModel.Database, pageAddress)));
    }

    private void AppBarToggleButton_Changed(object sender, RoutedEventArgs e)
    {
        var isChecked = sender is AppBarToggleButton { IsChecked: true };
        AllocationLayerGridRow.Height = isChecked ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

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

    private async void OnViewIndexClicked(object? sender, PageAddressEventArgs e)
    {
        var pageAddress = new PageAddress(e.FileId, e.PageId);

        await WeakReferenceMessenger.Default.Send(new OpenIndexMessage(new OpenIndexRequest(TabViewModel.Database, pageAddress)));
    }

    public void Dispose()
    {
        foreach(var child in AllocationItemRepeater.FindChildren())
        {
            if (child is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        AllocationItemRepeater.SizeChanged -= OnParentSizeChanged;
        AllocationLayerGrid.PageClicked -= OnPageSelected;
        AllocationLayerGrid.ViewIndexClicked -= OnViewIndexClicked;
        AllocationInfoAppBarToggleButton.Checked -= AppBarToggleButton_Changed;
        AllocationInfoAppBarToggleButton.Unchecked -= AppBarToggleButton_Changed;;
    }
}
