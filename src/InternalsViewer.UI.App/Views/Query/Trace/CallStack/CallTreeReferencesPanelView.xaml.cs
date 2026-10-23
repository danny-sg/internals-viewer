using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Controls.Query.Trace.CallStack;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.CallStack.Values;
using InternalsViewer.UI.App.ViewModels.Query.Trace.CallStack;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Trace.CallStack;

public sealed partial class CallTreeReferencesPanelView : UserControl, IReferenceFinder, IDocumentCommands, IDisposable
{
    public CallTreeReferencesPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => OnViewModelChanged();
    }

    public ValueReferencesViewModel? ViewModel => DataContext as ValueReferencesViewModel;

    private ValueReferencesViewModel? Attached { get; set; }

    public void FindReferences(ulong value) => ViewModel?.Owner.FindReferences(value);

    public FrameworkElement CreateCommands() => TabCommands.Panel(0, new DecodeToggle());

    public void Dispose()
    {
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

    private void OnReferencesPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ValueReferencesViewModel.References))
        {
            BuildTree();
        }
    }

    private void OnValueTreeLoaded(object sender, RoutedEventArgs e) => LayoutHelpers.EnableHorizontalScrolling(ValueTree);

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

        var valueCalls = await references.FindCallsAsync(row);

        var calls = await CallNodesAsync(references, row.Use, valueCalls, 0);

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
            case ValueCallRow { ValueCall.Node: { } callNode } call:
                references.Owner.Tree.ShowCall(callNode, call.ValueCall.CallIndex);
                break;

            case ValueTypeRow { Iterator: { } iterator }:
                references.Owner.Tree.NavigateToIterator(iterator);
                break;

            case ValueNoteRow { ValueCalls: { } valueCalls } more when node.Parent is { Content: ValueUseRow row } parent:
            {
                parent.Children.Remove(node);

                foreach (var call in await CallNodesAsync(references, row.Use, valueCalls, more.From))
                {
                    parent.Children.Add(call);
                }

                break;
            }
        }
    }

    private static async Task<List<TreeViewNode>> CallNodesAsync(ValueReferencesViewModel references,
                                                                 TraceValueUse use,
                                                                 IReadOnlyList<TraceValueCall> valueCalls,
                                                                 int from)
    {
        var rows = await references.CallRowsAsync(use, valueCalls, from);

        var nodes = rows.Select(r => new TreeViewNode { Content = r }).ToList();

        var end = from + rows.Count;

        if (end < valueCalls.Count)
        {
            nodes.Add(new TreeViewNode { Content = ValueNoteRow.More(valueCalls, end) });
        }

        return nodes;
    }
}
