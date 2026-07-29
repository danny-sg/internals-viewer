using InternalsViewer.Internals.DataAccess.AccessPaths.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.Predicates;

public sealed partial class PredicateTextBox : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text),
                                    typeof(PredicateText),
                                    typeof(PredicateTextBox),
                                    new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(nameof(PlaceholderText),
                                    typeof(string),
                                    typeof(PredicateTextBox),
                                    new PropertyMetadata(string.Empty, OnSourceChanged));

    public static readonly DependencyProperty TextPaddingProperty =
        DependencyProperty.Register(nameof(TextPadding),
                                    typeof(Thickness),
                                    typeof(PredicateTextBox),
                                    new PropertyMetadata(new Thickness(10, 8, 10, 8)));

    public static readonly DependencyProperty HasBackgroundProperty =
        DependencyProperty.Register(nameof(HasBackground),
                                    typeof(bool),
                                    typeof(PredicateTextBox),
                                    new PropertyMetadata(true, OnHasBackgroundChanged));

    public PredicateTextBox()
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

    public Thickness TextPadding
    {
        get => (Thickness)GetValue(TextPaddingProperty);
        set => SetValue(TextPaddingProperty, value);
    }

    public bool HasBackground
    {
        get => (bool)GetValue(HasBackgroundProperty);
        set => SetValue(HasBackgroundProperty, value);
    }

    private static void OnHasBackgroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((PredicateTextBox)d).ApplyBackground();
    }

    private void ApplyBackground()
    {
        if (HasBackground)
        {
            BackgroundBorder.ClearValue(Microsoft.UI.Xaml.Controls.Border.BackgroundProperty);
            BackgroundBorder.ClearValue(Microsoft.UI.Xaml.Controls.Border.BorderThicknessProperty);
        }
        else
        {
            BackgroundBorder.Background = null;
            BackgroundBorder.BorderThickness = new Thickness(0);
        }
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((PredicateTextBox)d).Render();
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
            Foreground = BrushFor(token.Type)
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

    private Brush BrushFor(PredicateTokenType type)
    {
        var key = type switch
        {
            PredicateTokenType.Keyword
                => "PredicateKeywordBrush",
            PredicateTokenType.Column 
                => "PredicateColumnBrush",
            PredicateTokenType.Operator 
                => "PredicateOperatorBrush",
            PredicateTokenType.Number 
                => "PredicateNumberBrush",
            PredicateTokenType.Literal 
                => "PredicateLiteralBrush",
            PredicateTokenType.Null 
                => "PredicateNullBrush",
            PredicateTokenType.Punctuation 
                => "PredicatePunctuationBrush",
            PredicateTokenType.Unknown 
                => "PredicateUnknownBrush",
            _ => null
        };

        if (key is not null && Resources.TryGetValue(key, out var resource) && resource is Brush brush)
        {
            return brush;
        }

        return TextHost.Foreground;
    }
}
