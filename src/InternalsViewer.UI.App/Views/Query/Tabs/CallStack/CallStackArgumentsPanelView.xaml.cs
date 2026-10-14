using System;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.UI.App.Controls;
using InternalsViewer.UI.App.Controls.CallStack;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Controls.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.CallStack;
using InternalsViewer.UI.App.ViewModels.Query.CallStack;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using Windows.UI.Core;

namespace InternalsViewer.UI.App.Views.Query.Tabs.CallStack;

public sealed partial class CallStackArgumentsPanelView : UserControl, IDocumentCommands
{
    public CallStackArgumentsPanelView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => Bindings.Update();
    }

    public CallStackTabViewModel? ViewModel => DataContext as CallStackTabViewModel;

    public FrameworkElement CreateCommands() => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, 8, 0),
        Children = { new DecodeToggle() }
    };

    private static bool IsShiftDown()
        => InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

    private static MenuFlyoutItem MenuItem(string text, Action action)
    {
        var item = new MenuFlyoutItem { Text = text };

        item.Click += (_, _) => action();

        return item;
    }

    private static void AddCopyItems(MenuFlyout menu, ulong value, string? decoded)
    {
        var hex = $"0x{value:X}";

        if (decoded is not null && decoded != hex)
        {
            menu.Items.Add(MenuItem("Copy Decoded", () => ClipboardText.Copy(decoded)));
        }

        menu.Items.Add(MenuItem("Copy Value", () => ClipboardText.Copy(hex)));

        menu.Items.Add(new MenuFlyoutSeparator());
    }

    private void OnArgumentPageClicked(object? sender, PageAddressEventArgs e)
        => ViewModel?.OpenPage(new PageAddress(e.FileId, e.PageId), e.Slot is 0 ? null : e.Slot, IsShiftDown());

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
            e.Menu.Items.Add(MenuItem("Go To Call", () => viewModel.Tree.GoToCall(node)));
        }

        var cell = arguments.CellAt(index, e.Ordinal);

        if (cell.Locator is { Kind: LocatorKind.ColumnStoreObject } columnStoreObject)
        {
            e.Menu.Items.Add(MenuItem("Open In The Columnstore Viewer", () => viewModel.OpenLocator(columnStoreObject, IsShiftDown())));
        }

        if (cell.Locator is { } locator)
        {
            e.Menu.Items.Add(MenuItem("Search Call Tree", () => viewModel.Tree.RequestSearch(locator.SearchText)));
        }

        if (cell.Value is not { } value)
        {
            return;
        }

        AddCopyItems(e.Menu, value, arguments.DecodedTextAt(index, e.Ordinal));

        if (arguments.HoBtIdAt(index, e.Ordinal) is { } hobtId)
        {
            e.Menu.Items.Add(MenuItem("Open The Index", () => viewModel.Query.OpenHoBt(hobtId)));
        }

        if (arguments.GetIterator(value) is { } iterator)
        {
            e.Menu.Items.Add(MenuItem($"Show The {iterator.Label} Iterator", () => viewModel.Tree.NavigateToIterator(iterator)));
        }

        if (cell.Type is { } type && ArgumentRowBuilder.CanFind(type, value))
        {
            e.Menu.Items.Add(MenuItem("Find", () => viewModel.Find(value)));
        }
    }
}
