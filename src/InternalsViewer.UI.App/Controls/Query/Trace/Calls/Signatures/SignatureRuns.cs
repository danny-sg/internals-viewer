using System;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Models.Query.Trace.Calls.Signatures.Enums;
using InternalsViewer.UI.App.Models.Query.Trace.Calls.Signatures;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using InternalsViewer.Query.FullTrace.Symbols;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Calls.Signatures;

public static class SignatureRuns
{
    public static readonly DependencyProperty ArgumentProperty
        = DependencyProperty.RegisterAttached("Argument",
                                              typeof(CallSignaturePart),
                                              typeof(SignatureRuns),
                                              new PropertyMetadata(null, OnArgumentChanged));

    public static CallSignaturePart? GetArgument(DependencyObject element) => (CallSignaturePart?)element.GetValue(ArgumentProperty);

    public static void SetArgument(DependencyObject element, CallSignaturePart? value) => element.SetValue(ArgumentProperty, value);

    public static void AddFunction(InlineCollection inlines, string function)
    {
        var separator = SymbolHelper.FindClassMethodSeparator(function);

        if (separator > 0)
        {
            inlines.Add(new Run { Text = function[..(separator + 2)], Foreground = AppResources.Get<Brush>("CppClassBrush") });
        }

        var method = separator > 0 ? function[(separator + 2)..] : function;

        inlines.Add(new Run { Text = method, Foreground = AppResources.Get<Brush>("CppFunctionBrush") });
    }

    public static void AddValue(InlineCollection inlines, CallSignaturePart part, bool decode)
    {
        if (!part.IsDecodedFor(decode))
        {
            HexText.AddRuns(inlines, part.Text);

            return;
        }

        if (part.DecodedString is { } decodedString)
        {
            StringText.AddRuns(inlines, decodedString);

            return;
        }

        if (part.DecodedClass is { } className)
        {
            var brush = part.IsLocal ? "CppLocalBrush" : "CppClassBrush";

            inlines.Add(new Run { Text = className, Foreground = AppResources.Get<Brush>(brush) });

            inlines.Add(new Run { Text = " " });
        }

        HexText.AddRuns(inlines, part.Decoded!);
    }

    public static void AddType(InlineCollection inlines, string type)
    {
        foreach (var token in SignatureTokenizer.Tokenize(type))
        {
            var brush = token.Type switch
            {
                SignatureTokenType.Keyword 
                    => "SqlKeywordBrush",
                SignatureTokenType.Punctuation
                    => "SqlPunctuationBrush",
                _ => "SqlIdentifierBrush"
            };

            inlines.Add(new Run { Text = token.Text, Foreground = AppResources.Get<Brush>(brush) });
        }
    }

    private static InlineCollection? Cleared(DependencyObject d)
    {
        if (d is not TextBlock text)
        {
            return null;
        }

        text.Inlines.Clear();

        return text.Inlines;
    }

    private static void OnArgumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (Cleared(d) is not { } inlines || e.NewValue is not CallSignaturePart part)
        {
            return;
        }

        if (part.Location is { } location)
        {
            inlines.Add(new Run { Text = $"{location}  ", Foreground = AppResources.Get<Brush>("TextFillColorSecondaryBrush") });
        }

        if (part.DeclaredType is { } type)
        {
            AddType(inlines, type);

            inlines.Add(new Run { Text = "  " });
        }

        AddValue(inlines, part, decode: part.MemoryTarget is null);
    }
}
