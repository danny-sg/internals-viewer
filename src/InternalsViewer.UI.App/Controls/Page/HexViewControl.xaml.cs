using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using System.Text;
using Windows.UI;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using InternalsViewer.UI.App.ViewModels.Page;

namespace InternalsViewer.UI.App.Controls.Page
{
    public sealed partial class HexViewControl
    {
        // 16 bytes per line is the conventional way of displaying hex
        private const int BytesPerLine = 16;

        public HexControlViewModel ViewModel { get; } = new();

        public HexViewControl()
        {
            InitializeComponent();

            SetAddress();
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

        private static void OnSelectedMarkerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (HexViewControl)d;

            HighlightMarkers(control, control.Markers);

            if(e.NewValue is Marker marker)
            {
                ScrollToPosition(control, marker.StartPosition);
            }   
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

            var run = new Run();

            var stringBuilder = new StringBuilder();

            var position = 0;

            for (var line = 0; line < data.Count / BytesPerLine; line++)
            {
                for (var byteIndex = 0; byteIndex < BytesPerLine; byteIndex++)
                {
                    stringBuilder.Append(StringHelpers.ToHexString(data[position]));

                    if (byteIndex != 15)
                    {
                        stringBuilder.Append(" ");
                    }

                    position++;
                }

                stringBuilder.Append(Environment.NewLine);
            }

            run.Text = stringBuilder.ToString();
            paragraph.Inlines.Add(run);

            target.HexRichTextBlock.Blocks.Clear();
            target.HexRichTextBlock.Blocks.Add(paragraph);

            HighlightMarkers(target, target.Markers);
        }

        private static void HighlightMarkers(HexViewControl target, ObservableCollection<Marker>? markers)
        {
            target.HexRichTextBlock.TextHighlighters.Clear();

            if (markers != null)
            {
                foreach (var marker in markers)
                {
                    var start = ToRunPosition(marker.StartPosition);
                    var end = ToRunPosition(marker.EndPosition + 1) - 1;

                    var length = end - start;

                    Color foregroundColour;
                    Color backgroundColour;

                    if (marker == target.SelectedMarker)
                    {
                        foregroundColour = Color.FromArgb(255, 255, 255, 255);
                        backgroundColour = target.HexRichTextBlock.SelectionHighlightColor.Color;
                    }
                    else
                    {
                        foregroundColour = marker.ForeColour.ToWindowsColor();
                        backgroundColour = marker.BackColour.ToWindowsColor();
                    }

                    var highlighter = new TextHighlighter
                    {
                        Foreground = new SolidColorBrush(foregroundColour),
                        Background = new SolidColorBrush(backgroundColour),

                        Ranges = { new TextRange(start, length) }
                    };

                    target.HexRichTextBlock.TextHighlighters.Add(highlighter);
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

            var positionLineNumber = position / BytesPerLine + 1;

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
        private static int FromRunPosition(int position)
        {
            // Bytes are represented by 2 characters and a space
            const int charactersPerByte = 3;

            var charactersPerLine = BytesPerLine * charactersPerByte + Environment.NewLine.Length;

            var lineNumber = position / charactersPerLine;
            var linePosition = position % charactersPerLine;
            var bytePosition = linePosition / charactersPerByte;

            return lineNumber * BytesPerLine + bytePosition;
        }

        private void HexRichTextBlock_SelectionChanged(object sender, RoutedEventArgs e)
        {
            var rect = HexRichTextBlock.SelectionEnd.GetCharacterRect(LogicalDirection.Forward);

            SelectionInfoPopup.HorizontalOffset = rect.X + 4;
            SelectionInfoPopup.VerticalOffset = rect.Y;

            ViewModel.StartOffset = FromRunPosition(HexRichTextBlock.SelectionStart.Offset);
            ViewModel.EndOffset = FromRunPosition(HexRichTextBlock.SelectionEnd.Offset);

            ViewModel.SelectedText = HexRichTextBlock.SelectedText;
        }

        private void HexRichTextBlock_LostFocus(object sender, RoutedEventArgs e)
        {
            SelectionInfoPopup.IsOpen = false;
        }

    }

}
