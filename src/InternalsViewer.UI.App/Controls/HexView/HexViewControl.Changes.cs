using System;
using Windows.Foundation;
using Windows.System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Controls.HexView;

public sealed partial class HexViewControl
{
    public static readonly DependencyProperty IsChangeNavigationEnabledProperty
        = DependencyProperty.Register(nameof(IsChangeNavigationEnabled),
            typeof(bool),
            typeof(HexViewControl),
            new PropertyMetadata(false));

    public bool IsChangeNavigationEnabled
    {
        get => (bool)GetValue(IsChangeNavigationEnabledProperty);
        set => SetValue(IsChangeNavigationEnabledProperty, value);
    }

    public event EventHandler<HexChangeNavigation>? ChangeNavigationRequested;

    private Point? ChangePressPoint { get; set; }

    private int? MenuChangeOffset { get; set; }

    private void OnChangePointerPressed(object sender, PointerRoutedEventArgs e)
        => ChangePressPoint = e.GetCurrentPoint(HexRichTextBlock).Position;

    private void OnChangePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var pressed = ChangePressPoint;

        ChangePressPoint = null;

        var released = e.GetCurrentPoint(HexRichTextBlock);

        if (!IsChangeNavigationEnabled
            || pressed is not { } start
            || released.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonReleased
            || Math.Abs(released.Position.X - start.X) > ClickSlop
            || Math.Abs(released.Position.Y - start.Y) > ClickSlop
            || GetMarkerOffset(released.Position) is not { } offset)
        {
            return;
        }

        var isForward = !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);

        if (!isForward && HexRichTextBlock.SelectionEnd is { } end)
        {
            HexRichTextBlock.Select(end, end);
        }

        RequestChange(offset, isForward);
    }

    private void NextChangeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (MenuChangeOffset is { } offset)
        {
            RequestChange(offset, isForward: true);
        }
    }

    private void PreviousChangeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (MenuChangeOffset is { } offset)
        {
            RequestChange(offset, isForward: false);
        }
    }

    private void SetChangeMenuItems()
    {
        MenuChangeOffset = IsChangeNavigationEnabled && MouseOver is { Offset: { } offset, HasMarker: true } ? offset : null;

        var visibility = MenuChangeOffset is null ? Visibility.Collapsed : Visibility.Visible;

        NextChangeMenuItem.Visibility = visibility;

        PreviousChangeMenuItem.Visibility = visibility;

        ChangeMenuSeparator.Visibility = visibility;
    }

    private void RequestChange(int offset, bool isForward)
        => ChangeNavigationRequested?.Invoke(this, new HexChangeNavigation(BaseAddress + offset, isForward));

    private int? GetMarkerOffset(Point point)
    {
        if (HexRichTextBlock.GetPositionFromPoint(point) is not { } position)
        {
            return null;
        }

        var offset = HexLayout.FromRunPosition(position.Offset, HexLayout.CharactersPerLine) - 1;

        return MarkerLookup.FindAt(Markers, offset) is null ? null : offset;
    }
}
