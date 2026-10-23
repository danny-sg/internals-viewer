using System;
using System.ComponentModel;
using InternalsViewer.UI.App.Controls.Query.Trace.CallStack.Signatures;
using InternalsViewer.UI.App.ViewModels.Query.Trace.CallStack;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Trace.CallStack;

public sealed partial class CallTreeMembersPanelView : UserControl, ISignatureNavigator, IDisposable
{
    public CallTreeMembersPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => OnViewModelChanged();
    }

    public CallTreeTabViewModel? ViewModel => DataContext as CallTreeTabViewModel;

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
