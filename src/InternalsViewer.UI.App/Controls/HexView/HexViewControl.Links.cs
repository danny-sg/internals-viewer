using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using InternalsViewer.UI.App.Models;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;

namespace InternalsViewer.UI.App.Controls.HexView;

public sealed partial class HexViewControl
{
    private const double LinkUnderlineThickness = 1;

    private const double LinkUnderlineInset = 2;

    private const double LinkTipGap = 12;

    private const string OpenLinkHint = "Ctrl+Click To open";

    public static readonly DependencyProperty LinksProperty
        = DependencyProperty.Register(nameof(Links),
            typeof(IReadOnlyList<HexLink>),
            typeof(HexViewControl),
            new PropertyMetadata(null, OnLinksChanged));

    public IReadOnlyList<HexLink>? Links
    {
        get => (IReadOnlyList<HexLink>?)GetValue(LinksProperty);
        set => SetValue(LinksProperty, value);
    }

    private HexLink? HoveredLink { get; set; }

    private HexLink? GetLinkAt(int offset) => Links?.FirstOrDefault(l => l.Contains(offset));

    private void DrawLinks()
    {
        LinkCanvas.Children.Clear();

        var length = Data?.Length ?? 0;

        if (Links is not { Count: > 0 } links || HexRichTextBlock.Blocks.Count == 0 || length == 0)
        {
            return;
        }

        var metrics = EnsureMetrics();

        var lineHeight = GetLineHeight();

        var brush = GetBrush(Colors.LightGray);

        foreach (var link in links)
        {
            var end = link.Offset + link.Length - 1;

            if (link.Length <= 0 || link.Offset < 0 || end >= length)
            {
                continue;
            }

            foreach (var bounds in metrics.GetSpanRects(link.Offset, end, lineHeight))
            {
                var underline = new Rectangle
                {
                    Width = bounds.Width - 2,
                    Height = LinkUnderlineThickness,
                    Fill = brush,
                    IsHitTestVisible = false
                };

                Canvas.SetLeft(underline, bounds.Left + 1);

                Canvas.SetTop(underline, bounds.Bottom - LinkUnderlineInset);

                LinkCanvas.Children.Add(underline);
            }
        }
    }

    private void UpdateHoveredLink(int? offset, Point position)
    {
        var link = offset is { } hovered ? GetLinkAt(hovered) : null;

        if (link is null)
        {
            if (HoveredLink is not null)
            {
                ChangeCursor(InputSystemCursor.Create(InputSystemCursorShape.Arrow));
            }

            HoveredLink = null;

            LinkTipPopup.IsOpen = false;

            return;
        }

        if (!ReferenceEquals(link, HoveredLink))
        {
            ChangeCursor(InputSystemCursor.Create(InputSystemCursorShape.Hand));

            LinkTipText.Text = $"{link.Text}{Environment.NewLine}{OpenLinkHint}";
        }

        HoveredLink = link;

        LinkTipPopup.HorizontalOffset = position.X + LinkTipGap;

        LinkTipPopup.VerticalOffset = position.Y + LinkTipGap;

        LinkTipPopup.IsOpen = true;
    }

    private void SetLinkMenuItem()
    {
        var link = HoveredLink;

        OpenLinkMenuItem.Visibility = link is null ? Visibility.Collapsed : Visibility.Visible;

        OpenLinkSeparator.Visibility = OpenLinkMenuItem.Visibility;

        OpenLinkMenuItem.Text = link?.Command ?? string.Empty;

        LinkTipPopup.IsOpen = false;
    }

    private void OpenLinkMenuItem_Click(object sender, RoutedEventArgs e) => HoveredLink?.Open();

    private bool OpenLinkAt(int offset)
    {
        if (GetLinkAt(offset) is not { } link)
        {
            return false;
        }

        link.Open();

        return true;
    }

    private static void OnLinksChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HexViewControl)d;

        if (control.HoveredLink is { } hovered && control.Links?.Contains(hovered) != true)
        {
            control.HoveredLink = null;

            control.LinkTipPopup.IsOpen = false;
        }

        control.DrawLinks();
    }
}
