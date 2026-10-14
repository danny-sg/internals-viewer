using System;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.UI.App.Controls;
using InternalsViewer.UI.App.Controls.CallStack;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Controls.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.Models.Query.CallStack;
using InternalsViewer.UI.App.ViewModels.Query.CallStack;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed partial class CallTreeArgumentsPanelView : UserControl, IDocumentCommands
{
    public CallTreeArgumentsPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public CallTreeTabViewModel? ViewModel => DataContext as CallTreeTabViewModel;

    public FrameworkElement CreateCommands() => TabCommands.Panel(0, new DecodeToggle());

    private static void AddCopyItems(MenuFlyout menu, ulong value, string? decoded)
    {
        var hex = $"0x{value:X}";

        if (decoded is not null && decoded != hex)
        {
            menu.Items.Add(MenuItems.Create("Copy Decoded", () => ClipboardText.Copy(decoded)));
        }

        menu.Items.Add(MenuItems.Create("Copy Value", () => ClipboardText.Copy(hex)));

        menu.Items.Add(new MenuFlyoutSeparator());
    }

    private void OnArgumentPageClicked(object? sender, PageAddressEventArgs e)
        => ViewModel?.OpenPage(new PageAddress(e.FileId, e.PageId), e.Slot is 0 ? null : e.Slot, KeyboardState.IsShiftDown());

    private void OnArgumentObjectClicked(object? sender, ObjectValue value)
    {
        if (value.Origin is { } origin)
        {
            ViewModel?.Tree.ShowOrigin(origin);
        }
    }

    private void OnCopySignatureClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.Arguments.Signature is { Length: > 0 } signature)
        {
            ClipboardText.Copy(signature);
        }
    }

    private void OnArgumentCellMenuOpening(object? sender, ResultCellMenuEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var arguments = viewModel.Arguments;

        var index = (int)e.Row.Id;

        if (arguments.NodeAt(index) is { } node)
        {
            e.Menu.Items.Add(MenuItems.Create("Go To Call", () => viewModel.Tree.GoToCall(node)));
        }

        var cell = arguments.CellAt(index, e.Ordinal);

        if (cell.Locator is { Kind: LocatorKind.ColumnStoreObject } columnStoreObject)
        {
            e.Menu.Items.Add(MenuItems.Create("Open In The Columnstore Viewer",
                                              () => viewModel.OpenLocator(columnStoreObject, KeyboardState.IsShiftDown())));
        }

        if (cell.Locator is { } locator)
        {
            e.Menu.Items.Add(MenuItems.Create("Search Call Tree", () => viewModel.Tree.RequestSearch(locator.SearchText)));
        }

        if (cell.Value is not { } value)
        {
            return;
        }

        if (arguments.OriginAt(index, e.Ordinal) is { } origin)
        {
            e.Menu.Items.Add(MenuItems.Create("Go To Origin", () => viewModel.Tree.ShowOrigin(origin)));
        }

        AddCopyItems(e.Menu, value, arguments.DecodedTextAt(index, e.Ordinal));

        if (arguments.HoBtIdAt(index, e.Ordinal) is { } hobtId)
        {
            e.Menu.Items.Add(MenuItems.Create("Open The Index", () => viewModel.Query.OpenHoBt(hobtId)));
        }

        if (arguments.GetIterator(value) is { } iterator)
        {
            e.Menu.Items.Add(MenuItems.Create($"Show The {iterator.Label} Iterator", () => viewModel.Tree.NavigateToIterator(iterator)));
        }

        if (cell.Type is { } type && ArgumentRowBuilder.CanFind(type, value))
        {
            e.Menu.Items.Add(MenuItems.Create("Find", () => viewModel.Find(value)));
        }
    }
}
