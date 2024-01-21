using System.Collections;
using InternalsViewer.UI.App.vNext.Controls.Allocation;
using DatabaseViewModel = InternalsViewer.UI.App.vNext.ViewModels.Tabs.DatabaseViewModel;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.vNext.Messages;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.vNext.Views;

public sealed partial class DatabaseView
{
    public DatabaseView()
    {
        InitializeComponent();

        AllocationItemRepeater.SizeChanged += OnParentSizeChanged;
    }

    public DatabaseViewModel ViewModel => (DatabaseViewModel)DataContext;

    public double AllocationMapHeight { get; set; }

    private void OnPageClicked(object? sender, PageClickedEventArgs e)
    {
        var pageAddress = new Internals.Engine.Address.PageAddress(e.FileId, e.PageId);

        WeakReferenceMessenger.Default.Send(new OpenPageMessage(new OpenPageRequest(ViewModel.Database, pageAddress)));
    }

    private void AppBarToggleButton_Changed(object sender, RoutedEventArgs e)
    {
        var isChecked = sender is AppBarToggleButton { IsChecked: true };
        AllocationLayerGridRow.Height = isChecked ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        if(isChecked)
        {
            AllocationLayerGrid.Height = Height / 2;
        }
    }

    private void OnParentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if(AllocationItemRepeater.ItemsSource is IList items)
        {
            var itemCount = items.Count;

            if(itemCount > 0)
            {
                var itemHeight = AllocationItemRepeater.ActualHeight / itemCount;

                ViewModel.AllocationMapHeight = itemHeight;
            }
        }

    }
}
