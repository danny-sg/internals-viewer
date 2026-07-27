using InternalsViewer.Internals.DataAccess.AccessPaths.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.Predicates;

/// <summary>
/// Displays formatted predicate text
/// </summary>
/// <remarks>
/// The control renders a token sequence and nothing else, so what is shown always matches the model the caller formatted, and the control
/// never has to interpret a predicate itself.
/// </remarks>
public sealed partial class PredicateView : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text),
                                    typeof(PredicateText),
                                    typeof(PredicateView),
                                    new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(nameof(PlaceholderText),
                                    typeof(string),
                                    typeof(PredicateView),
                                    new PropertyMetadata(string.Empty, OnSourceChanged));

    public PredicateView()
    {
        InitializeComponent();

        ActualThemeChanged += (_, _) => Render();

        Loaded += (_, _) => Render();
    }

    /// <summary>
    /// Formatted predicate to display
    /// </summary>
    public PredicateText? Text
    {
        get => (PredicateText?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Text shown when there is nothing to display
    /// </summary>
    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((PredicateView)d).Render();
    }

    private void Render()
    {
        TextHost.Blocks.Clear();

        var paragraph = new Paragraph();

        if (Text is null || Text.IsEmpty)
        {
            if (!string.IsNullOrEmpty(PlaceholderText))
            {
                paragraph.Inlines.Add(new Run
                {
                    Text = PlaceholderText,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                    FontStyle = Windows.UI.Text.FontStyle.Italic
                });
            }

            TextHost.Blocks.Add(paragraph);

            return;
        }

        foreach (var token in Text.Tokens)
        {
            paragraph.Inlines.Add(CreateInline(token));
        }

        TextHost.Blocks.Add(paragraph);
    }

    /// <summary>
    /// Builds the inline for a token, wrapping it so a description can be shown when one is present
    /// </summary>
    /// <remarks>
    /// A tooltip has to hang off a <see cref="FrameworkElement"/>, so a token carrying a description is hosted in an inline container
    /// rather than added as a bare run.
    /// </remarks>
    private Inline CreateInline(PredicateToken token)
    {
        var run = new Run
        {
            Text = token.Text,
            Foreground = BrushFor(token.Kind)
        };

        if (string.IsNullOrEmpty(token.Description))
        {
            return run;
        }

        var block = new TextBlock
        {
            FontFamily = TextHost.FontFamily,
            FontSize = TextHost.FontSize
        };

        block.Inlines.Add(run);

        ToolTipService.SetToolTip(block, token.Description);

        return new InlineUIContainer { Child = block };
    }

    private Brush BrushFor(PredicateTokenKind kind)
    {
        var key = kind switch
        {
            PredicateTokenKind.Keyword => "PredicateKeywordBrush",
            PredicateTokenKind.Column => "PredicateColumnBrush",
            PredicateTokenKind.Operator => "PredicateOperatorBrush",
            PredicateTokenKind.Number => "PredicateNumberBrush",
            PredicateTokenKind.Literal => "PredicateLiteralBrush",
            PredicateTokenKind.Null => "PredicateNullBrush",
            PredicateTokenKind.Punctuation => "PredicatePunctuationBrush",
            PredicateTokenKind.Unknown => "PredicateUnknownBrush",
            _ => null
        };

        if (key is not null && Resources.TryGetValue(key, out var resource) && resource is Brush brush)
        {
            return brush;
        }

        return TextHost.Foreground;
    }
}
