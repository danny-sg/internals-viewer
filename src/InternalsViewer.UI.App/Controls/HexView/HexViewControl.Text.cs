using System;
using System.Collections.Generic;
using System.Text;
using InternalsViewer.Internals.Engine.Pages;
using InternalsViewer.UI.App.Models;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.HexView;

public sealed partial class HexViewControl
{
    /// <summary>
    /// Builds the address column for whatever length of data is shown, offset by <see cref="BaseAddress"/>
    /// </summary>
    /// <remarks>
    /// A page is always the same size, however a columnstore blob region is not, and starts part way into the blob it
    /// was taken from, so the address has to say where in that blob a line sits rather than where in the slice.
    /// </remarks>
    private void SetAddress()
    {
        var length = Data is { Length: > 0 } ? Data.Length : PageData.Size;

        var lineCount = (length + BytesPerLine - 1) / BytesPerLine;

        // A held drag leaves the bytes where they were, so the addresses are the only sign of where it has reached
        var baseAddress = IsScrolling ? _pendingScrollLine * BytesPerLine : BaseAddress;

        var stringBuilder = new StringBuilder();

        for (var i = 0; i < lineCount; i++)
        {
            // Separator rather than terminator - a trailing newline renders as an extra blank address line
            if (i > 0)
            {
                stringBuilder.AppendLine();
            }

            stringBuilder.Append($"{baseAddress + (i * BytesPerLine):X8}");
        }

        AddressTextBlock.Text = stringBuilder.ToString();

        SetAreas(baseAddress, lineCount);
    }

    /// <summary>
    /// Names the areas the window covers, which is the only map of the blob a held drag has to steer by
    /// </summary>
    /// <remarks>
    /// A name is written where its area starts rather than against every line, so the column reads as a map of
    /// where the window has reached rather than a wall of repeated text.
    /// </remarks>
    private void SetAreas(int baseAddress, int lineCount)
    {
        AreaOverlay.Visibility = IsScrolling && Areas is { Count: > 0 } ? Visibility.Visible : Visibility.Collapsed;

        AreaOverlay.Children.Clear();

        if (AreaOverlay.Visibility == Visibility.Collapsed || Areas is not { } areas)
        {
            return;
        }

        foreach (var label in HexAreas.GetLabels(areas, baseAddress, lineCount))
        {
            AreaOverlay.Children.Add(AreaLabel(label.Name, label.Line));
        }
    }

    /// <summary>
    /// One area name, sitting over the bytes on the line its area starts at
    /// </summary>
    private static Border AreaLabel(string name, int line) => new()
    {
        Background = new SolidColorBrush(Colors.White),
        CornerRadius = new CornerRadius(2),
        Padding = new Thickness(4, 0, 4, 0),
        Margin = new Thickness(0, line * LineHeight, 12, 0),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        Child = new TextBlock
        {
            Text = name,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.Black),
            LineHeight = LineHeight
        }
    };

    /// <summary>
    /// Named stretches of the data, in order, each running until the next one starts
    /// </summary>
    public IReadOnlyList<HexArea>? Areas
    {
        get => (IReadOnlyList<HexArea>?)GetValue(AreasProperty);
        set => SetValue(AreasProperty, value);
    }

    public static readonly DependencyProperty AreasProperty
        = DependencyProperty.Register(nameof(Areas),
            typeof(IReadOnlyList<HexArea>),
            typeof(HexViewControl),
            new PropertyMetadata(null));

    /// <summary>
    /// Offset the address column counts from, so a slice of a larger structure shows its true offsets
    /// </summary>
    public int BaseAddress
    {
        get => (int)GetValue(BaseAddressProperty);
        set => SetValue(BaseAddressProperty, value);
    }

    public static readonly DependencyProperty BaseAddressProperty
        = DependencyProperty.Register(nameof(BaseAddress),
            typeof(int),
            typeof(HexViewControl),
            new PropertyMetadata(0, OnBaseAddressChanged));

    private static void OnBaseAddressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((HexViewControl)d).InvalidateHexData();

    private bool _isHexRebuildPending;

    /// <summary>
    /// Asks for one rebuild of the hex text at the end of the current pass, however many properties moved
    /// </summary>
    /// <remarks>
    /// Moving the window sets the base address, the data and the selected marker, and each of those on its own used
    /// to rebuild the whole run list. Coalescing them leaves one rebuild per window move rather than three.
    /// </remarks>
    private void InvalidateHexData()
    {
        if (_isHexRebuildPending)
        {
            return;
        }

        _isHexRebuildPending = true;

        DispatcherQueue.TryEnqueue(() =>
        {
            _isHexRebuildPending = false;

            SetHexData(Data ?? [], this);

            SetAddress();
        });
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
            new PropertyMetadata(null, OnDataChanged));

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((HexViewControl)d).InvalidateHexData();

    private static void SetHexData(IReadOnlyList<byte> data, HexViewControl target)
    {
        var paragraph = new Paragraph();

        paragraph.Inlines.Add(new Run { Text = HexTextBuilder.Build(data, BytesPerLine) });

        target.HexRichTextBlock.Blocks.Clear();
        target.HexRichTextBlock.Blocks.Add(paragraph);

        HighlightMarkers(target, target.Markers);

        target.DrawChangeSpans();

        target.DrawSelectionMask();
    }
}
