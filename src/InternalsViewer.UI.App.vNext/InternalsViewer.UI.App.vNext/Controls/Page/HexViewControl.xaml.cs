using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using System.Text;
using Windows.UI;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.UI.App.vNext.Controls.Allocation;
using InternalsViewer.UI.App.vNext.Helpers;
using InternalsViewer.UI.App.vNext.Models;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Controls.Primitives;
using InternalsViewer.Internals.Converters;
using System.Globalization;
using InternalsViewer.UI.App.vNext.ViewModels.Page;

namespace InternalsViewer.UI.App.vNext.Controls.Page
{
    public sealed partial class HexViewControl
    {
        public HexControlViewModel ViewModel { get; } = new();

        public HexViewControl()
        {
            InitializeComponent();

            SetAddress();
        }

        private void SetAddress()
        {
            var stringBuilder = new StringBuilder();

            for (var i = 0; i < PageData.Size / 16; i++)
            {
                stringBuilder.AppendLine($"{i * 16:X8}");
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
                new PropertyMetadata(default, OnSelectedRangeChanged));

        private static void OnSelectedRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (HexViewControl)d;

            HighlightMarkers(control, control.Markers);
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

            for (var line = 0; line < data.Count / 16; line++)
            {
                for (var column = 0; column < 16; column++)
                {
                    stringBuilder.Append(StringHelpers.ToHexString(data[position]));

                    if (column != 15)
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
            }
        }

        private static int ToRunPosition(int position)
        {
            var lineNumber = position / 16;

            return position * 3 + lineNumber * (Environment.NewLine.Length - 1);
        }

        private void HexRichTextBlock_SelectionChanged(object sender, RoutedEventArgs e)
        {
            var rect = HexRichTextBlock.SelectionEnd.GetCharacterRect(LogicalDirection.Forward);

            SelectionInfoPopup.HorizontalOffset = rect.X + 4;
            SelectionInfoPopup.VerticalOffset = rect.Y;

            ViewModel.StartOffset = ToRunPosition(HexRichTextBlock.SelectionStart.Offset);
            ViewModel.EndOffset = ToRunPosition(HexRichTextBlock.SelectionEnd.Offset);

            ViewModel.SelectedText = HexRichTextBlock.SelectedText;
        }

        private void HexRichTextBlock_LostFocus(object sender, RoutedEventArgs e)
        {
            SelectionInfoPopup.IsOpen = false;
        }

    }

}
