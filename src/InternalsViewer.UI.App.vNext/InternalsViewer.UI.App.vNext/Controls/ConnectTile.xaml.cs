using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace InternalsViewer.UI.App.vNext.Controls;
public sealed partial class ConnectTile : UserControl
{
    public ConnectTile()
    {
        this.InitializeComponent();
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
        // Manually focus to ScrollForwardBtn since this button disappears after scrolling to the end.          
        ScrollForwardButton.Focus(FocusState.Programmatic);
    }

    private void ScrollForwardButton_Click(object sender, RoutedEventArgs e)
    {
        ScrollViewer.ChangeView(ScrollViewer.HorizontalOffset + ScrollViewer.ViewportWidth, null, null);

        // Manually focus to ScrollBackBtn since this button disappears after scrolling to the end.    
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
