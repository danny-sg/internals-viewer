using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.UI.App.Controls.Allocation;
using InternalsViewer.UI.App.Messages;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

/// <summary>Dock document hosting the per-file allocation maps for the active query.</summary>
public sealed partial class AllocationDocumentView : UserControl
{
    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    public AllocationDocumentView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
        AllocationItemRepeater.SizeChanged += OnParentSizeChanged;
    }

    private void OnParentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var itemCount = viewModel.DatabaseFiles.Length;

        if (itemCount > 0)
        {
            viewModel.AllocationMapHeight = AllocationItemRepeater.ActualHeight / itemCount;
        }
    }

    private void OnPageSelected(object? sender, PageAddressEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var pageAddress = new PageAddress(e.FileId, e.PageId);

        WeakReferenceMessenger.Default
                              .Send(new OpenPageMessage(new OpenPageRequest(viewModel.Database, pageAddress)));
    }
}
