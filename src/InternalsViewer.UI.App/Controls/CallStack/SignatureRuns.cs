using System;
using InternalsViewer.UI.App.Models.Query.CallStack;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.CallStack;

public static class SignatureRuns
{
    public static readonly DependencyProperty FunctionProperty
        = DependencyProperty.RegisterAttached("Function",
                                              typeof(string),
                                              typeof(SignatureRuns),
                                              new PropertyMetadata(null, OnFunctionChanged));

    public static string? GetFunction(DependencyObject element) => (string?)element.GetValue(FunctionProperty);

    public static void SetFunction(DependencyObject element, string? value) => element.SetValue(FunctionProperty, value);

    public static readonly DependencyProperty ArgumentProperty
        = DependencyProperty.RegisterAttached("Argument",
                                              typeof(CallSignaturePart),
                                              typeof(SignatureRuns),
                                              new PropertyMetadata(null, OnArgumentChanged));

    public static CallSignaturePart? GetArgument(DependencyObject element) => (CallSignaturePart?)element.GetValue(ArgumentProperty);

    public static void SetArgument(DependencyObject element, CallSignaturePart? value) => element.SetValue(ArgumentProperty, value);

    public static void AddFunction(InlineCollection inlines, string function)
    {
        var separator = function.LastIndexOf("::", StringComparison.Ordinal);

        if (separator > 0)
        {
            inlines.Add(new Run { Text = function[..(separator + 2)], Foreground = Resource("CppClassBrush") });
        }

        var method = separator > 0 ? function[(separator + 2)..] : function;

        inlines.Add(new Run { Text = method, Foreground = Resource("CppFunctionBrush") });
    }

    public static void AddDecoded(InlineCollection inlines, CallSignaturePart part)
    {
        if (part.DecodedString is { } decodedString)
        {
            StringText.AddRuns(inlines, decodedString);

            return;
        }

        if (part.DecodedClass is { } className)
        {
            inlines.Add(new Run { Text = className, Foreground = Resource("CppClassBrush") });

            inlines.Add(new Run { Text = " " });
        }

        HexText.AddRuns(inlines, part.Decoded ?? part.Text);
    }

    public static void AddType(InlineCollection inlines, string type)
    {
        foreach (var token in SignatureTokenizer.Tokenize(type))
        {
            var brush = token.Type switch
            {
                SignatureTokenType.Keyword => "SqlKeywordBrush",
                SignatureTokenType.Punctuation => "SqlPunctuationBrush",
                _ => "SqlIdentifierBrush"
            };

            inlines.Add(new Run { Text = token.Text, Foreground = Resource(brush) });
        }
    }

    private static Brush? Resource(string key)
        => Application.Current.Resources.TryGetValue(key, out var resource) ? resource as Brush : null;

    private static void OnFunctionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock text)
        {
            return;
        }

        text.Inlines.Clear();

        if (e.NewValue is string function)
        {
            AddFunction(text.Inlines, function);
        }

        ToolTipService.SetToolTip(text, e.NewValue);
    }

    private static void OnArgumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock text)
        {
            return;
        }

        text.Inlines.Clear();

        if (e.NewValue is not CallSignaturePart part)
        {
            return;
        }

        if (part.DeclaredType is { } type)
        {
            AddType(text.Inlines, type);

            text.Inlines.Add(new Run { Text = "  " });
        }

        if (part.Decoded is not null)
        {
            AddDecoded(text.Inlines, part);
        }
        else
        {
            HexText.AddRuns(text.Inlines, part.Text);
        }
    }
}
