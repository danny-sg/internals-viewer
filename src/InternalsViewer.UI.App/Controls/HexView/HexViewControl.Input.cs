using System;
using Windows.Foundation;
using InternalsViewer.UI.App.Models;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;

namespace InternalsViewer.UI.App.Controls.HexView;

public sealed partial class HexViewControl
{
    /// <summary>
    /// How far a pointer can move between press and release and still count as a click rather than a drag
    /// </summary>
    private const double ClickSlop = 3;

    public static readonly DependencyProperty MouseOverProperty
        = DependencyProperty.Register(nameof(MouseOver),
            typeof(MouseOverInfo),
            typeof(HexViewControl),
            new PropertyMetadata(null, null));

    public MouseOverInfo MouseOver
    {
        get => (MouseOverInfo)GetValue(MouseOverProperty);
        set => SetValue(MouseOverProperty, value);
    }

    private Point? _pressPoint;

    private void HexRichTextBlock_SelectionChanged(object sender, RoutedEventArgs e)
    {
        var rect = HexRichTextBlock.SelectionEnd.GetCharacterRect(LogicalDirection.Forward);

        SelectionInfoPopup.HorizontalOffset = rect.X + 4;
        SelectionInfoPopup.VerticalOffset = rect.Y;

        ViewModel.StartOffset = HexLayout.FromRunPosition(HexRichTextBlock.SelectionStart.Offset,
                                                          HexLayout.CharactersPerLine);

        ViewModel.EndOffset = HexLayout.FromRunPosition(HexRichTextBlock.SelectionEnd.Offset,
                                                        HexLayout.CharactersPerLine);

        ViewModel.SelectedText = HexRichTextBlock.SelectedText;
    }

    private void HexRichTextBlock_LostFocus(object sender, RoutedEventArgs e)
    {
        SelectionInfoPopup.IsOpen = false;
    }

    private void HexRichTextBlock_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var position = HexRichTextBlock.GetPositionFromPoint(e.GetCurrentPoint(HexRichTextBlock).Position);

        if (position != null)
        {
            var offset = HexLayout.FromRunPosition(position.Offset, HexLayout.CharactersPerLine) - 1;

            MouseOver = new(offset, MarkerLookup.FindAt(Markers, offset));
        }
    }

    private void OnHexPointerPressed(object sender, PointerRoutedEventArgs e)
        => _pressPoint = e.GetCurrentPoint(HexRichTextBlock).Position;

    /// <summary>
    /// Clears the selection the mask was drawn for, a drag being left alone so text can still be selected
    /// </summary>
    private void OnHexPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var pressed = _pressPoint;

        _pressPoint = null;

        if (SelectedMarker is null || pressed is not { } start)
        {
            return;
        }

        var released = e.GetCurrentPoint(HexRichTextBlock).Position;

        if (Math.Abs(released.X - start.X) > ClickSlop || Math.Abs(released.Y - start.Y) > ClickSlop)
        {
            return;
        }

        SelectedMarker = null;
    }

    private void HexRichTextBlock_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        ChangeCursor(InputSystemCursor.Create(InputSystemCursorShape.Arrow));

        e.Handled = true;
    }

    private void HexRichTextBlock_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        MouseOver = new MouseOverInfo(null, null);
    }

    public sealed class MouseOverInfo(int? offset, Marker? marker)
    {
        public int? Offset { get; } = offset;

        public Marker? Marker { get; } = marker;

        public bool HasMarker => Marker != null;
    }
}
