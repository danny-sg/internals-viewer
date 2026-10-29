using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.WinUI;
using InternalsViewer.Query.FullTrace;
using InternalsViewer.UI.App.Controls.Query.Trace.CallStack.Signatures;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Helpers.Converters.Page;
using InternalsViewer.UI.App.Models.Query.Trace.CallStack.Signatures.Enums;
using InternalsViewer.UI.App.Models.Query.Trace.CallStack.Signatures;
using InternalsViewer.UI.App.Models.Query.Trace.CallStack.Stack;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using InternalsViewer.UI.App.ViewModels.Query.Trace.CallStack;
using InternalsViewer.UI.App.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.Query.Trace.CallStack;

public sealed partial class CallStackControl : UserControl, IReferenceFinder, IDisposable
{
    private const double StackShare = 2;

    private const double DetailsShare = 1;

    public CallStackControl()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => OnViewModelChanged();

        StackTree.Loaded += OnStackTreeLoaded;

        Settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    public CallStackViewModel? ViewModel => DataContext as CallStackViewModel;

    private CallStackViewModel? Attached { get; set; }

    private SettingsViewModel Settings { get; } = App.GetService<SettingsViewModel>();

    private static Brush TransparentBrush { get; } = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    private HashSet<uint> ExpandedThreads { get; } = [];

    private PlayheadCallRow? ContextRow { get; set; }

    private SourceStep? ContextStep { get; set; }

    private SourceFrame? ContextFrame { get; set; }

    private bool IsBuilding { get; set; }

    public static Brush? RowBackground(bool isCurrent)
        => isCurrent ? AppResources.Get<Brush>("SolidBackgroundFillColorQuarternaryBrush") : null;

    public static string ArgumentLabel(string? label, string? location) => label ?? location ?? string.Empty;

    public static Visibility GetTargetVisibility(string? target) => GetVisibility(target is not null);

    public static Visibility GetChipVisibility(bool isTracked, string? target) => GetVisibility(isTracked && target is null);

    public void FindReferences(ulong value) => ViewModel?.FindReferences(value);

    public void Dispose()
    {
        Settings.PropertyChanged -= OnSettingsPropertyChanged;

        StackTree.Loaded -= OnStackTreeLoaded;

        Detach();
    }

    private static Visibility GetVisibility(bool isVisible) => isVisible ? Visibility.Visible : Visibility.Collapsed;

    private Visibility IsVisible(object? value) => GetVisibility(value is not null);

    private Visibility IsTextVisible(string? text) => GetVisibility(!string.IsNullOrEmpty(text));

    private Visibility IsMemoryVisible(CallSignaturePart? part) => GetVisibility(part is { IsMemoryTracked: true, MemoryTarget: null });

    private Visibility IsTargetVisible(CallSignaturePart? part) => GetVisibility(part?.MemoryTarget is not null);

    private string TargetText(CallSignaturePart? part) => part?.MemoryTarget ?? string.Empty;

    private Visibility IsEmpty(IReadOnlyList<PlayheadThreadRow>? threads) => GetVisibility(threads is not { Count: > 0 });

    private Visibility IsFunctionVisible(PlayheadCallRow? row, CallSignaturePart? selected)
        => GetVisibility(row is not null && selected is null);

    private Visibility AreArgumentsVisible(PlayheadCallRow? row) => GetVisibility(row is { Arguments.Count: > 0 });

    private Visibility IsSourceVisible(IReadOnlyList<SourceStep>? steps) => GetVisibility(steps is { Count: > 0 });

    private string PositionText(PlayheadCallRow? row) => row is { FromPosition.IsKnown: true } ? row.FromPosition.ToString() : "None";

    private string StartText(PlayheadCallRow? row)
        => row is null ? string.Empty : TracePositionText.GetRulerText(row.FromPosition, row.OriginSequence);

    private string EndText(PlayheadCallRow? row)
        => row is null ? string.Empty : TracePositionText.GetRulerText(row.ToPosition, row.OriginSequence);

    private string InstructionsText(PlayheadCallRow? row) => row is null ? string.Empty : $"{row.Instructions}";

    private Brush? CategoryBrush(PlayheadCallRow? row) => row is null ? null : HexColorToBrushConverter.ToBrush(row.Node.CategoryColour);

    private string ThisText(PlayheadCallRow? row)
        => row?.This is { } self
            ? self.MemoryTarget is null && self.Decoded is { } decoded && decoded != self.Text ? $"{self.Text}  {decoded}" : self.Text
            : "None";

    private string ReturnText(PlayheadCallRow? row)
        => row?.Return is { } returned
            ? $"{returned.TypeName} {returned.GetText(Settings.DecodeValues && returned.MemoryTarget is null)}".Trim()
            : "None";

    private void OnViewModelChanged()
    {
        if (ReferenceEquals(ViewModel, Attached))
        {
            return;
        }

        Detach();

        Attached = ViewModel;

        Attached?.PropertyChanged += OnViewModelPropertyChanged;

        Bindings.Update();

        Rebuild();

        ShowDetails();
    }

    private void Detach()
    {
        Attached?.PropertyChanged -= OnViewModelPropertyChanged;

        Attached = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CallStackViewModel.Threads):
                Rebuild();

                SelectRow();
                break;

            case nameof(CallStackViewModel.SelectedValue):
                ShowDetails();
                break;

            case nameof(CallStackViewModel.SelectedCall):
                ShowDetails();

                SelectRow();
                break;
        }
    }

    private void Rebuild()
    {
        IsBuilding = true;

        try
        {
            StackTree.SelectedNode = null;

            StackTree.RootNodes.Clear();

            var threads = Attached?.Threads ?? [];

            var anyExpanded = threads.Any(t => ExpandedThreads.Contains(t.ThreadId));

            for (var index = 0; index < threads.Count; index++)
            {
                var thread = threads[index];

                var root = new TreeViewNode { Content = thread };

                foreach (var call in thread.Calls)
                {
                    root.Children.Add(new TreeViewNode { Content = call });
                }

                StackTree.RootNodes.Add(root);

                if (ExpandedThreads.Contains(thread.ThreadId) || (!anyExpanded && index == 0))
                {
                    root.IsExpanded = true;

                    ExpandedThreads.Add(thread.ThreadId);
                }
            }
        }
        finally
        {
            IsBuilding = false;
        }
    }

    private void SelectRow()
    {
        if (Attached?.SelectedCall is not { } selected)
        {
            return;
        }

        foreach (var node in StackTree.RootNodes.SelectMany(r => r.Children))
        {
            if (ReferenceEquals(node.Content, selected))
            {
                StackTree.SelectedNode = node;

                return;
            }
        }
    }

    private void ShowDetails()
    {
        var isVisible = Attached is { SelectedValue: not null } or { SelectedCall: not null };

        if (isVisible != (DetailsPane.Visibility == Visibility.Visible))
        {
            DetailsPane.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

            DetailsSplitter.Visibility = DetailsPane.Visibility;

            SplitterColumn.Width = isVisible ? GridLength.Auto : new GridLength(0);

            StackColumn.Width = new GridLength(isVisible ? StackShare : 1, GridUnitType.Star);

            DetailsColumn.Width = isVisible ? new GridLength(DetailsShare, GridUnitType.Star) : new GridLength(0);
        }

        ShowDetailsTitle();

        ShowValueDetails();
    }

    private void ShowDetailsTitle()
    {
        DetailsTitle.Inlines.Clear();

        if (Attached?.SelectedCall?.Signature.FirstOrDefault(p => p.Kind == CallSignaturePartKind.Function) is { } function)
        {
            SignatureRuns.AddFunction(DetailsTitle.Inlines, function.Text);
        }

        if (Attached?.SelectedValue is { Label: { } label } value)
        {
            DetailsTitle.Inlines.Add(new Run
            {
                Text = $"  [{label}]",
                Foreground = AppResources.Get<Brush>("TextFillColorSecondaryBrush")
            });

            if (value.DeclaredType is { } type)
            {
                DetailsTitle.Inlines.Add(new Run { Text = " " });

                SignatureRuns.AddType(DetailsTitle.Inlines, type);
            }
        }

        ToolTipService.SetToolTip(DetailsTitle, string.Concat(DetailsTitle.Inlines.OfType<Run>().Select(r => r.Text)));
    }

    private void ShowValueDetails()
    {
        ValueTypeText.Inlines.Clear();

        ValueDecodedText.Inlines.Clear();

        ValueTypeCopyButton.Tag = null;

        ValueDecodedCopyButton.Tag = null;

        ValueDecodedCopyButton.Visibility = Visibility.Collapsed;

        if (Attached?.SelectedValue is not { } value)
        {
            return;
        }

        if ((value.DeclaredType ?? (value.IsThis ? CallSignaturePart.ThisLabel : null)) is { } type)
        {
            SignatureRuns.AddType(ValueTypeText.Inlines, type);
        }

        if (value.Location is { } location)
        {
            ValueTypeText.Inlines.Add(new Run
            {
                Text = ValueTypeText.Inlines.Count > 0 ? $" ({location})" : location,
                Foreground = AppResources.Get<Brush>("TextFillColorSecondaryBrush")
            });
        }

        if (value.Decoded is not null)
        {
            SignatureRuns.AddValue(ValueDecodedText.Inlines, value, decode: true);
        }

        ValueTypeCopyButton.Tag = GetText(ValueTypeText);

        ValueDecodedCopyButton.Tag = GetText(ValueDecodedText);

        var typeVisibility = GetVisibility(ValueTypeText.Inlines.Count > 0);

        ValueTypeLabel.Visibility = typeVisibility;

        ValueTypeText.Visibility = typeVisibility;

        ValueTypeCopyButton.Visibility = typeVisibility;

        var decodedVisibility = GetVisibility(ValueDecodedText.Inlines.Count > 0);

        ValueDecodedLabel.Visibility = decodedVisibility;

        ValueDecodedText.Visibility = decodedVisibility;

        ValueDecodedCopyButton.Visibility = decodedVisibility;
    }

    private Style GetStyle(string key) => (Style)Resources[key];

    private static string GetText(TextBlock textBlock) => string.Concat(textBlock.Inlines.OfType<Run>().Select(r => r.Text));

    private void OnStackTreeExpanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        if (!IsBuilding && args.Node.Content is PlayheadThreadRow thread)
        {
            ExpandedThreads.Add(thread.ThreadId);
        }
    }

    private void OnStackTreeCollapsed(TreeView sender, TreeViewCollapsedEventArgs args)
    {
        if (!IsBuilding && args.Node.Content is PlayheadThreadRow thread)
        {
            ExpandedThreads.Remove(thread.ThreadId);
        }
    }

    private void OnStackTreeItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode { Content: PlayheadCallRow row })
        {
            Attached?.Select(row);
        }
    }

    private void OnValueSelected(object? sender, CallSignaturePart part)
    {
        if (Attached is not { } viewModel)
        {
            return;
        }

        if ((sender as FrameworkElement)?.DataContext is TreeViewNode { Content: PlayheadCallRow row })
        {
            viewModel.SelectedCall = row;
        }

        viewModel.SelectedValue = part;
    }

    private void OnArgumentTapped(object sender, TappedRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CallSignaturePart part && Attached is { } viewModel)
        {
            viewModel.SelectedValue = part;
        }
    }

    private void OnCloseDetailsClick(object sender, RoutedEventArgs e) => Attached?.ClearSelection();

    private void OnSourceStepPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        SetSourceHover(sender, true);

        Attached?.Mark((sender as FrameworkElement)?.DataContext as SourceStep);
    }

    private void OnSourceFramePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        SetSourceHover(sender, true);

        if ((sender as FrameworkElement)?.DataContext is SourceFrame frame)
        {
            Attached?.MarkFrame(frame);
        }
    }

    private void OnSourcePointerExited(object sender, PointerRoutedEventArgs e)
    {
        SetSourceHover(sender, false);

        Attached?.Mark(null);
    }

    private static void SetSourceHover(object sender, bool hovered)
    {
        if (sender is Panel panel)
        {
            panel.Background = hovered ? AppResources.Get<Brush>("SystemFillColorSuccessBackgroundBrush") : TransparentBrush;
        }
    }

    private void OnSourceStepRightTapped(object sender, RightTappedRoutedEventArgs e)
        => ContextStep = (sender as FrameworkElement)?.DataContext as SourceStep;

    private void OnSourceGoToClick(object sender, RoutedEventArgs e)
    {
        if (ContextStep is { } step)
        {
            Attached?.GoTo(step);
        }
    }

    private void OnSourceFrameRightTapped(object sender, RightTappedRoutedEventArgs e)
        => ContextFrame = (sender as FrameworkElement)?.DataContext as SourceFrame;

    private void OnSourceFrameGoToClick(object sender, RoutedEventArgs e)
    {
        if (ContextFrame is { } frame)
        {
            Attached?.GoTo(frame);
        }
    }

    private void OnCallRowRightTapped(object sender, RightTappedRoutedEventArgs e)
        => ContextRow = (sender as FrameworkElement)?.DataContext is TreeViewNode { Content: PlayheadCallRow row } ? row : null;

    private void OnOpenInCallTreeClick(object sender, RoutedEventArgs e) => WithContextRow((stack, row) => stack.OpenInCallTree(row));

    private void OnZoomClick(object sender, RoutedEventArgs e) => WithContextRow((stack, row) => stack.Zoom(row));

    private void OnScopeClick(object sender, RoutedEventArgs e) => WithContextRow((stack, row) => stack.Scope(row));

    private void OnSearchClick(object sender, RoutedEventArgs e)
        => WithContextRow((stack, row) =>
        {
            if (TraceLinkMenu.GetSearchText(row.Node.Symbol) is { } text)
            {
                stack.Navigator.Search(text);
            }
        });

    private void OnCopySignatureClick(object sender, RoutedEventArgs e) => WithContextRow((_, row) => ClipboardText.Copy(DisplayText(row)));

    private void OnCopySymbolClick(object sender, RoutedEventArgs e) => WithContextRow((_, row) => ClipboardText.Copy(row.Node.Symbol));

    private void OnCopyAddressClick(object sender, RoutedEventArgs e)
        => WithContextRow((_, row) => ClipboardText.Copy(row.Node.Frame is { } frame ? $"0x{frame.Address:X}" : string.Empty));

    private void OnCopyRowSignatureClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is TreeViewNode { Content: PlayheadCallRow row })
        {
            ClipboardText.Copy(DisplayText(row));
        }
    }

    private void OnCopyArgumentClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CallSignaturePart part)
        {
            ClipboardText.Copy(part.GetText(Settings.DecodeValues));
        }
    }

    private void OnCopyValueClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string { Length: > 0 } text)
        {
            ClipboardText.Copy(text);
        }
    }

    private void OnCopyTapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

    private void OnMemoryClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is CallSignaturePart part)
        {
            ViewModel?.ShowMemory(part);
        }
    }

    private void OnMemoryTapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

    private void OnStartClick(object sender, RoutedEventArgs e) => MovePlayhead(Attached?.SelectedCall?.Span.From);

    private void OnEndClick(object sender, RoutedEventArgs e) => MovePlayhead(Attached?.SelectedCall?.Span.To);

    private void OnStartContextRequested(UIElement sender, ContextRequestedEventArgs e)
        => ShowCallLinkMenu(Attached?.SelectedCall is { } row ? new TraceCallLink(row.Span.From, row.Node, row.CallIndex) : null, sender, e);

    private void OnEndContextRequested(UIElement sender, ContextRequestedEventArgs e)
        => ShowCallLinkMenu(Attached?.SelectedCall is { } row ? new TraceCallLink(row.Span.To, row.Node, row.CallIndex) : null, sender, e);

    private void OnThisContextRequested(UIElement sender, ContextRequestedEventArgs e)
        => ShowAddressMenu(Attached?.SelectedCall?.This, sender, e);

    private void OnReturnContextRequested(UIElement sender, ContextRequestedEventArgs e)
        => ShowAddressMenu(Attached?.SelectedCall?.Return, sender, e);

    private void OnSelectedValueContextRequested(UIElement sender, ContextRequestedEventArgs e)
        => ShowAddressMenu(Attached?.SelectedValue, sender, e);

    private void OnArgumentContextRequested(UIElement sender, ContextRequestedEventArgs e)
        => ShowAddressMenu((sender as FrameworkElement)?.DataContext as CallSignaturePart, sender, e);

    private void OnDetailsTitleContextRequested(UIElement sender, ContextRequestedEventArgs e)
        => ShowSearchMenu(Attached?.SelectedValue is { } value ? value.DeclaredType : Attached?.SelectedCall?.Node.Symbol, sender, e);

    private void OnValueTypeContextRequested(UIElement sender, ContextRequestedEventArgs e)
        => ShowSearchMenu(Attached?.SelectedValue?.DeclaredType, sender, e);

    private void MovePlayhead(TracePoint? point)
    {
        if (Attached is { } stack && point is { } target)
        {
            stack.Navigator.MovePlayhead(target);
        }
    }

    private void ShowCallLinkMenu(TraceCallLink? link, UIElement sender, ContextRequestedEventArgs e)
    {
        if (Attached is { } stack && link is not null)
        {
            TraceLinkMenu.Show(TraceLinkMenu.CreateForPosition(stack.Navigator, link), sender, e);
        }
    }

    private void ShowAddressMenu(CallSignaturePart? part, UIElement sender, ContextRequestedEventArgs e)
    {
        if (Attached is not { } stack || part is null)
        {
            return;
        }

        var menu = part is { CanFind: true, Value: { } value } ? TraceLinkMenu.CreateForAddress(stack.Navigator, value) : new MenuFlyout();

        TraceLinkMenu.AddSearch(menu, stack.Navigator, part.MemoryTarget ?? part.DecodedClass);

        TraceLinkMenu.Show(menu, sender, e);
    }

    private void ShowSearchMenu(string? name, UIElement sender, ContextRequestedEventArgs e)
    {
        if (Attached is not { } stack)
        {
            return;
        }

        var menu = new MenuFlyout();

        TraceLinkMenu.AddSearch(menu, stack.Navigator, name);

        TraceLinkMenu.Show(menu, sender, e);
    }

    private void OnSignatureMemorySelected(object? sender, CallSignaturePart part)
    {
        if ((sender as FrameworkElement)?.DataContext is TreeViewNode { Content: PlayheadCallRow row })
        {
            ViewModel?.ShowMemory(row, part);
        }
    }

    private string DisplayText(PlayheadCallRow row) => string.Concat(row.Signature.Select(p => p.GetDisplayText(Settings.DecodeValues)));

    private void WithContextRow(Action<CallStackViewModel, PlayheadCallRow> action)
    {
        if (Attached is { } stack && ContextRow is { } row)
        {
            action(stack, row);
        }
    }

    private void OnStackTreeLoaded(object sender, RoutedEventArgs e)
    {
        if (StackTree.FindDescendant<TreeViewList>() is { } list)
        {
            list.ItemContainerTransitions = [];
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.DecodeValues))
        {
            Bindings.Update();
        }
    }
}
