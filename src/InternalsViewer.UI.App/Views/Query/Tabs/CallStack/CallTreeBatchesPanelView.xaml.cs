using System;
using System.ComponentModel;
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

        UpdateBatch();
    }

    private void Detach()
    {
        Attached?.PropertyChanged -= OnViewModelPropertyChanged;

        Attached = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(BatchesViewModel.HasDetail):
                UpdateDetail();
                break;

            case nameof(BatchesViewModel.SelectedCall):
                UpdateBatch();
                break;

            case nameof(BatchesViewModel.TableVersion):
                BatchView.Refresh();
                break;
        }
    }

    private void UpdateBatch()
    {
        if (Attached is { SelectedCall: { } call } batches)
        {
            BatchSplitter.Visibility = Visibility.Visible;

            BatchView.Visibility = Visibility.Visible;

            BatchSplitterRow.Height = GridLength.Auto;

            BatchRow.Height = BatchHeight;

            BatchView.Show(batches, call);

            return;
        }

        if (BatchRow.Height.Value > 0)
        {
            BatchHeight = BatchRow.Height;
        }

        BatchView.Clear();

        BatchSplitter.Visibility = Visibility.Collapsed;

        BatchView.Visibility = Visibility.Collapsed;

        BatchSplitterRow.Height = new GridLength(0);

        BatchRow.Height = new GridLength(0);
    }

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

            DetailRow.Height = DetailHeight;

            return;
        }

        if (DetailRow.Height.Value > 0)
        {
            DetailHeight = DetailRow.Height;
        }

        SplitterRow.Height = new GridLength(0);

        DetailRow.Height = new GridLength(0);
    }

    private void OnCloseDetailClick(object sender, RoutedEventArgs e) => Attached?.ClearSlot();
}
