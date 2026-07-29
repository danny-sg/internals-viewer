using System.Collections.Generic;
using System.Collections.Immutable;
using InternalsViewer.Internals.DataAccess.AccessPaths.Predicates;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.DataAccess.AccessPaths.Text;
using InternalsViewer.UI.App.Controls.Predicates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace InternalsViewer.UI.App.Controls.AccessSteps;

public sealed partial class SeekDescriptionPanel : UserControl
{
    public static readonly DependencyProperty StrategyProperty =
        DependencyProperty.Register(nameof(Strategy),
                                    typeof(SeekStrategy),
                                    typeof(SeekDescriptionPanel),
                                    new PropertyMetadata(null, OnStrategyChanged));

    public static readonly DependencyProperty PhysicalOperatorProperty =
        DependencyProperty.Register(nameof(PhysicalOperator),
                                    typeof(string),
                                    typeof(SeekDescriptionPanel),
                                    new PropertyMetadata(null, OnStrategyChanged));

    public static readonly DependencyProperty LogicalOperatorProperty =
        DependencyProperty.Register(nameof(LogicalOperator),
                                    typeof(string),
                                    typeof(SeekDescriptionPanel),
                                    new PropertyMetadata(null, OnStrategyChanged));

    public static readonly DependencyProperty IsOrderedProperty =
        DependencyProperty.Register(nameof(IsOrdered),
                                    typeof(bool?),
                                    typeof(SeekDescriptionPanel),
                                    new PropertyMetadata(null, OnStrategyChanged));

    public SeekStrategy? Strategy
    {
        get => (SeekStrategy?)GetValue(StrategyProperty);
        set => SetValue(StrategyProperty, value);
    }

    public string? PhysicalOperator
    {
        get => (string?)GetValue(PhysicalOperatorProperty);
        set => SetValue(PhysicalOperatorProperty, value);
    }

    public string? LogicalOperator
    {
        get => (string?)GetValue(LogicalOperatorProperty);
        set => SetValue(LogicalOperatorProperty, value);
    }

    public bool? IsOrdered
    {
        get => (bool?)GetValue(IsOrderedProperty);
        set => SetValue(IsOrderedProperty, value);
    }

    public SeekDescriptionPanel()
    {
        InitializeComponent();
    }

    private static void OnStrategyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SeekDescriptionPanel)d).Rebuild();
    }

    private void Rebuild()
    {
        RowsPanel.Children.Clear();

        if (!string.IsNullOrEmpty(PhysicalOperator))
        {
            AddTextRow("Physical Operator", PhysicalOperator);
        }

        if (!string.IsNullOrEmpty(LogicalOperator))
        {
            AddTextRow("Logical Operator", LogicalOperator);
        }

        if (IsOrdered is { } isOrdered)
        {
            AddTextRow("Ordered", isOrdered ? "True" : "False");
        }

        if (Strategy?.Bounds is null)
        {
            return;
        }

        if (Strategy.Ranges.Count > 1)
        {
            for (var index = 0; index < Strategy.Ranges.Count; index++)
            {
                RowsPanel.Children.Add(SeekPanelRows.TitledRow($"Range {index + 1}", 110, new PredicateTextBox
                {
                    Text = PredicateText.From(Strategy.Ranges[index]),
                    TextPadding = new Thickness(2, 0, 2, 0),
                    HasBackground = false,
                    FontSize = FontSize
                }, dimTitle: true));
            }
        }
        else
        {
            RowsPanel.Children.Add(SeekPanelRows.TitledRow("Seek Bounds", 110, new PredicateTextBox
            {
                Text = PredicateText.From(Strategy.Bounds),
                TextPadding = new Thickness(2, 0, 2, 0),
                HasBackground = false,
                FontSize = FontSize
            }, dimTitle: true));
        }

        RowsPanel.Children.Add(SeekPanelRows.TitledRow("Scan Direction", 110, new TextBlock
        {
            Text = Strategy.Direction.ToString(),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        }, dimTitle: true));

        if (Strategy.Residual is not null)
        {
            RowsPanel.Children.Add(SeekPanelRows.TitledRow("Predicate", 110, BuildPredicateView(Strategy.Residual), dimTitle: true));
        }
        else if (Strategy.HasUntranslatedResidual)
        {
            RowsPanel.Children.Add(SeekPanelRows.TitledRow("Predicate", 110, new TextBlock
            {
                Text = "Not translatable — rows are not filtered",
                FontSize = 12,
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8
            }, dimTitle: true));
        }
    }

    private void AddTextRow(string title, string text)
    {
        RowsPanel.Children.Add(SeekPanelRows.TitledRow(title, 110, new TextBlock
        {
            Text = text,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        }, dimTitle: true));
    }

    private StackPanel BuildPredicateView(AccessPredicate predicate)
    {
        IReadOnlyList<AccessPredicate> arms;

        string keyword;

        switch (predicate)
        {
            case AccessPredicate.Or or:
                arms = or.Predicates;
                keyword = "OR";
                break;

            case AccessPredicate.And and:
                arms = and.Predicates;
                keyword = "AND";
                break;

            default:
                arms = [predicate];
                keyword = string.Empty;
                break;
        }

        var view = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };

        for (var index = 0; index < arms.Count; index++)
        {
            var tokens = ImmutableArray.CreateBuilder<PredicateToken>();

            if (index > 0)
            {
                tokens.Add(new PredicateToken(PredicateTokenType.Keyword, keyword));
                tokens.Add(new PredicateToken(PredicateTokenType.Space, " "));
            }

            tokens.AddRange(PredicateWriter.Write(arms[index]));

            view.Children.Add(new PredicateTextBox
            {
                Text = new PredicateText(tokens.ToImmutable()),
                TextPadding = new Thickness(2, 0, 2, 0),
                HasBackground = false
                
            });
        }

        return view;
    }
}
