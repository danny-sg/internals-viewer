using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using InternalsViewer.UI.App.ViewModels.Page;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Text;
using Microsoft.UI.Text;

namespace InternalsViewer.UI.App.Controls.Page;

public sealed partial class HexViewControl
{
    public class MouseOverInfo(int? offset, Marker? marker)
    {
        public int? Offset { get; } = offset;

        public Marker? Marker { get; } = marker;

        public bool HasMarker => Marker != null;
    }

    // 16 bytes per line is the conventional way of displaying hex
    private const int BytesPerLine = 16;

    // Bytes are represented by 2 characters and a space
    const int CharactersPerByte = 2;

    public HexControlViewModel ViewModel { get; } = new();

    public HexViewControl()
    {
        InitializeComponent();

        SetAddress();

        MouseOver = new(default, default);
    }

    private void SetAddress()
    {
        var stringBuilder = new StringBuilder();

        for (var i = 0; i < PageData.Size / BytesPerLine; i++)
        {
            stringBuilder.AppendLine($"{i * BytesPerLine:X8}");
        }

        AddressTextBlock.Text = stringBuilder.ToString();
    }

    public byte[] Data
    {
        get { return (byte[])GetValue(DataProperty); }
        set { SetValue(DataProperty, value); }
    }

    public static readonly DependencyProperty DataProperty = DependencyProperty
        .Register(nameof(Data),
            typeof(byte[]),
            typeof(HexViewControl),
            new PropertyMetadata(default, OnDataChanged));

    public ObservableCollection<Marker>? Markers
    {
        get { return (ObservableCollection<Marker>)GetValue(MarkersProperty); }
        set { SetValue(MarkersProperty, value); }
    }

    public static readonly DependencyProperty MarkersProperty = DependencyProperty
        .Register(nameof(Data),
            typeof(ObservableCollection<Marker>),
            typeof(HexViewControl),
            new PropertyMetadata(default, OnMarkersChanged));

    public Marker? SelectedMarker
    {
        get => (Marker?)GetValue(SelectedMarkerProperty);
        set => SetValue(SelectedMarkerProperty, value);
    }

    public static readonly DependencyProperty SelectedMarkerProperty
        = DependencyProperty.Register(nameof(SelectedMarker),
            typeof(Marker),
            typeof(HexViewControl),
            new PropertyMetadata(default, OnSelectedMarkerChanged));

    public MouseOverInfo MouseOver
    {
        get => (MouseOverInfo)GetValue(MouseOverProperty);
        set => SetValue(MouseOverProperty, value);
    }

    public static readonly DependencyProperty MouseOverProperty
        = DependencyProperty.Register(nameof(MouseOver),
            typeof(MouseOverInfo),
            typeof(HexViewControl),
            new PropertyMetadata(default, null));

    private static void OnSelectedMarkerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HexViewControl)d;

        if (e.NewValue is Marker marker)
        {
            ScrollToPosition(control, marker.StartPosition);
        }

        SetHexData(control.Data, control);
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        SetHexData(e.NewValue as byte[] ?? Array.Empty<byte>(), (HexViewControl)d);
    }

    private static void OnMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        HighlightMarkers((HexViewControl)d, (ObservableCollection<Marker>)e.NewValue);
    }

    private static void SetHexData(IReadOnlyList<byte> data, HexViewControl target)
    {
        var paragraph = new Paragraph();

        var stringBuilder = new StringBuilder();

        var position = 0;

        for (var line = 0; line < data.Count / BytesPerLine; line++)
        {
            for (var byteIndex = 0; byteIndex < BytesPerLine; byteIndex++)
            {
                if (position == target.SelectedMarker?.StartPosition)
                {
                    // Flush current run, replace with selection inline
                    paragraph.Inlines.Add(FlushRun(stringBuilder));
                }

                stringBuilder.Append(StringHelpers.ToHexString(data[position]));

                if (position == target.SelectedMarker?.EndPosition)
                {
                    // Flush current run with selection formatting
                    paragraph.Inlines.Add(FlushSelectionRun(stringBuilder, target.SelectedMarker));
                }

                // Add a space between bytes, but not for the last byte of the line
                if (byteIndex != 15)
                {
                    stringBuilder.Append(" ");
                }

                position++;
            }

            stringBuilder.Append(Environment.NewLine);
        }

        paragraph.Inlines.Add(FlushRun(stringBuilder));

        target.HexRichTextBlock.Blocks.Clear();
        target.HexRichTextBlock.Blocks.Add(paragraph);

        HighlightMarkers(target, target.Markers);
    }

    private static Inline FlushRun(StringBuilder stringBuilder)
    {
        var run = new Run { Text = stringBuilder.ToString() };

        stringBuilder.Clear();

        return run;
    }

    private static Inline FlushSelectionRun(StringBuilder stringBuilder, Marker marker)
    {
        var run = new Run { Text = stringBuilder.ToString(), TextDecorations = TextDecorations.Underline, FontWeight = FontWeights.Bold };

        stringBuilder.Clear();

        return run;
    }

    private static Inline FlushSelectionContainerRun(StringBuilder stringBuilder, Marker marker)
    {
        var container = new InlineUIContainer();

        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Colors.Navy),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(marker.BackColour)
        };

        border.VerticalAlignment = VerticalAlignment.Top;

        var textRun = new TextBlock { Text = stringBuilder.ToString() };

        textRun.Foreground = new SolidColorBrush(marker.ForeColour);

        textRun.TextWrapping = TextWrapping.Wrap;

        container.Child = border;

        border.Child = textRun;

        stringBuilder.Clear();

        return container;
    }

    private static void HighlightMarkers(HexViewControl target, ObservableCollection<Marker>? markers)
    {
        target.HexRichTextBlock.TextHighlighters.Clear();

        void Highlight(Marker source)
        {
            var start = ToRunPosition(source.StartPosition);
            var end = ToRunPosition(source.EndPosition + 1) - 1;

            var length = end - start;

            var foregroundColour = source.ForeColour;
            var backgroundColour = source.BackColour;

            var highlighter = new TextHighlighter
            {
                Foreground = new SolidColorBrush(foregroundColour),
                Background = new SolidColorBrush(backgroundColour),

                Ranges = { new TextRange(start, length) }
            };

            target.HexRichTextBlock.TextHighlighters.Add(highlighter);

            if (source.Children.Any())
            {
                foreach (var child in source.Children)
                {
                    Highlight(child);
                }
            }
        }

        if (markers != null)
        {
            foreach (var marker in markers)
            {
                Highlight(marker);
            }

            target.ScrollViewer.ScrollToVerticalOffset(0);
        }
    }

    /// <summary>
    /// Scrolls the hex view to the specified offset position
    /// </summary>
    /// <remarks>
    /// There doesn't seem to be a ScrollToPosition type method built into the RichTextBlock control.
    /// 
    /// This works by taking the height of the text and dividing by the know number of lines to get the height of each line, then 
    /// multiplying by the calculated line number to get the position.
    /// </remarks>
    private static void ScrollToPosition(HexViewControl target, int position)
    {
        const int totalLines = PageData.Size / BytesPerLine;

        var positionLineNumber = (position / BytesPerLine) - 1;

        var heightPerLine = target.HexRichTextBlock.ActualHeight / totalLines;

        var scrollPosition = positionLineNumber * heightPerLine;

        target.ScrollViewer.ScrollToVerticalOffset(scrollPosition);
    }

    /// <summary>
    /// Converts a byte position to a position in the hex text block
    /// </summary>
    private static int ToRunPosition(int position)
    {
        // Bytes are represented by 2 characters and a space
        const int charactersPerByte = 3;

        var lineNumber = position / BytesPerLine;

        return position * charactersPerByte + lineNumber * (Environment.NewLine.Length - 1);
    }

    /// <summary>
    /// Converts a position in the hex text block to a byte position
    /// </summary>
    private static int FromRunPosition(int position, decimal charactersPerLine)
    {
        var lineNumber = (int)Math.Floor(position / charactersPerLine);

        var linePosition = position % charactersPerLine;
        var bytePosition = Math.Round(linePosition / (CharactersPerByte + 1));

        return lineNumber * BytesPerLine + (int)bytePosition;
    }

    private void HexRichTextBlock_SelectionChanged(object sender, RoutedEventArgs e)
    {
        var rect = HexRichTextBlock.SelectionEnd.GetCharacterRect(LogicalDirection.Forward);

        SelectionInfoPopup.HorizontalOffset = rect.X + 4;
        SelectionInfoPopup.VerticalOffset = rect.Y;

        ViewModel.StartOffset = FromRunPosition(HexRichTextBlock.SelectionStart.Offset,
                                                BytesPerLine * CharactersPerByte // Bytes
                                                 + BytesPerLine - 1               // Spaces in between bytes (except last byte)
                                                 + Environment.NewLine.Length);

        ViewModel.EndOffset = FromRunPosition(HexRichTextBlock.SelectionEnd.Offset,
                                                BytesPerLine * CharactersPerByte // Bytes
                                              + BytesPerLine - 1               // Spaces in between bytes (except last byte)
                                              + Environment.NewLine.Length);

        ViewModel.SelectedText = HexRichTextBlock.SelectedText;
    }

    private void HexRichTextBlock_LostFocus(object sender, RoutedEventArgs e)
    {
        SelectionInfoPopup.IsOpen = false;
    }

    private void HexRichTextBlock_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var position = HexRichTextBlock.GetPositionFromPoint(e.GetCurrentPoint(HexRichTextBlock).Position);

        if (position != null)
        {
            var offset = FromRunPosition(position.Offset, BytesPerLine * CharactersPerByte // Bytes
                                                          + BytesPerLine - 1               // Spaces in between bytes (except last byte)
                                                          + Environment.NewLine.Length) - 1;

            var marker = Markers?.FirstOrDefault(m => m.StartPosition <= offset && m.EndPosition >= offset);

            MouseOver = new(offset, marker);
        }
    }

    private void HexRichTextBlock_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        ChangeCursor(InputSystemCursor.Create(InputSystemCursorShape.Arrow));

        e.Handled = true;
    }

    private void HexRichTextBlock_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        MouseOver = new(default, default);
    }
}