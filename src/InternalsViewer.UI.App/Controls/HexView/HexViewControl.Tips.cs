using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Windows.Foundation;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using InternalsViewer.UI.App.ViewModels.HexView;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.HexView;

public sealed partial class HexViewControl
{
    public static readonly DependencyProperty WatchesProperty
        = DependencyProperty.Register(nameof(Watches),
            typeof(ObservableCollection<HexWatch>),
            typeof(HexViewControl),
            new PropertyMetadata(null, OnWatchesChanged));

    public ObservableCollection<HexWatch>? Watches
    {
        get => (ObservableCollection<HexWatch>?)GetValue(WatchesProperty);
        set => SetValue(WatchesProperty, value);
    }

    private Dictionary<HexTipViewModel, Popup> TipPopups { get; } = [];

    private void HexRichTextBlock_SelectionChanged(object sender, RoutedEventArgs e)
    {
        SelectTip();

        PlaceTips();
    }

    private void HexRichTextBlock_LostFocus(object sender, RoutedEventArgs e) => ViewModel.Dismiss();

    private void HexMenu_Opening(object sender, object e)
    {
        SetLinkMenuItem();

        SetChangeMenuItems();

        CopyMenuItem.IsEnabled = HexRichTextBlock.SelectedText.Length > 0;

        ClearPinsMenuItem.IsEnabled = ViewModel.HasPins;
    }

    private void CopyMenuItem_Click(object sender, RoutedEventArgs e) => HexRichTextBlock.CopySelectionToClipboard();

    private void SelectAllMenuItem_Click(object sender, RoutedEventArgs e) => HexRichTextBlock.SelectAll();

    private void ClearPinsMenuItem_Click(object sender, RoutedEventArgs e) => ViewModel.ClearPins();

    private void OnTipsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncTipPopups();

        DrawPinOutlines();

        PlaceTips();
    }

    private void OnTipsLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Attach();

        PlaceTips();
    }

    private void OnTipsUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Detach();

        PlaceTips();
    }

    private void OnTipPinClicked(object? sender, HexTipViewModel tip)
    {
        var isUnpinning = tip.IsPinned;

        ViewModel.TogglePin(tip);

        if (isUnpinning && HexRichTextBlock.FocusState != FocusState.Unfocused)
        {
            SelectTip();
        }

        DrawPinOutlines();

        PlaceTips();
    }

    private void OnTipValuePinClicked(object? sender, HexTipValue value)
    {
        if (sender is HexTipControl { Tip: { } tip })
        {
            ViewModel.TogglePinnedValue(tip, value);
        }
    }

    private void OnTipLabelEditing(object? sender, HexTipViewModel tip)
    {
        ViewModel.Pin(tip);

        DrawPinOutlines();
    }

    private void OnTipSizeChanged(object sender, SizeChangedEventArgs e) => PlaceTips();

    private void SelectTip()
    {
        if (HexRichTextBlock.SelectionStart is not { } selectionStart
            || HexRichTextBlock.SelectionEnd is not { } selectionEnd
            || selectionStart.Offset == selectionEnd.Offset)
        {
            ViewModel.Dismiss();

            return;
        }

        var data = Data ?? [];

        var start = HexLayout.FromRunPosition(selectionStart.Offset, HexLayout.CharactersPerLine) - 1;

        var end = Math.Min(HexLayout.FromRunPosition(selectionEnd.Offset, HexLayout.CharactersPerLine) - 1, data.Length - 1);

        if (start < 0 || start > end)
        {
            ViewModel.Dismiss();

            return;
        }

        ViewModel.Select(BaseAddress + start, BaseAddress + end);
    }

    private void RefreshTips()
    {
        ViewModel.SetData(Data ?? [], BaseAddress);

        DrawPinOutlines();

        PlaceTips();
    }

    private void SyncTipPopups()
    {
        var removed = new List<HexTipViewModel>();

        foreach (var (tip, popup) in TipPopups)
        {
            if (ViewModel.Tips.Contains(tip))
            {
                continue;
            }

            popup.IsOpen = false;

            TipLayer.Children.Remove(popup);

            removed.Add(tip);
        }

        foreach (var tip in removed)
        {
            TipPopups.Remove(tip);
        }

        foreach (var tip in ViewModel.Tips)
        {
            if (TipPopups.ContainsKey(tip))
            {
                continue;
            }

            var control = new HexTipControl { Tip = tip };

            control.PinClicked += OnTipPinClicked;

            control.ValuePinClicked += OnTipValuePinClicked;

            control.LabelEditing += OnTipLabelEditing;

            control.SizeChanged += OnTipSizeChanged;

            var popup = new Popup { Child = control };

            TipLayer.Children.Add(popup);

            TipPopups[tip] = popup;
        }
    }

    private void PlaceTips()
    {
        var viewport = GetTextViewport();

        Dictionary<int, double>? offsets = null;

        foreach (var tip in ViewModel.Tips)
        {
            if (!TipPopups.TryGetValue(tip, out var popup))
            {
                continue;
            }

            var anchor = viewport is { } view
                ? HexTipLayout.GetAnchor(EnsureMetrics(), tip.End, BaseAddress, Data?.Length ?? 0, GetLineHeight(), view)
                : null;

            if (anchor is not { } bounds)
            {
                popup.IsOpen = false;

                continue;
            }

            var position = HexTipLayout.GetTipPosition(bounds);

            if (tip.IsValueTip)
            {
                offsets ??= [];

                offsets.TryGetValue(tip.End, out var offset);

                position = HexTipLayout.GetValuePosition(bounds, offset);

                offsets[tip.End] = offset + popup.Child.ActualSize.X + HexTipLayout.Gap;
            }

            popup.HorizontalOffset = position.X + ScrollViewer.BorderThickness.Left;

            popup.VerticalOffset = position.Y + ScrollViewer.BorderThickness.Top;

            popup.IsOpen = true;
        }
    }

    private Rect? GetTextViewport()
    {
        if (!IsLoaded
            || Visibility != Visibility.Visible
            || HexRichTextBlock.Blocks.Count == 0
            || ScrollViewer.Content is not UIElement content)
        {
            return null;
        }

        var origin = HexRichTextBlock.TransformToVisual(content).TransformPoint(default);

        return new Rect(ScrollViewer.HorizontalOffset - origin.X,
                        ScrollViewer.VerticalOffset - origin.Y,
                        ScrollViewer.ViewportWidth,
                        ScrollViewer.ViewportHeight);
    }

    private void DrawPinOutlines()
    {
        PinOutlineCanvas.Children.Clear();

        var length = Data?.Length ?? 0;

        if (HexRichTextBlock.Blocks.Count == 0 || length == 0 || !ViewModel.HasPins)
        {
            return;
        }

        var metrics = EnsureMetrics();

        var lineHeight = GetLineHeight();

        var brush = AppResources.Get<Brush>("AccentFillColorDefaultBrush");

        var outlined = new HashSet<(int Start, int End)>();

        foreach (var tip in ViewModel.Tips)
        {
            if (!tip.IsPinned || !outlined.Add((tip.Start, tip.End)))
            {
                continue;
            }

            var start = Math.Max(tip.Start - BaseAddress, 0);

            var end = Math.Min(tip.End - BaseAddress, length - 1);

            if (start > end)
            {
                continue;
            }

            foreach (var shape in CreateOutline(metrics.GetSpanBorder(start, end, lineHeight), brush))
            {
                PinOutlineCanvas.Children.Add(shape);
            }
        }
    }

    private static void OnWatchesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HexViewControl)d;

        if (e.NewValue is not ObservableCollection<HexWatch> watches)
        {
            control.Watches = [];

            return;
        }

        control.ViewModel.SetWatches(watches);
    }
}
