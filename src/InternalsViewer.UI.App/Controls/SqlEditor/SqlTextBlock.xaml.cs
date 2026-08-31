using InternalsViewer.Query.Parsing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace InternalsViewer.UI.App.Controls.SqlEditor;

/// <summary>
/// Formatted SQL using tokenizer
/// </summary>
public sealed partial class SqlTextBlock : UserControl
{
    public static readonly DependencyProperty SqlProperty =
        DependencyProperty.Register(nameof(Sql), typeof(string), typeof(SqlTextBlock),
            new PropertyMetadata(string.Empty, OnSourceChanged));

    /// <summary>
    /// The SQL to display
    /// </summary>
    public string Sql
    {
        get => (string)GetValue(SqlProperty);
        set => SetValue(SqlProperty, value);
    }

    public static readonly DependencyProperty MaxLinesProperty =
        DependencyProperty.Register(nameof(MaxLines), typeof(int), typeof(SqlTextBlock),
            new PropertyMetadata(0));

    /// <summary>
    /// The lines to show before the text is trimmed, or zero for all of them
    /// </summary>
    public int MaxLines
    {
        get => (int)GetValue(MaxLinesProperty);
        set => SetValue(MaxLinesProperty, value);
    }

    public SqlTextBlock()
    {
        InitializeComponent();

        ActualThemeChanged += (_, _) => Render();

        Loaded += (_, _) => Render();
    }

    private static SqlTokenizer Tokenizer { get; } = new();

    private void Render()
    {
        TextHost.Blocks.Clear();

        var paragraph = new Paragraph();

        foreach (var token in Tokenizer.Tokenize(Sql))
        {
            paragraph.Inlines.Add(new Run { Text = token.Text, Foreground = BrushFor(token.Type) });
        }

        TextHost.Blocks.Add(paragraph);
    }

    private Brush BrushFor(SqlTokenType type)
    {
        var key = type switch
        {
            SqlTokenType.Keyword
                => "SqlKeywordBrush",
            SqlTokenType.Identifier
                => "SqlIdentifierBrush",
            SqlTokenType.Operator
                => "SqlOperatorBrush",
            SqlTokenType.Number
                => "SqlNumberBrush",
            SqlTokenType.Literal
                => "SqlLiteralBrush",
            SqlTokenType.Punctuation
                => "SqlPunctuationBrush",
            SqlTokenType.Comment
                => "SqlCommentBrush",
            SqlTokenType.Unknown
                => "SqlUnknownBrush",
            _ => null
        };

        if (key is not null && Application.Current.Resources.TryGetValue(key, out var resource) && resource is Brush brush)
        {
            return brush;
        }

        return TextHost.Foreground;
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SqlTextBlock)d).Render();
    }
}
