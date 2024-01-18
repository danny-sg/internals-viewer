using InternalsViewer.Internals.Engine.Parsers;
using InternalsViewer.UI.App.vNext.ViewModels.Tabs;

namespace InternalsViewer.UI.App.vNext.Views;

public sealed partial class PageView
{
    public PageViewModel ViewModel => (PageViewModel)DataContext;

    public PageView()
    {
        InitializeComponent();
    }

    private void PageAddressTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (PageAddressParser.TryParse(PageAddressTextBox.Text, out var pageAddress))
            {
                ViewModel.LoadPageCommand.Execute(pageAddress);
            }
        }
    }
}
