using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.Models.Columnstore;
using InternalsViewer.UI.App.ViewModels.Columnstore;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Columnstore.Tabs;

public sealed partial class ColumnstoreRowGroupsTabView
{
    public ColumnstoreRowGroupsTabView()
    {
        InitializeComponent();

        // x:Bind resolves against the view, and the dock sets DataContext after the view is built
        DataContextChanged += (_, _) => Bindings.Update();
    }

    public ColumnstoreTabViewModel ViewModel => (ColumnstoreTabViewModel)DataContext;

    private async void DataPointerButton_Click(object sender, RoutedEventArgs e)
    {
        if (((HyperlinkButton)sender).Tag is not SegmentSummary { HasDataPointer: true } segment)
        {
            return;
        }

        var request = new OpenPageRequest(ViewModel.Database, segment.DataPage) { Slot = segment.DataSlot };

        await WeakReferenceMessenger.Default.Send(new OpenPageMessage(request));
    }

    private void ViewDictionaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (((HyperlinkButton)sender).Tag is SegmentSummary { HasDictionary: true } segment)
        {
            ViewModel.OpenDictionary(segment);
        }
    }

    private void ViewSegmentButton_Click(object sender, RoutedEventArgs e)
    {
        if (((HyperlinkButton)sender).Tag is SegmentSummary segment)
        {
            ViewModel.OpenSegment(segment);
        }
    }
}
