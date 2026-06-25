using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.QueryReplay;

public sealed partial class EventFilterControl : UserControl
{
    public EventFilterViewModel? ViewModel
    {
        get => (EventFilterViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(EventFilterViewModel), typeof(EventFilterControl),
            new PropertyMetadata(null, OnViewModelChanged));

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((EventFilterControl)d).Bindings.Update();

    public EventFilterControl()
    {
        InitializeComponent();
    }
}
