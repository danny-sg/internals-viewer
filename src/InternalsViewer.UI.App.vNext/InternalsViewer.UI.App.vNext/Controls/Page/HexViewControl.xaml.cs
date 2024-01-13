using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Helpers;
using InternalsViewer.UI.App.vNext.Helpers;
using InternalsViewer.UI.App.vNext.Models;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.vNext.Controls.Page
{
    public sealed partial class HexViewControl
    {
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

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SetHexData(e.NewValue as byte[] ?? Array.Empty<byte>(), (HexViewControl)d);
        }

        private static void OnMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            SetHexData(e.NewValue as byte[] ?? Array.Empty<byte>(), (HexViewControl)d);
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

            target.HexRichTextBlock.Blocks.Add(paragraph);

            HighlightMarkers(target);
        }

        private static void HighlightMarkers(HexViewControl target)
        {
            target.HexRichTextBlock.TextHighlighters.Clear();

            if (target.Markers != null)
            {
                foreach (var marker in target.Markers)
                {
                    var start = ToRunPosition(marker.StartPosition);
                    var end = ToRunPosition(marker.EndPosition);

                    var length = end - start;

                    var highlighter = new TextHighlighter
                    {
                        Foreground = new SolidColorBrush(marker.ForeColour.ToWindowsColor()),
                        Background = new SolidColorBrush(marker.BackColour.ToWindowsColor()),
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
    }

}
