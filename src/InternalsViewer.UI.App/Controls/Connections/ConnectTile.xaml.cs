using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.Connections;

public sealed partial class ConnectTile
{
    public ConnectTile()
    {
        InitializeComponent();
    }

    private void ScrollViewer_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
    {
        if (e.FinalView.HorizontalOffset < 1)
        {
            ScrollBackButton.Visibility = Visibility.Collapsed;
        }
        else if (e.FinalView.HorizontalOffset > 1)
        {
            ScrollBackButton.Visibility = Visibility.Visible;
        }

        if (e.FinalView.HorizontalOffset > ScrollViewer.ScrollableWidth - 1)
        {
            ScrollForwardButton.Visibility = Visibility.Collapsed;
        }
        else if (e.FinalView.HorizontalOffset < ScrollViewer.ScrollableWidth - 1)
        {
            ScrollForwardButton.Visibility = Visibility.Visible;
        }
    }

    private void ScrollBackButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollViewer.ChangeView(ScrollViewer.HorizontalOffset - ScrollViewer.ViewportWidth, null, null);
        
        ScrollForwardButton.Focus(FocusState.Programmatic);
    }

    private void ScrollForwardButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollViewer.ChangeView(ScrollViewer.HorizontalOffset + ScrollViewer.ViewportWidth, null, null);
   
        ScrollBackButton.Focus(FocusState.Programmatic);
    }

    private void ScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateScrollButtonsVisibility();
    }

    private void UpdateScrollButtonsVisibility()
    {
        if (ScrollViewer.ScrollableWidth > 0)
        {
            ScrollForwardButton.Visibility = Visibility.Visible;
        }
        else
        {
            ScrollForwardButton.Visibility = Visibility.Collapsed;
        }
    }
}
