using System.ComponentModel;
using InternalsViewer.UI.App.ViewModels.Query;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs;

public sealed partial class InstructionsDocumentView : UserControl
{
    public QueryViewModel? ViewModel => DataContext as QueryViewModel;

    private QueryViewModel? _subscribed;

    public InstructionsDocumentView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += (_, _) => Unsubscribe();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
       // Bindings.Update();
        Subscribe();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {

    }

    private void Subscribe()
    {
        if (ReferenceEquals(_subscribed, ViewModel))
        {
            return;
        }

        Unsubscribe();

        _subscribed = ViewModel;

        if (_subscribed is not null)
        {
            _subscribed.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void Unsubscribe()
    {
        if (_subscribed is not null)
        {
            _subscribed.PropertyChanged -= OnViewModelPropertyChanged;
            _subscribed = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_subscribed is null)
        {
            return;
        }
    }
}
