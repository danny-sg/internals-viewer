using System;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace InternalsViewer.UI.App.vNext.Controls.Connections;

public sealed partial class HeaderTile
{
    public string Title
    {
        get { return (string)GetValue(TitleProperty); }
        set { SetValue(TitleProperty, value); }
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(HeaderTile), new PropertyMetadata(null));

    public static readonly DependencyProperty FlyoutProperty =
        DependencyProperty.Register(nameof(Flyout), typeof(FlyoutBase), typeof(HeaderTile), new PropertyMetadata(null));

    public string Description
    {
        get { return (string)GetValue(DescriptionProperty); }
        set { SetValue(DescriptionProperty, value); }
    }

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(HeaderTile), new PropertyMetadata(null));

    public object Source
    {
        get { return GetValue(SourceProperty); }
        set { SetValue(SourceProperty, value); }
    }

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(object), typeof(HeaderTile), new PropertyMetadata(null));


    public FlyoutBase Flyout
    {
        get { return (FlyoutBase)GetValue(FlyoutProperty); }
        set { SetValue(FlyoutProperty, value); }
    }

    public HeaderTile()
    {
        InitializeComponent();
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        Click?.Invoke(this, e);
    }

    public event EventHandler<RoutedEventArgs>? Click;
}
