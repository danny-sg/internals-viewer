using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.Internals.Helpers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

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

            for (var i = 0; i <= (PageData.Size / 16); i++)
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

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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
        }
    }
}
