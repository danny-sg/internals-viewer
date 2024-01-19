using InternalsViewer.UI.App.vNext.Controls.Allocation;
using DatabaseViewModel = InternalsViewer.UI.App.vNext.ViewModels.Tabs.DatabaseViewModel;
using CommunityToolkit.Mvvm.Messaging;
using InternalsViewer.UI.App.vNext.Messages;

namespace InternalsViewer.UI.App.vNext.Views;

public sealed partial class DatabaseView
{
    public DatabaseView()
    {
        InitializeComponent();
    }

    public DatabaseViewModel ViewModel => (DatabaseViewModel)DataContext;

    private void OnPageClicked(object? sender, PageClickedEventArgs e)
    {
        var pageAddress = new Internals.Engine.Address.PageAddress(e.FileId, e.PageId);

        WeakReferenceMessenger.Default.Send(new OpenPageMessage(new OpenPageRequest(ViewModel.Database, pageAddress)));
    }
}
