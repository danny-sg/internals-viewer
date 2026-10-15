using System;
using System.ComponentModel;
using InternalsViewer.Query.Results;
using InternalsViewer.UI.App.Controls.CallStack;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Controls.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Query.CallStack;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed partial class CallTreeBatchesPanelView : UserControl, IDocumentCommands, IDisposable
{
    public CallTreeBatchesPanelView()
    {
        InitializeComponent();

        BatchGrid.RowDetailsContent = CreateBatchDetails;

        DataContextChanged += (_, _) => Reattach();
    }

    public BatchesViewModel? ViewModel => DataContext as BatchesViewModel;

    private BatchesViewModel? Attached { get; set; }

    private GridLength DetailHeight { get; set; } = new(1, GridUnitType.Star);

    private GridLength BatchHeight { get; set; } = new(2, GridUnitType.Star);

    public FrameworkElement CreateCommands() => TabCommands.Panel(0, new DecodeToggle());

    public void Dispose() => Detach();

    private void Reattach()
    {
        if (ReferenceEquals(Attached, ViewModel))
        {
            return;
        }

        Detach();

        Attached = ViewModel;

        Attached?.PropertyChanged += OnViewModelPropertyChanged;

        Bindings.Update();

        UpdateDetail();
    }

    private void Detach()
    {
        Attached?.PropertyChanged -= OnViewModelPropertyChanged;

        Attached = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BatchesViewModel.HasDetail))
        {
            UpdateDetail();
        }
    }

    private FrameworkElement? CreateBatchDetails(ResultRow<long> row)
        => Attached is { } batches ? new CallTreeBatchDetailsView { Batches = batches, Call = (int)row.Id } : null;

    private void OnCallClicked(object? sender, CallValue value) => Attached?.Owner.Tree.ShowCall(value.Node, value.Call);

    private void OnCellMenuOpening(object? sender, ResultCellMenuEventArgs e)
    {
        var call = (int)e.Row.Id;

        if (Attached is { } batches && batches.GetCallNode(call) is { } node)
        {
            e.Menu.Items.Add(MenuItems.Create("Call Stack", () => batches.Owner.ShowCallStack(node, call)));
        }
    }

    private void UpdateDetail()
    {
        var isVisible = Attached?.HasDetail == true;

        DetailSplitter.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

        DetailPane.Visibility = DetailSplitter.Visibility;

        if (isVisible)
        {
            SplitterRow.Height = GridLength.Auto;

            BatchRow.Height = BatchHeight;

            DetailRow.Height = DetailHeight;

            return;
        }

        if (DetailRow.Height.Value > 0)
        {
            DetailHeight = DetailRow.Height;

            BatchHeight = BatchRow.Height;
        }

        SplitterRow.Height = new GridLength(0);

        DetailRow.Height = new GridLength(0);

        BatchRow.Height = new GridLength(1, GridUnitType.Star);
    }

    private void OnCloseDetailClick(object sender, RoutedEventArgs e) => Attached?.ClearSlot();
}
