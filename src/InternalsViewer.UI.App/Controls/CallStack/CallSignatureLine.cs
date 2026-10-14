using System;
using System.Collections.Generic;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.CallStack;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.CallStack;

public sealed partial class CallSignatureLine : UserControl
{
    private const double TextSize = 11;

    public static readonly DependencyProperty PartsProperty =
        DependencyProperty.Register(nameof(Parts),
                                    typeof(IReadOnlyList<CallSignaturePart>),
                                    typeof(CallSignatureLine),
                                    new PropertyMetadata(null, OnPartsChanged));

    public IReadOnlyList<CallSignaturePart>? Parts
    {
        get => (IReadOnlyList<CallSignaturePart>?)GetValue(PartsProperty);
        set => SetValue(PartsProperty, value);
    }

    public static readonly DependencyProperty IsDecodedProperty =
        DependencyProperty.Register(nameof(IsDecoded),
                                    typeof(bool),
                                    typeof(CallSignatureLine),
                                    new PropertyMetadata(true, OnPartsChanged));

    public bool IsDecoded
    {
        get => (bool)GetValue(IsDecodedProperty);
        set => SetValue(IsDecodedProperty, value);
    }

    public CallSignatureLine()
    {
        Content = Line;
    }

    private StackPanel Line { get; } = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

    private void Build()
    {
        Line.Children.Clear();

        foreach (var part in Parts ?? [])
        {
            Line.Children.Add(Element(part));
        }
    }

    private TextBlock Element(CallSignaturePart part)
    {
        var isDecoded = part.IsDecodedFor(IsDecoded);

        var text = new TextBlock
        {
            FontSize = TextSize,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = part.Kind switch
            {
                CallSignaturePartKind.Function => Resource<Brush>("CppClassBrush"),
                CallSignaturePartKind.Punctuation => Resource<Brush>("SqlPunctuationBrush"),
                CallSignaturePartKind.Name => Resource<Brush>("TextFillColorSecondaryBrush"),
                CallSignaturePartKind.NotCaptured => Resource<Brush>("TextFillColorTertiaryBrush"),
                _ when part.CanFind => Resource<Brush>("AccentTextFillColorPrimaryBrush"),
                _ => Resource<Brush>("TextFillColorPrimaryBrush")
            }
        };

        if (isDecoded && part.DecodedClass is { } className)
        {
            text.Inlines.Add(new Run { Text = className, Foreground = Resource<Brush>("CppClassBrush") });
            text.Inlines.Add(new Run { Text = " " });

            HexText.AddRuns(text.Inlines, part.Decoded!);
        }
        else
        {
            HexText.Apply(text, part.GetText(IsDecoded));
        }

        if (!isDecoded && Resource<FontFamily>("MonospaceFontFamily") is { } monospace)
        {
            text.FontFamily = monospace;
        }

        var alternative = part.Kind != CallSignaturePartKind.Value ? null : isDecoded ? part.Text : part.Decoded is null ? null : part.GetText(true);

        if (part.ToolTip is { } toolTip)
        {
            ToolTipService.SetToolTip(text, alternative is null ? toolTip : $"{toolTip}\n{alternative}");
        }

        if (part is { Kind: CallSignaturePartKind.Value, Value: { } value })
        {
            var menu = new MenuFlyout();

            if (part.Decoded is not null)
            {
                menu.Items.Add(MenuItem("Copy Decoded", () => ClipboardText.Copy(part.GetText(true))));
            }

            menu.Items.Add(MenuItem("Copy Value", () => ClipboardText.Copy(part.Text)));

            if (part.CanFind)
            {
                menu.Items.Add(new MenuFlyoutSeparator());

                menu.Items.Add(MenuItem("Find", () => Finder()?.Find(value)));
            }

            text.ContextFlyout = menu;
        }

        return text;
    }

    private static MenuFlyoutItem MenuItem(string text, Action action)
    {
        var item = new MenuFlyoutItem { Text = text };

        item.Click += (_, _) => action();

        return item;
    }

    private IValueFinder? Finder()
    {
        DependencyObject? current = this;

        while (current is not null)
        {
            if (current is IValueFinder finder)
            {
                return finder;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static T? Resource<T>(string key) where T : class
        => Application.Current.Resources.TryGetValue(key, out var resource) ? resource as T : null;

    private static void OnPartsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((CallSignatureLine)d).Build();
}
