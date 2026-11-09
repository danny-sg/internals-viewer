using System;
using Windows.System;
using Windows.UI.Core;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels.Page;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using InternalsViewer.UI.App.Controls;
using InternalsViewer.UI.App.Models.Logging;

namespace InternalsViewer.UI.App.Views;

public sealed partial class PageView : IDisposable
{
    private const double MinimumTabsWidth = 200;

    private const int WideTabsRow = 1;

    private const int NarrowTabsRow = 3;

    private const int WideTabsColumn = 2;

    public PageView()
    {
        InitializeComponent();

        PageAddressTextBox.AddressChanged += PageAddressTextBox_Changed;
        MarkerTreeView.PageClicked += Control_PageClicked;
        AllocationControl.PageClicked += Control_PageClicked;
        LogRecordTreeView.RecordClicked += OnLogRecordClicked;
        ResultsGrid.PageClicked += Control_PageClicked;
    }

    public PageTabViewModel ViewModel => (PageTabViewModel)DataContext;

    private bool IsNarrow { get; set; }

    public Visibility GetTabContentVisibility(int selectedIndex, bool isTabVisible, int index)
    {
        return selectedIndex == index && isTabVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public void Dispose()
    {
        PageAddressTextBox.AddressChanged -= PageAddressTextBox_Changed;
        MarkerTreeView.PageClicked -= Control_PageClicked;
        AllocationControl.PageClicked -= Control_PageClicked;
        LogRecordTreeView.RecordClicked -= OnLogRecordClicked;
        ResultsGrid.PageClicked -= Control_PageClicked;

        AllocationControl.Dispose();
        PfsControl.Dispose();

        (DataContext as PageTabViewModel)?.Dispose();
    }

    private void OnContentGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var isNarrow = ContentGrid.ActualWidth < DataColumn.ActualWidth + SlotColumn.ActualWidth + MinimumTabsWidth;

        if (isNarrow != IsNarrow)
        {
            IsNarrow = isNarrow;

            ApplyTabsLayout();
        }
    }

    private void ApplyTabsLayout()
    {
        Grid.SetRow(TabsArea, IsNarrow ? NarrowTabsRow : WideTabsRow);

        Grid.SetColumn(TabsArea, IsNarrow ? 0 : WideTabsColumn);

        Grid.SetColumnSpan(TabsArea, IsNarrow ? ContentGrid.ColumnDefinitions.Count : 1);

        ContentRow.Height = new GridLength(1, GridUnitType.Star);

        TabsRow.Height = IsNarrow ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        TabsSplitter.Visibility = IsNarrow ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnLogRecordClicked(LogRecordItem item)
    {
        ViewModel.SelectSlotForRecord(item);
    }

    private void PageAddressTextBox_Changed(object? sender, PageAddressEventArgs args)
    {
        ViewModel.LoadPageCommand.Execute(new PageAddress(args.FileId, args.PageId));
    }

    private void PfsControl_PageClicked(object? sender, PageAddressEventArgs e)
    {
        var state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);

        var isShiftPressed = state.HasFlag(CoreVirtualKeyStates.Down);

        if (isShiftPressed)
        {
            var pageAddress = new PageAddress(e.FileId, e.PageId + ViewModel.AllocationStartPage);

            var request = new OpenPageRequest(ViewModel.Database, pageAddress) { Slot = e.Slot };

#pragma warning disable VSTHRD110
            WeakReferenceMessenger.Default.Send(new OpenPageMessage(request));
#pragma warning restore VSTHRD110
        }
        else
        {
            var pageAddress = new PageAddress(e.FileId, e.PageId);

            ViewModel.SelectPfsPageCommand.Execute(pageAddress);
        }
    }

    private void Control_PageClicked(object? sender, PageAddressEventArgs e)
    {
        var pageAddress = new PageAddress(e.FileId, e.PageId);

        var state = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);

        var isShiftPressed = state.HasFlag(CoreVirtualKeyStates.Down);

        if (isShiftPressed)
        {
            var request = new OpenPageRequest(ViewModel.Database, pageAddress) { Slot = e.Slot };

#pragma warning disable VSTHRD110
            WeakReferenceMessenger.Default.Send(new OpenPageMessage(request));
#pragma warning restore VSTHRD110
        }
        else
        {
            if (e.Slot != null)
            {
                ViewModel.LoadRowIdentifierCommand.Execute(new RowIdentifier(pageAddress, e.Slot.Value));
            }
            else
            {
                ViewModel.LoadPageCommand.Execute(pageAddress);
            }
        }
    }
}
