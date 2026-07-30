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

public sealed partial class AccessStrategyPanel : UserControl
{
    public static readonly DependencyProperty StrategyProperty =
        DependencyProperty.Register(nameof(Strategy),
                                    typeof(AccessStrategy),
                                    typeof(AccessStrategyPanel),
                                    new PropertyMetadata(null, OnStrategyChanged));

    public static readonly DependencyProperty CurrentPhaseProperty =
        DependencyProperty.Register(nameof(CurrentPhase),
                                    typeof(AccessPhase?),
                                    typeof(AccessStrategyPanel),
                                    new PropertyMetadata(null, OnCurrentPhaseChanged));

    public static readonly DependencyProperty CountersProperty =
        DependencyProperty.Register(nameof(Counters),
                                    typeof(AccessCounters),
                                    typeof(AccessStrategyPanel),
                                    new PropertyMetadata(default(AccessCounters), OnCountersChanged));

    public static readonly DependencyProperty IsWalkActiveProperty =
        DependencyProperty.Register(nameof(IsWalkActive),
                                    typeof(bool),
                                    typeof(AccessStrategyPanel),
                                    new PropertyMetadata(false, OnCurrentPhaseChanged));

    private readonly List<(AccessPhase Phase, Grid Row)> _phaseRows = [];

    private readonly List<(TextBlock Value, Func<AccessCounters, string> Get)> _counterValues = [];

    public AccessStrategy? Strategy
    {
        get => (AccessStrategy?)GetValue(StrategyProperty);
        set => SetValue(StrategyProperty, value);
    }

    public AccessPhase? CurrentPhase
    {
        get => (AccessPhase?)GetValue(CurrentPhaseProperty);
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

    public AccessStrategyPanel()
    {
        InitializeComponent();
    }

    private static void OnStrategyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AccessStrategyPanel)d).Rebuild();
    }

    private static void OnCurrentPhaseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AccessStrategyPanel)d).ApplyCurrentPhase();
    }

    private static void OnCountersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AccessStrategyPanel)d).ApplyCounters();
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

        foreach (var phase in Strategy.Phases)
        {
            var content = new RichTextBlock
            {
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

            var row = SeekPanelRows.TitledRow(phase.Title, 80, content, semiBoldTitle: true);

            _phaseRows.Add((phase.Phase, row));

            PhasesPanel.Children.Add(row);
        }

        if (Strategy.RowGoalReason is not null)
        {
            PhasesPanel.Children.Add(new TextBlock
            {
                Text = $"Row goal {Strategy.RowGoal} — {Strategy.RowGoalReason}",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7,
                Margin = new Thickness(0, 6, 0, 0)
            });
        }

        PhasesPanel.Children.Add(SeekPanelRows.SectionHeader("Counters", topMargin: 12));

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

    private InlineUIContainer ConditionInline(ImmutableArray<PredicateToken> tokens)
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
                    HasBackground = false,
                    FontSize = FontSize
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
        var value = new TextBlock();

        _counterValues.Add((value, get));

        PhasesPanel.Children.Add(SeekPanelRows.TitledRow(label, 110, value, dimTitle: true));
    }

    private void ApplyCounters()
    {
        foreach (var (value, get) in _counterValues)
        {
            value.Text = get(Counters);
        }
    }

    private void ApplyCurrentPhase()
    {
        foreach (var (phase, row) in _phaseRows)
        {
            row.Opacity = !IsWalkActive || CurrentPhase is null || phase == CurrentPhase ? 1 : 0.55;
        }
    }
}
