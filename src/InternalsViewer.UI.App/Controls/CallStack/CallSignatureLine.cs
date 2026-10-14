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

    public static readonly DependencyProperty TextSizeProperty =
        DependencyProperty.Register(nameof(TextSize),
                                    typeof(double),
                                    typeof(CallSignatureLine),
                                    new PropertyMetadata(11d, OnPartsChanged));

    public double TextSize
    {
        get => (double)GetValue(TextSizeProperty);
        set => SetValue(TextSizeProperty, value);
    }

    public static readonly DependencyProperty HighlightedPartProperty =
        DependencyProperty.Register(nameof(HighlightedPart),
                                    typeof(CallSignaturePart),
                                    typeof(CallSignatureLine),
                                    new PropertyMetadata(null, OnPartsChanged));

    public CallSignaturePart? HighlightedPart
    {
        get => (CallSignaturePart?)GetValue(HighlightedPartProperty);
        set => SetValue(HighlightedPartProperty, value);
    }

    public CallSignatureLine()
    {
        Content = Line;
    }

    public event EventHandler<CallSignaturePart>? ValueSelected;

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

        var typeName = part.ShowsTypeFor(IsDecoded) ? part.TypeName : null;

        if (typeName is not null)
        {
            SignatureRuns.AddType(text.Inlines, typeName);

            text.Inlines.Add(new Run { Text = " " });
        }

        if (part.Kind == CallSignaturePartKind.Function)
        {
            SignatureRuns.AddFunction(text.Inlines, part.Text);
        }
        else if (isDecoded && (part.DecodedString is not null || part.DecodedClass is not null))
        {
            SignatureRuns.AddDecoded(text.Inlines, part);
        }
        else if (typeName is not null)
        {
            HexText.AddRuns(text.Inlines, part.GetText(IsDecoded));
        }
        else
        {
            HexText.Apply(text, part.GetText(IsDecoded));
        }

        if (!isDecoded && Resource<FontFamily>("MonospaceFontFamily") is { } monospace)
        {
            text.FontFamily = monospace;
        }

        if (ReferenceEquals(part, HighlightedPart))
        {
            Highlight(text);
        }

        var alternative = part.Kind != CallSignaturePartKind.Value ? null : isDecoded ? part.Text : part.Decoded is null ? null : part.GetText(true);

        if (part.ToolTip is { } toolTip)
        {
            ToolTipService.SetToolTip(text, alternative is null ? toolTip : $"{toolTip}\n{alternative}");
        }

        if (part is { Kind: CallSignaturePartKind.Value, Value: { } value })
        {
            text.Tapped += (_, _) => ValueSelected?.Invoke(this, part);

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

    private static void Highlight(TextBlock text)
    {
        var length = 0;

        foreach (var inline in text.Inlines)
        {
            length += inline is Run run ? run.Text.Length : 0;
        }

        length = length > 0 ? length : text.Text.Length;

        if (length == 0)
        {
            return;
        }

        var highlighter = new TextHighlighter
        {
            Background = Resource<Brush>("SearchHighlightBrush"),
            Foreground = Resource<Brush>("SearchHighlightForegroundBrush")
        };

        highlighter.Ranges.Add(new TextRange { StartIndex = 0, Length = length });

        text.TextHighlighters.Add(highlighter);
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
