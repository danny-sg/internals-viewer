using System;
using InternalsViewer.Internals.Engine.Address;
using InternalsViewer.Query.CallStack.Locators;
using InternalsViewer.Query.CallStack;
using InternalsViewer.Query.FullTrace.CustomHooks;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.UI.App.Controls.Dialogs;
using InternalsViewer.UI.App.Controls;
using InternalsViewer.UI.App.Controls.CallStack;
using InternalsViewer.UI.App.Controls.Docking;
using InternalsViewer.UI.App.Controls.Results;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.CallStack.Arguments;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.Query.CallStack;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;

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

    private async Task AddCustomHookAsync(CallTreeTabViewModel viewModel, CallStackNode node, (string Name, int Register) argument)
    {
        if (node.Frame is not { } frame)
        {
            return;
        }

        var dialog = new CustomHookDialog(node.Symbol, argument.Name, argument.Register == ArgumentLayout.ReturnRegister)
        {
            XamlRoot = XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        viewModel.Query.AddCustomHook(new CustomHookDefinition(dialog.HookName.Trim(),
                                                               frame.Module,
                                                               frame.Guid,
                                                               frame.Age,
                                                               frame.Rva,
                                                               node.Symbol,
                                                               argument.Name,
                                                               argument.Register,
                                                               dialog.IsReadAtReturn,
                                                               dialog.Offset,
                                                               (int)dialog.Length));
    }

    private void OnArgumentCellMenuOpening(object? sender, ResultCellMenuEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        var arguments = viewModel.Arguments;

        var index = (int)e.Row.Id;

        if (arguments.GetNode(index) is { } node)
        {
            e.Menu.Items.Add(MenuItems.Create("Go To Call", () => viewModel.Tree.GoToCall(node)));

            e.Menu.Items.Add(MenuItems.Create("Call Stack", () => viewModel.ShowCallStack(node, index)));

            e.Menu.Items.Add(MenuItems.Create("Scope", () => viewModel.Query.TraceTimelineTab.PlayheadStack.Scope(node)));

            if (node.Frame is { Guid.Length: > 0 } && arguments.GetHookArgument(e.Ordinal) is { } argument)
            {
                e.Menu.Items.Add(MenuItems.Create("Custom Hook", () => _ = AddCustomHookAsync(viewModel, node, argument)));
            }
        }

        var cell = arguments.GetCell(index, e.Ordinal);

        if (cell.Locator is { ValueType: LocatorValueType.ColumnStoreObject } columnStoreObject)
        {
            e.Menu.Items.Add(MenuItems.Create("Open In Columnstore Viewer",
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

        if (arguments.GetOrigin(index, e.Ordinal) is { } origin)
        {
            e.Menu.Items.Add(MenuItems.Create("Go To Origin", () => viewModel.Tree.ShowOrigin(origin)));
        }

        AddCopyItems(e.Menu, value, arguments.GetDecodedText(index, e.Ordinal));

        if (arguments.GetHoBtId(index, e.Ordinal) is { } hobtId)
        {
            e.Menu.Items.Add(MenuItems.Create("Open Index", () => viewModel.Query.OpenHoBt(hobtId)));
        }

        if (arguments.GetIterator(value) is { } iterator)
        {
            e.Menu.Items.Add(MenuItems.Create($"Show {iterator.Label} Iterator", () => viewModel.Tree.NavigateToIterator(iterator)));
        }

        if (cell.Type is { } type && ArgumentRowBuilder.CanFind(type, value))
        {
            e.Menu.Items.Add(MenuItems.Create("Find", () => viewModel.FindReferences(value)));
        }
    }
}
