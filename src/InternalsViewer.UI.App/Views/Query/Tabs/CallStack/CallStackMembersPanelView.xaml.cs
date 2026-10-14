using System;
using System.ComponentModel;
using InternalsViewer.UI.App.Controls.CallStack;
using InternalsViewer.UI.App.ViewModels.Query.CallStack;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed partial class CallStackMembersPanelView : UserControl, ISignatureNavigator, IDisposable
{
    public CallStackMembersPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => OnViewModelChanged();
    }

    public CallStackTabViewModel? ViewModel => DataContext as CallStackTabViewModel;

    private SymbolsViewModel? Symbols { get; set; }

    public void OnTypeInvoked(string typeName) => ViewModel?.ListMembers(typeName);

    public void Dispose() => Detach();

    private void OnViewModelChanged()
    {
        Detach();

        Symbols = ViewModel?.Symbols;

        Symbols?.PropertyChanged += OnSymbolsPropertyChanged;

        Bindings.Update();
    }

    private void Detach()
    {
        Symbols?.PropertyChanged -= OnSymbolsPropertyChanged;

        Symbols = null;
    }

    private void OnSymbolsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SymbolsViewModel.MembersFilter) && Symbols is { } symbols && MembersSearchBox.Text != symbols.MembersFilter)
        {
            MembersSearchBox.Text = symbols.MembersFilter;
        }
    }

    private void OnMembersBackClick(object sender, RoutedEventArgs e)
    {
        if (Symbols is { } symbols)
        {
            _ = symbols.GoBackMembersAsync();
        }
    }

    private void OnMembersSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        => Symbols?.MembersFilter = sender.Text;
}
