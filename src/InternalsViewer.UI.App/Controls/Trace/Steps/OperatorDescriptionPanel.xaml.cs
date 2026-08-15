using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Windows.Input;
using InternalsViewer.Execution.AccessPaths.Definitions;
using InternalsViewer.Execution.AccessPaths.Descriptions;
using InternalsViewer.Execution.AccessPaths.Joins;
using InternalsViewer.Execution.AccessPaths.Predicates;
using InternalsViewer.Execution.AccessPaths.Results;
using InternalsViewer.Execution.AccessPaths.Search;
using InternalsViewer.Execution.AccessPaths.Text;
using InternalsViewer.UI.App.Controls.Predicates;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace InternalsViewer.UI.App.Controls.Trace.Steps;

/// <summary>
/// Describes the selected operator, what it does, the properties it was given and the phases its walk passes through
/// </summary>
public sealed partial class OperatorDescriptionPanel : UserControl
{
    private const double TitleWidth = 110;

    private const double PhaseTitleWidth = 80;

    private const double PhaseIndent = 30;

    private static readonly SolidColorBrush TrueBrush = new(Windows.UI.Color.FromArgb(255, 15, 123, 15));

    private static readonly SolidColorBrush FalseBrush = new(Windows.UI.Color.FromArgb(255, 168, 46, 46));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description),
                                    typeof(OperatorDescription),
                                    typeof(OperatorDescriptionPanel),
                                    new PropertyMetadata(null, OnDescriptionChanged));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon),
                                    typeof(Uri),
                                    typeof(OperatorDescriptionPanel),
                                    new PropertyMetadata(null, OnDescriptionChanged));

    public static readonly DependencyProperty OperatorNameProperty =
        DependencyProperty.Register(nameof(OperatorName),
                                    typeof(string),
                                    typeof(OperatorDescriptionPanel),
                                    new PropertyMetadata(null, OnDescriptionChanged));

    public static readonly DependencyProperty DefinitionProperty =
        DependencyProperty.Register(nameof(Definition),
                                    typeof(IteratorDefinition),
                                    typeof(OperatorDescriptionPanel),
                                    new PropertyMetadata(null, OnDescriptionChanged));

    public static readonly DependencyProperty StrategyProperty =
        DependencyProperty.Register(nameof(Strategy),
                                    typeof(AccessStrategy),
                                    typeof(OperatorDescriptionPanel),
                                    new PropertyMetadata(null, OnDescriptionChanged));

    public static readonly DependencyProperty PhysicalOperatorProperty =
        DependencyProperty.Register(nameof(PhysicalOperator),
                                    typeof(string),
                                    typeof(OperatorDescriptionPanel),
                                    new PropertyMetadata(null, OnDescriptionChanged));

    public static readonly DependencyProperty LogicalOperatorProperty =
        DependencyProperty.Register(nameof(LogicalOperator),
                                    typeof(string),
                                    typeof(OperatorDescriptionPanel),
                                    new PropertyMetadata(null, OnDescriptionChanged));

    public static readonly DependencyProperty IsOrderedProperty =
        DependencyProperty.Register(nameof(IsOrdered),
                                    typeof(bool?),
                                    typeof(OperatorDescriptionPanel),
                                    new PropertyMetadata(null, OnDescriptionChanged));

    public static readonly DependencyProperty IsPendingProperty =
        DependencyProperty.Register(nameof(IsPending),
                                    typeof(bool),
                                    typeof(OperatorDescriptionPanel),
                                    new PropertyMetadata(false, OnDescriptionChanged));

    public static readonly DependencyProperty CurrentPhaseProperty =
        DependencyProperty.Register(nameof(CurrentPhase),
                                    typeof(AccessPhase?),
                                    typeof(OperatorDescriptionPanel),
                                    new PropertyMetadata(null, OnCurrentPhaseChanged));

    public static readonly DependencyProperty CountersProperty =
        DependencyProperty.Register(nameof(Counters),
                                    typeof(AccessCounters),
                                    typeof(OperatorDescriptionPanel),
                                    new PropertyMetadata(default(AccessCounters), OnCountersChanged));

    public static readonly DependencyProperty RunToPhaseCommandProperty =
        DependencyProperty.Register(nameof(RunToPhaseCommand),
                                    typeof(ICommand),
                                    typeof(OperatorDescriptionPanel),
                                    new PropertyMetadata(null));

    public static readonly DependencyProperty IsWalkActiveProperty =
        DependencyProperty.Register(nameof(IsWalkActive),
                                    typeof(bool),
                                    typeof(OperatorDescriptionPanel),
                                    new PropertyMetadata(false, OnCurrentPhaseChanged));

    private readonly List<(AccessPhase Phase, Grid Row)> _phaseRows = [];

    private readonly List<(TextBlock Value, Func<AccessCounters, string> Get)> _counterValues = [];

    public OperatorDescription? Description
    {
        get => (OperatorDescription?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public Uri? Icon
    {
        get => (Uri?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string? OperatorName
    {
        get => (string?)GetValue(OperatorNameProperty);
        set => SetValue(OperatorNameProperty, value);
    }

    public IteratorDefinition? Definition
    {
        get => (IteratorDefinition?)GetValue(DefinitionProperty);
        set => SetValue(DefinitionProperty, value);
    }

    public AccessStrategy? Strategy
    {
        get => (AccessStrategy?)GetValue(StrategyProperty);
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

    /// <summary>
    /// The operator is a correlated access path that has not been bound yet, so it has no descent to describe
    /// </summary>
    public bool IsPending
    {
        get => (bool)GetValue(IsPendingProperty);
        set => SetValue(IsPendingProperty, value);
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

    /// <summary>
    /// Run to the next step the operator takes in a phase, taking the phase as its parameter
    /// </summary>
    public ICommand? RunToPhaseCommand
    {
        get => (ICommand?)GetValue(RunToPhaseCommandProperty);
        set => SetValue(RunToPhaseCommandProperty, value);
    }

    public bool IsWalkActive
    {
        get => (bool)GetValue(IsWalkActiveProperty);
        set => SetValue(IsWalkActiveProperty, value);
    }

    public OperatorDescriptionPanel()
    {
        InitializeComponent();
    }

    private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((OperatorDescriptionPanel)d).Rebuild();
    }

    private static void OnCurrentPhaseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((OperatorDescriptionPanel)d).ApplyCurrentPhase();
    }

    private static void OnCountersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((OperatorDescriptionPanel)d).ApplyCounters();
    }

    private void Rebuild()
    {
        RowsPanel.Children.Clear();

        _phaseRows.Clear();
        _counterValues.Clear();

        PlaceholderText.Visibility = Description is null ? Visibility.Visible : Visibility.Collapsed;

        if (Description is not { } description)
        {
            return;
        }

        AddHeader();

        AddSummary(description.Summary);

        AddFlagRow("Is Streaming", description.IsStreaming);
        AddFlagRow("Is Blocking", description.IsBlocking);

        AddProperties();

        AddEntryPoint();

        AddPhases(description);

        AddCounters();

        ApplyCounters();

        ApplyCurrentPhase();
    }

    private void AddHeader()
    {
        if (OperatorName is not { Length: > 0 } name)
        {
            return;
        }

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 12)
        };

        if (Icon is { } icon)
        {
            header.Children.Add(new Image
            {
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
                Source = new SvgImageSource(icon)
            });
        }

        header.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = FontSize,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });

        RowsPanel.Children.Add(header);
    }

    private void AddSummary(string summary)
    {
        if (summary.Length == 0)
        {
            return;
        }

        RowsPanel.Children.Add(new TextBlock
        {
            Text = summary,
            FontSize = FontSize,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
    }

    private void AddProperties()
    {
        if (!string.IsNullOrEmpty(PhysicalOperator))
        {
            AddTextRow("Physical Operator", PhysicalOperator);
        }

        if (!string.IsNullOrEmpty(LogicalOperator))
        {
            AddTextRow("Logical Operator", LogicalOperator);
        }

        switch (Definition)
        {
            case HashMatchDefinition hash:
                AddJoinProperties(hash);

                AddColumnsRow("Build Keys", hash.Build.JoinColumns);
                AddColumnsRow("Probe Keys", hash.Probe.JoinColumns);
                break;

            case MergeJoinDefinition merge:
                AddJoinProperties(merge);

                AddColumnsRow("Join Keys", merge.Outer.JoinColumns);
                break;

            case NestedLoopsDefinition loops:
                AddJoinProperties(loops);
                break;

            case TopDefinition top:
                AddTextRow("Row Count", $"{top.RowCount:N0}");

                if (top.IsPercent)
                {
                    AddTextRow("Is Percent", "True");
                }

                break;

            case SortDefinition sort:
                AddColumnsRow("Sort Keys", [.. sort.Keys.Select(k => k.Descending ? $"{k.Column} DESC" : k.Column)]);

                AddTextRow("Distinct", sort.IsDistinct ? "True" : "False");

                if (sort.TopCount is { } topCount)
                {
                    AddTextRow("Top Count", $"{topCount:N0}");
                }

                break;

            case ConcatenationDefinition concatenation:
                AddTextRow("Inputs", $"{concatenation.Inputs.Count:N0}");
                break;
        }

        AddAccessPathProperties();
    }

    private void AddJoinProperties(JoinDefinition join)
    {
        AddTextRow("Join Type", join.JoinType.ToDisplayName());

        if (join.Residual is { } residual and not (AccessPredicate.True or AccessPredicate.NoTranslation))
        {
            RowsPanel.Children.Add(SeekPanelRows.TitledRow("Predicate", TitleWidth, BuildPredicateView(residual), dimTitle: true));
        }
    }

    private void AddAccessPathProperties()
    {
        if (IsOrdered is { } isOrdered)
        {
            AddTextRow("Ordered", isOrdered ? "True" : "False");
        }

        if (Strategy is not { } strategy)
        {
            if (IsPending)
            {
                RowsPanel.Children.Add(new TextBlock
                {
                    Text = "The range is bound from each outer row, so the descent is planned when the first rebind arrives",
                    FontSize = FontSize,
                    Opacity = 0.7,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 12)
                });
            }

            return;
        }

        if (strategy.KeyColumns is { Count: > 0 } keyColumns)
        {
            AddColumnsRow("Key Columns", keyColumns);
        }

        if (strategy.IsUnique is { } isUnique)
        {
            AddTextRow("Unique", isUnique ? "True" : "False");
        }

        if (strategy.RowGoal is { } rowGoal)
        {
            AddTextRow("Row Goal", $"{rowGoal:N0}");
        }

        if (strategy.Bounds is null)
        {
            return;
        }

        if (strategy.Ranges.Count > 1)
        {
            for (var index = 0; index < strategy.Ranges.Count; index++)
            {
                AddPredicateTextRow($"Range {index + 1}", PredicateText.From(strategy.Ranges[index]));
            }
        }
        else
        {
            AddPredicateTextRow("Seek Bounds", PredicateText.From(strategy.Bounds));
        }

        AddTextRow("Scan Direction", strategy.Direction.ToString());

        if (strategy.Residual is not null)
        {
            RowsPanel.Children.Add(SeekPanelRows.TitledRow("Predicate", TitleWidth, BuildPredicateView(strategy.Residual), dimTitle: true));
        }
        else if (strategy.HasUntranslatedResidual)
        {
            RowsPanel.Children.Add(SeekPanelRows.TitledRow("Predicate", TitleWidth, new TextBlock
            {
                Text = "Not translatable — rows are not filtered",
                FontSize = FontSize,
                FontStyle = Windows.UI.Text.FontStyle.Italic,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.8
            }, dimTitle: true));
        }
    }

    private void AddEntryPoint()
    {
        if (Strategy?.EntryPoint is not { } entryPoint)
        {
            return;
        }

        var content = new StackPanel { Orientation = Orientation.Horizontal };

        content.Children.Add(new TextBlock
        {
            Text = entryPoint.ToString(),
            FontSize = FontSize
        });

        if (Strategy.EntryPointSource is { Length: > 0 } source)
        {
            content.Children.Add(new TextBlock
            {
                Text = source,
                FontSize = FontSize,
                FontFamily = new FontFamily("Consolas"),
                Opacity = 0.7,
                Margin = new Thickness(8, 0, 0, 0)
            });
        }

        RowsPanel.Children.Add(SeekPanelRows.TitledRow("Entry Point", TitleWidth, content, dimTitle: true));
    }

    private void AddPhases(OperatorDescription description)
    {
        if (description.Phases.IsDefaultOrEmpty)
        {
            return;
        }

        RowsPanel.Children.Add(SeekPanelRows.SectionHeader("Phases", topMargin: 4));

        foreach (var phase in description.Phases)
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

            var row = SeekPanelRows.TitledRow(phase.Title, PhaseTitleWidth, content, semiBoldTitle: true);

            _phaseRows.Add((phase.Phase, row));

            RowsPanel.Children.Add(IndentedPhase(row, phase.Phase));
        }

        if (Strategy?.RowGoalReason is not null)
        {
            RowsPanel.Children.Add(new TextBlock
            {
                Text = $"Row goal {Strategy.RowGoal} — {Strategy.RowGoalReason}",
                FontSize = FontSize,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7,
                Margin = new Thickness(0, 0, 0, 6)
            });
        }
    }

    /// <summary>
    /// Sets a phase in from the section beside a button that runs on to it
    /// </summary>
    private Grid IndentedPhase(Grid row, AccessPhase phase)
    {
        var indented = new Grid();

        indented.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(PhaseIndent) });
        indented.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        indented.Children.Add(RunToPhaseButton(phase));

        Grid.SetColumn(row, 1);

        indented.Children.Add(row);

        return indented;
    }

    private Button RunToPhaseButton(AccessPhase phase)
    {
        var button = new Button
        {
            Width = 20,
            Height = 20,
            MinWidth = 0,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 1, 0, 0),
            CornerRadius = new CornerRadius(0),
            BorderThickness = new Thickness(1),
            BorderBrush = Application.Current.Resources["ControlStrokeColorDefaultBrush"] as Brush,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Content = new FontIcon
            {
                Glyph = "",
                FontSize = 9,
                Foreground = TrueBrush
            }
        };

        ToolTipService.SetToolTip(button, "Run query to here");

        button.Click += (_, _) =>
        {
            if (RunToPhaseCommand is { } command && command.CanExecute(phase))
            {
                command.Execute(phase);
            }
        };

        return button;
    }

    private void AddCounters()
    {
        if (Strategy is not { } strategy)
        {
            return;
        }

        RowsPanel.Children.Add(SeekPanelRows.SectionHeader("Counters", topMargin: 12));

        if (strategy.RangeCount > 1)
        {
            var rangeCount = strategy.RangeCount;

            AddCounterRow("Range Seeks", c => $"{c.RangeSeeks:N0}/{rangeCount:N0}");
        }

        AddCounterRow("Pages Read", c => c.PagesRead);
        AddCounterRow("Data Read", c => FormatDataRead(c.PagesRead));
        AddCounterRow("Comparisons", c => c.Comparisons);
        AddCounterRow("Rows Read", c => c.RowsRead);
        AddCounterRow("Rows Output", c => c.RowsOutput);
        AddCounterRow("Ghosts Skipped", c => c.GhostsSkipped);
        AddCounterRow("Leaf Links Followed", c => c.LeafLinksFollowed);
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

    private void AddFlagRow(string title, bool value)
    {
        var glyph = new TextBlock
        {
            Text = value ? "✓" : "✕",
            FontSize = FontSize,
            Foreground = value ? TrueBrush : FalseBrush
        };

        RowsPanel.Children.Add(SeekPanelRows.TitledRow(title, TitleWidth, glyph, dimTitle: true));
    }

    private void AddTextRow(string title, string text)
    {
        RowsPanel.Children.Add(SeekPanelRows.TitledRow(title, TitleWidth, new TextBlock
        {
            Text = text,
            FontSize = FontSize,
            TextWrapping = TextWrapping.Wrap
        }, dimTitle: true));
    }

    private void AddColumnsRow(string title, IReadOnlyList<string> columns)
    {
        if (columns.Count == 0)
        {
            return;
        }

        AddTextRow(title, string.Join(", ", columns));
    }

    private void AddPredicateTextRow(string title, PredicateText text)
    {
        RowsPanel.Children.Add(SeekPanelRows.TitledRow(title, TitleWidth, new PredicateTextBox
        {
            Text = text,
            TextPadding = new Thickness(2, 0, 2, 0),
            HasBackground = false,
            FontSize = FontSize
        }, dimTitle: true));
    }

    private static string FormatDataRead(long pagesRead)
    {
        var kb = pagesRead * 8;

        return kb > 1024 ? $"{kb / 1024D:N1} MB" : $"{kb:N0} KB";
    }

    private void AddCounterRow(string label, Func<AccessCounters, long> get)
    {
        AddCounterRow(label, c => get(c).ToString("N0"));
    }

    private void AddCounterRow(string label, Func<AccessCounters, string> get)
    {
        var value = new TextBlock { FontSize = FontSize };

        _counterValues.Add((value, get));

        RowsPanel.Children.Add(SeekPanelRows.TitledRow(label, TitleWidth, value, dimTitle: true));
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
                HasBackground = false,
                FontSize = FontSize
            });
        }

        return view;
    }
}
