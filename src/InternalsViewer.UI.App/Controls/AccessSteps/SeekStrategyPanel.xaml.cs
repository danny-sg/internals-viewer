using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using InternalsViewer.Internals.DataAccess.AccessPaths.Results;
using InternalsViewer.Internals.DataAccess.AccessPaths.Search;
using InternalsViewer.Internals.DataAccess.AccessPaths.Text;
using InternalsViewer.UI.App.Controls.Predicates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace InternalsViewer.UI.App.Controls.AccessSteps;

public sealed partial class SeekStrategyPanel : UserControl
{
    public static readonly DependencyProperty StrategyProperty =
        DependencyProperty.Register(nameof(Strategy),
                                    typeof(SeekStrategy),
                                    typeof(SeekStrategyPanel),
                                    new PropertyMetadata(null, OnStrategyChanged));

    public static readonly DependencyProperty CurrentPhaseProperty =
        DependencyProperty.Register(nameof(CurrentPhase),
                                    typeof(SeekPhase?),
                                    typeof(SeekStrategyPanel),
                                    new PropertyMetadata(null, OnCurrentPhaseChanged));

    public static readonly DependencyProperty CountersProperty =
        DependencyProperty.Register(nameof(Counters),
                                    typeof(AccessCounters),
                                    typeof(SeekStrategyPanel),
                                    new PropertyMetadata(default(AccessCounters), OnCountersChanged));

    public static readonly DependencyProperty IsWalkActiveProperty =
        DependencyProperty.Register(nameof(IsWalkActive),
                                    typeof(bool),
                                    typeof(SeekStrategyPanel),
                                    new PropertyMetadata(false, OnCurrentPhaseChanged));

    private readonly List<(SeekPhase Phase, Grid Row)> _phaseRows = [];

    private readonly List<(TextBlock Value, Func<AccessCounters, string> Get)> _counterValues = [];

    public SeekStrategy? Strategy
    {
        get => (SeekStrategy?)GetValue(StrategyProperty);
        set => SetValue(StrategyProperty, value);
    }

    public SeekPhase? CurrentPhase
    {
        get => (SeekPhase?)GetValue(CurrentPhaseProperty);
        set => SetValue(CurrentPhaseProperty, value);
    }

    public AccessCounters Counters
    {
        get => (AccessCounters)GetValue(CountersProperty);
        set => SetValue(CountersProperty, value);
    }

    public bool IsWalkActive
    {
        get => (bool)GetValue(IsWalkActiveProperty);
        set => SetValue(IsWalkActiveProperty, value);
    }

    public SeekStrategyPanel()
    {
        InitializeComponent();
    }

    private static void OnStrategyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SeekStrategyPanel)d).Rebuild();
    }

    private static void OnCurrentPhaseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SeekStrategyPanel)d).ApplyCurrentPhase();
    }

    private static void OnCountersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SeekStrategyPanel)d).ApplyCounters();
    }

    private void Rebuild()
    {
        PhasesPanel.Children.Clear();
        _phaseRows.Clear();
        _counterValues.Clear();

        PlaceholderText.Visibility = Strategy is null ? Visibility.Visible : Visibility.Collapsed;

        if (Strategy is null)
        {
            return;
        }

        PhasesPanel.Children.Add(SectionHeader("Strategy", topMargin: 0));

        foreach (var phase in Strategy.Phases)
        {
            var content = new RichTextBlock
            {
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };

            var paragraph = new Paragraph();

            paragraph.Inlines.Add(new Run { Text = phase.Lead });

            if (!phase.LeadCondition.IsDefaultOrEmpty)
            {
                paragraph.Inlines.Add(ConditionInline(phase.LeadCondition));
            }

            if (phase.Middle.Length > 0)
            {
                paragraph.Inlines.Add(new Run { Text = phase.Middle });
            }

            if (!phase.Condition.IsDefaultOrEmpty)
            {
                paragraph.Inlines.Add(ConditionInline(phase.Condition));
            }

            if (phase.Trail.Length > 0)
            {
                paragraph.Inlines.Add(new Run { Text = phase.Trail });
            }

            content.Blocks.Add(paragraph);

            var row = TitledRow(phase.Title, 80, content, semiBoldTitle: true);

            _phaseRows.Add((phase.Phase, row));

            PhasesPanel.Children.Add(row);
        }

        if (Strategy.RowGoalReason is not null)
        {
            PhasesPanel.Children.Add(new TextBlock
            {
                Text = $"Row goal {Strategy.RowGoal} — {Strategy.RowGoalReason}",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7,
                Margin = new Thickness(0, 6, 0, 0)
            });
        }

        if (Strategy.Bounds is not null)
        {
            PhasesPanel.Children.Add(SectionHeader("Search Details", topMargin: 12));

            PhasesPanel.Children.Add(TitledRow("Seek Bounds", 110, new PredicateTextBox
            {
                Text = PredicateText.From(Strategy.Bounds),
                TextPadding = new Thickness(2, 0, 2, 0),
                HasBackground = false
            }, dimTitle: true));

            PhasesPanel.Children.Add(TitledRow("Scan Direction", 110, new TextBlock
            {
                Text = Strategy.Direction.ToString(),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            }, dimTitle: true));

            if (Strategy.Residual is not null)
            {
                PhasesPanel.Children.Add(TitledRow("Residual", 110, new PredicateTextBox
                {
                    Text = PredicateText.From(Strategy.Residual),
                    TextPadding = new Thickness(2, 0, 2, 0),
                    HasBackground = false
                }, dimTitle: true));
            }
            else if (Strategy.HasUntranslatedResidual)
            {
                PhasesPanel.Children.Add(TitledRow("Residual", 110, new TextBlock
                {
                    Text = "Not translatable — rows are not filtered",
                    FontSize = 12,
                    FontStyle = Windows.UI.Text.FontStyle.Italic,
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.8
                }, dimTitle: true));
            }
        }

        PhasesPanel.Children.Add(SectionHeader("Counters", topMargin: 12));

        if (Strategy.RangeCount > 1)
        {
            var rangeCount = Strategy.RangeCount;

            AddCounterRow("Range Seeks", c => $"{c.RangeSeeks:N0}/{rangeCount:N0}");
        }

        AddCounterRow("Pages Read", c => c.PagesRead);
        AddCounterRow("Comparisons", c => c.Comparisons);
        AddCounterRow("Rows Read", c => c.RowsRead);
        AddCounterRow("Rows Output", c => c.RowsOutput);
        AddCounterRow("Ghosts Skipped", c => c.GhostsSkipped);
        AddCounterRow("Leaf Links Followed", c => c.LeafLinksFollowed);

        ApplyCounters();

        ApplyCurrentPhase();
    }

    private static InlineUIContainer ConditionInline(ImmutableArray<PredicateToken> tokens)
    {
        return new InlineUIContainer
        {
            Child = new Border
            {
                Margin = new Thickness(2, 0, 2, -3),
                Child = new PredicateTextBox
                {
                    Text = new PredicateText(tokens),
                    TextPadding = new Thickness(0),
                    HasBackground = false
                }
            }
        };
    }

    private void AddCounterRow(string label, Func<AccessCounters, long> get)
    {
        AddCounterRow(label, c => get(c).ToString("N0"));
    }

    private void AddCounterRow(string label, Func<AccessCounters, string> get)
    {
        var value = new TextBlock { FontSize = 12 };

        _counterValues.Add((value, get));

        PhasesPanel.Children.Add(TitledRow(label, 110, value, dimTitle: true));
    }

    private void ApplyCounters()
    {
        foreach (var (value, get) in _counterValues)
        {
            value.Text = get(Counters);
        }
    }

    private static TextBlock SectionHeader(string text, double topMargin)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, topMargin, 0, 4)
        };
    }

    private static Grid TitledRow(string title,
                                  double titleWidth,
                                  FrameworkElement content,
                                  bool semiBoldTitle = false,
                                  bool dimTitle = false)
    {
        var row = new Grid
        {
            Margin = new Thickness(0, 2, 0, 2)
        };

        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(titleWidth) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var titleBlock = new TextBlock
        {
            Text = title,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Top
        };

        if (semiBoldTitle)
        {
            titleBlock.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        }

        if (dimTitle)
        {
            titleBlock.Opacity = 0.7;
        }

        row.Children.Add(titleBlock);

        content.HorizontalAlignment = HorizontalAlignment.Stretch;

        Grid.SetColumn(content, 1);

        row.Children.Add(content);

        return row;
    }

    private void ApplyCurrentPhase()
    {
        foreach (var (phase, row) in _phaseRows)
        {
            row.Opacity = !IsWalkActive || CurrentPhase is null || phase == CurrentPhase ? 1 : 0.55;
        }
    }
}
