using System;
using System.ComponentModel;
using InternalsViewer.UI.App.Controls.CallStack;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.CallStack;
using InternalsViewer.UI.App.ViewModels.Query.CallStack;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed partial class CallTreeSymbolsPanelView : UserControl, ISignatureNavigator, IDisposable
{
    public CallTreeSymbolsPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => OnViewModelChanged();
    }

    public CallTreeTabViewModel? ViewModel => DataContext as CallTreeTabViewModel;

    private CallTreeTabViewModel? Attached { get; set; }

    private SymbolModuleRow? ContextModule { get; set; }

    public void OnTypeInvoked(string typeName) => ViewModel?.ListMembers(typeName);

    public void Dispose() => Detach();

    private void OnViewModelChanged()
    {
        Detach();

        Attached = ViewModel;

        if (Attached is { } attached)
        {
            attached.Symbols.PropertyChanged += OnSymbolsPropertyChanged;
            attached.SymbolSearchRequested += OnSymbolSearchRequested;
        }

        Bindings.Update();

        BuildSymbolTree();
    }

    private void Detach()
    {
        if (Attached is not { } attached)
        {
            return;
        }

        attached.Symbols.PropertyChanged -= OnSymbolsPropertyChanged;
        attached.SymbolSearchRequested -= OnSymbolSearchRequested;

        Attached = null;
    }

    private void OnSymbolSearchRequested() => DispatcherQueue.TryEnqueue(() => SymbolSearchBox.Focus(FocusState.Programmatic));

    private void OnSymbolsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Attached?.Symbols is not { } symbols)
        {
            return;
        }

        if (e.PropertyName == nameof(SymbolsViewModel.SymbolSearchItems))
        {
            BuildSymbolTree();
        }
        else if (e.PropertyName == nameof(SymbolsViewModel.SymbolSearchText) && SymbolSearchBox.Text != symbols.SymbolSearchText)
        {
            SymbolSearchBox.Text = symbols.SymbolSearchText;
        }
    }

    private void BuildSymbolTree()
    {
        SymbolTree.RootNodes.Clear();

        foreach (var item in Attached?.Symbols.SymbolSearchItems ?? [])
        {
            var node = new TreeViewNode { Content = item };

            if (item is SymbolModuleRow module)
            {
                node.IsExpanded = module.IsExpanded;

                foreach (var group in module.Classes)
                {
                    var groupNode = new TreeViewNode { Content = group, IsExpanded = group.IsExpanded };

                    foreach (var member in group.Members)
                    {
                        groupNode.Children.Add(new TreeViewNode { Content = member });
                    }

                    node.Children.Add(groupNode);
                }
            }

            SymbolTree.RootNodes.Add(node);
        }
    }

    private void OnSymbolSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            sender.ItemsSource = Attached?.Symbols.SuggestionsFor(sender.Text);
        }

        Attached?.Symbols.SymbolSearchText = sender.Text;
    }

    private void OnSymbolSearchGotFocus(object sender, RoutedEventArgs e)
    {
        if (Attached is { } attached && sender is AutoSuggestBox { Text.Length: 0 } box)
        {
            box.ItemsSource = attached.Symbols.SuggestionsFor(string.Empty);

            box.IsSuggestionListOpen = true;
        }
    }

    private void OnModuleRightTapped(object sender, RightTappedRoutedEventArgs e) =>
        ContextModule = ((sender as FrameworkElement)?.DataContext as TreeViewNode)?.Content as SymbolModuleRow;

    private void OnExcludeModuleClick(object sender, RoutedEventArgs e)
    {
        if (ContextModule is { } module)
        {
            Attached?.Symbols.ExcludeModule(module.Module);
        }
    }

    private void OnClearModuleExclusionsClick(object sender, RoutedEventArgs e) => Attached?.Symbols.ClearModuleExclusions();

    private void OnExpandSymbolsClick(object sender, RoutedEventArgs e) => ExpandSymbolTree(true);

    private void OnCollapseSymbolsClick(object sender, RoutedEventArgs e) => ExpandSymbolTree(false);

    private void ExpandSymbolTree(bool expanded)
    {
        foreach (var node in SymbolTree.RootNodes)
        {
            LayoutHelpers.SetExpanded(node, expanded);
        }
    }

    private void OnSymbolFieldToggled(object sender, RoutedEventArgs e)
    {
        if (Attached?.Symbols is not { } symbols || sender is not ToggleButton { Tag: string field } toggle)
        {
            return;
        }

        var on = toggle.IsChecked == true;

        switch (field)
        {
            case "Module":
                symbols.SymbolSearchModule = on;

                break;

            case "Class":
                symbols.SymbolSearchClass = on;

                break;

            case "Signature":
                symbols.SymbolSearchSignature = on;

                break;
        }
    }
}
