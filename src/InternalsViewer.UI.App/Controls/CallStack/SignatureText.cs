using InternalsViewer.UI.App.Models.Query.CallStack;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.CallStack;

/// <summary>
/// Something that a type followed from a signature can hand the click back to
/// </summary>
public interface ISignatureNavigator
{
    void OnTypeInvoked(string typeName);
}

/// <summary>
/// Draws a member signature into a text block as coloured runs, with each type a link
/// </summary>
public static class SignatureText
{
    public static readonly DependencyProperty SignatureProperty
        = DependencyProperty.RegisterAttached("Signature",
                                              typeof(string),
                                              typeof(SignatureText),
                                              new PropertyMetadata(null, OnChanged));

    public static string? GetSignature(DependencyObject element) => (string?)element.GetValue(SignatureProperty);

    public static void SetSignature(DependencyObject element, string? value) => element.SetValue(SignatureProperty, value);

    private static void Build(TextBlock text)
    {
        text.Inlines.Clear();

        if (GetSignature(text) is not { Length: > 0 } signature)
        {
            return;
        }

        foreach (var token in SignatureTokenizer.Tokenize(signature))
        {
            text.Inlines.Add(CreateInline(token));
        }
    }

    private static Inline CreateInline(SignatureToken token)
    {
        switch (token.Type)
        {
            case SignatureTokenType.Name:
                return new Run { Text = token.Text };

            case SignatureTokenType.Keyword:
                return new Run { Text = token.Text, Foreground = Brush("SqlKeywordBrush") };

            case SignatureTokenType.Type:
                var link = new Hyperlink
                {
                    UnderlineStyle = UnderlineStyle.None,
                    Foreground = Brush("SqlIdentifierBrush")
                };

                link.Inlines.Add(new Run { Text = token.Text });

                link.Click += (sender, _) => Navigator(sender)?.OnTypeInvoked(token.Text);

                return link;

            default:
                return new Run { Text = token.Text, Foreground = Brush("SqlPunctuationBrush") };
        }
    }

    private static ISignatureNavigator? Navigator(Hyperlink link)
    {
        DependencyObject? current = link.ContentStart.VisualParent;

        while (current is not null)
        {
            if (current is ISignatureNavigator navigator)
            {
                return navigator;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static Brush? Brush(string key)
        => Application.Current.Resources.TryGetValue(key, out var resource) ? resource as Brush : null;

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock text)
        {
            Build(text);
        }
    }
}
