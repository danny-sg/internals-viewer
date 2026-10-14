using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Query.FullTrace.CallLog;
using InternalsViewer.UI.App.Controls.CallStack;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.Models.Query.CallStack;
using InternalsViewer.UI.App.ViewModels.Query.CallStack;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed partial class CallStackReferencesPanelView : UserControl, IValueFinder, IDocumentCommands, IDisposable
{
    public CallStackReferencesPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => OnViewModelChanged();

        Display.IsDecoded = Settings.DecodeValues;

        Settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    public ValueReferencesViewModel? ViewModel => DataContext as ValueReferencesViewModel;

    private ValueReferencesViewModel? Attached { get; set; }

    private SettingsViewModel Settings { get; } = App.GetService<SettingsViewModel>();

    private ValueDisplay Display => (ValueDisplay)Resources["ValueDisplay"];

    public void Find(ulong value) => ViewModel?.Owner.Find(value);

    public FrameworkElement CreateCommands() => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 8, 0),
        Children = { new DecodeToggle() }
    };

    public void Dispose()
    {
        Settings.PropertyChanged -= OnSettingsPropertyChanged;

        Attached?.PropertyChanged -= OnReferencesPropertyChanged;

        Attached = null;
    }

    private static List<TreeViewNode> ReferenceNodes(ValueReferences? references)
    {
        var nodes = new List<TreeViewNode>();

        if (references is null)
        {
            return nodes;
        }

        if (references.Type is { } type)
        {
            nodes.Add(new TreeViewNode { Content = new ValueTypeRow(type, references.Iterator) });
        }

        foreach (var group in references.Groups)
        {
            var groupNode = new TreeViewNode { Content = group, IsExpanded = true };

            foreach (var row in group.Rows)
            {
                groupNode.Children.Add(new TreeViewNode { Content = row, HasUnrealizedChildren = true });
            }

            nodes.Add(groupNode);
        }

        return nodes;
    }

    private void OnViewModelChanged()
    {
        Attached?.PropertyChanged -= OnReferencesPropertyChanged;

        Attached = ViewModel;

        Attached?.PropertyChanged += OnReferencesPropertyChanged;

        Bindings.Update();

        BuildTree();
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.DecodeValues))
        {
            Display.IsDecoded = Settings.DecodeValues;
        }
    }

    private void OnReferencesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ValueReferencesViewModel.References))
        {
            BuildTree();
        }
    }

    private void BuildTree()
    {
        ValueTree.RootNodes.Clear();

        foreach (var node in ReferenceNodes(Attached?.References))
        {
            ValueTree.RootNodes.Add(node);
        }
    }

    private async void OnValueTreeExpanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        if (args.Node is not { HasUnrealizedChildren: true, Content: ValueUseRow row } node || Attached is not { } references)
        {
            return;
        }

        node.HasUnrealizedChildren = false;

        var loading = new TreeViewNode { Content = ValueNoteRow.Loading };

        node.Children.Add(loading);

        var matches = await references.FindCallsAsync(row);

        var calls = await CallNodesAsync(references, row.Use, matches, 0);

        node.Children.Remove(loading);

        if (calls.Count == 0)
        {
            node.Children.Add(new TreeViewNode { Content = new ValueNoteRow("No Calls") });
        }

        foreach (var call in calls)
        {
            node.Children.Add(call);
        }
    }

    private async void OnValueTreeItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is not TreeViewNode node || Attached is not { } references)
        {
            return;
        }

        switch (node.Content)
        {
            case ValueCallRow { Match.Node: { } callNode } call:
                references.Owner.Tree.ShowCall(callNode, call.Match.Call);
                break;

            case ValueTypeRow { Iterator: { } iterator }:
                references.Owner.Tree.NavigateToIterator(iterator);
                break;

            case ValueNoteRow { Matches: { } matches } more when node.Parent is { Content: ValueUseRow row } parent:
            {
                parent.Children.Remove(node);

                foreach (var call in await CallNodesAsync(references, row.Use, matches, more.From))
                {
                    parent.Children.Add(call);
                }

                break;
            }
        }
    }

    private static async Task<List<TreeViewNode>> CallNodesAsync(ValueReferencesViewModel references,
                                                                 TraceValueUse use,
                                                                 IReadOnlyList<TraceValueMatch> matches,
                                                                 int from)
    {
        var rows = await references.CallRowsAsync(use, matches, from);

        var nodes = rows.Select(r => new TreeViewNode { Content = r }).ToList();

        var end = from + rows.Count;

        if (end < matches.Count)
        {
            nodes.Add(new TreeViewNode { Content = ValueNoteRow.More(matches, end) });
        }

        return nodes;
    }
}
