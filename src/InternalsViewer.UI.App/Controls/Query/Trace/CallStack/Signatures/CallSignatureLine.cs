using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.CallStack.Signatures.Enums;
using InternalsViewer.UI.App.Models.Query.Trace.CallStack.Signatures;
using InternalsViewer.UI.App.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.Query.Trace.CallStack.Signatures;

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

        Loaded += OnLoaded;

        Unloaded += (_, _) => Settings.PropertyChanged -= OnSettingsPropertyChanged;
    }

    public event EventHandler<CallSignaturePart>? ValueSelected;

    public event EventHandler<CallSignaturePart>? MemorySelected;

    private StackPanel Line { get; } = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

    private SettingsViewModel Settings { get; } = App.GetService<SettingsViewModel>();

    private bool IsDecoded { get; set; }

    private void Build()
    {
        IsDecoded = Settings.DecodeValues;

        Line.Children.Clear();

        foreach (var part in Parts ?? [])
        {
            Line.Children.Add(Element(part));

            if (part is { IsMemoryTracked: true, MemoryTarget: null })
            {
                Line.Children.Add(MemoryIcon(part));
            }
        }
    }

    private TextBlock Element(CallSignaturePart part)
    {
        var isDecoded = part.IsDecodedFor(IsDecoded);

        var text = new TextBlock
        {
            FontSize = TextSize,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = AppResources.Get<Brush>(part.Kind switch
            {
                CallSignaturePartKind.Function => "CppClassBrush",
                CallSignaturePartKind.Punctuation => "SqlPunctuationBrush",
                CallSignaturePartKind.Name => "TextFillColorSecondaryBrush",
                CallSignaturePartKind.NotCaptured => "TextFillColorTertiaryBrush",
                _ when part.CanFind => "AccentTextFillColorPrimaryBrush",
                _ => "TextFillColorPrimaryBrush"
            })
        };

        if (part.ShowsTypeFor(IsDecoded))
        {
            SignatureRuns.AddType(text.Inlines, part.TypeName!);

            text.Inlines.Add(new Run { Text = " " });
        }

        if (part.Kind == CallSignaturePartKind.Function)
        {
            SignatureRuns.AddFunction(text.Inlines, part.Text);
        }
        else
        {
            SignatureRuns.AddValue(text.Inlines, part, IsDecoded);
        }

        if (!isDecoded && AppResources.Get<FontFamily>("MonospaceFontFamily") is { } monospace)
        {
            text.FontFamily = monospace;
        }

        if (ReferenceEquals(part, HighlightedPart) && text.Inlines.OfType<Run>().Sum(r => r.Text.Length) is > 0 and var length)
        {
            SearchHighlight.Highlight(text, [(0, length)]);
        }

        var alternative = part.Kind != CallSignaturePartKind.Value ? null
                          : isDecoded ? part.Text
                          : part.Decoded is null ? null : part.GetText(true);

        if (part.ToolTip is { } toolTip)
        {
            ToolTipService.SetToolTip(text, alternative is null ? toolTip : $"{toolTip}\n{alternative}");
        }

        if (part is { Kind: CallSignaturePartKind.Value, Value: { } value })
        {
            text.Tapped += (_, _) => ValueSelected?.Invoke(this, part);

            text.ContextFlyout = ValueMenu(part, value);
        }

        return text;
    }

    private FontIcon MemoryIcon(CallSignaturePart part)
    {
        var icon = new FontIcon
        {
            Glyph = "\uE950",
            FontSize = TextSize,
            Margin = new Thickness(3, 0, 1, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = AppResources.Get<Brush>("TextFillColorSecondaryBrush")
        };

        ToolTipService.SetToolTip(icon, "Show Memory");

        icon.Tapped += (_, e) =>
        {
            e.Handled = true;

            MemorySelected?.Invoke(this, part);
        };

        return icon;
    }

    private MenuFlyout ValueMenu(CallSignaturePart part, ulong value)
    {
        var menu = new MenuFlyout();

        if (part.Decoded is not null)
        {
            menu.Items.Add(MenuItems.Create("Copy Decoded", () => ClipboardText.Copy(part.GetText(true))));
        }

        menu.Items.Add(MenuItems.Create("Copy Value", () => ClipboardText.Copy(part.Text)));

        if (part.CanFind)
        {
            menu.Items.Add(new MenuFlyoutSeparator());

            menu.Items.Add(MenuItems.Create("Find", () => LayoutHelpers.FindParent<IReferenceFinder>(this)?.FindReferences(value)));
        }

        return menu;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Settings.PropertyChanged -= OnSettingsPropertyChanged;

        Settings.PropertyChanged += OnSettingsPropertyChanged;

        if (IsDecoded != Settings.DecodeValues)
        {
            Build();
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.DecodeValues))
        {
            Build();
        }
    }

    private static void OnPartsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((CallSignatureLine)d).Build();
}
