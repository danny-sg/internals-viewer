using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InternalsViewer.Query.FullTrace.Arguments;
using InternalsViewer.Query.FullTrace.Arguments.Enums;
using InternalsViewer.Query.FullTrace.Calls;
using InternalsViewer.Query.FullTrace.Calls.Enums;
using InternalsViewer.Query.FullTrace.Timeline.Ticks;
using InternalsViewer.UI.App.Models.Query.Trace.Timeline;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace InternalsViewer.UI.App.Controls.Query.Trace.Dialogs;

public sealed partial class OperatorTrackDialog
{
    private const string ReturnLabel = "Return (RAX)";

    private const string RegisterType = "unsigned __int64";

    private const double OperatorWidth = 72;

    private const int MaximumSuggestions = 50;

    private const string ValuePlaceholder = "Value, such as 0, 0x1F or NULL";

    private const string FunctionPlaceholder = "Function, such as CQScanFilterNew::GetRow";

    private static readonly (string Label, TraceConditionOperator Operator)[] Operators =
    [
        ("=", TraceConditionOperator.Equal),
        ("!=", TraceConditionOperator.NotEqual),
        ("<", TraceConditionOperator.LessThan),
        ("<=", TraceConditionOperator.LessThanOrEqual),
        (">", TraceConditionOperator.GreaterThan),
        (">=", TraceConditionOperator.GreaterThanOrEqual)
    ];

    private static readonly ConditionTarget[] PatternTargets =
    [
        new("Called By", TraceConditionTarget.CalledBy, null, string.Empty),
        new("Called Directly By", TraceConditionTarget.CalledDirectlyBy, null, string.Empty),
        new("Operator", TraceConditionTarget.Operator, null, string.Empty)
    ];

    public OperatorTrackDialog(OperatorTrackDialogSource source, TraceTickDefinition? existing, TraceFunctionChoice? function, Color colour)
    {
        Source = source;

        Existing = existing;

        Function = function;

        TrackName = existing?.Name ?? function?.Symbol ?? string.Empty;

        HeightFactor = existing?.HeightFactor ?? 1;

        WidthFactor = existing?.WidthFactor ?? 1;

        InitializeComponent();

        Title = existing is null ? "Add to Operators" : "Edit Operator Track";

        PrimaryButtonText = existing is null ? "Add" : "Save";

        FunctionBox.Text = function?.Label ?? (existing is null ? string.Empty : $"{existing.Module}!{existing.Function}");

        ColourPicker.Color = colour;

        ColourSwatch.Background = new SolidColorBrush(colour);
    }

    public string TrackName { get; set; }

    public double HeightFactor { get; set; }

    public double WidthFactor { get; set; }

    public TraceTickDefinition? Definition { get; private set; }

    private OperatorTrackDialogSource Source { get; }

    private TraceTickDefinition? Existing { get; }

    private TraceFunctionChoice? Function { get; set; }

    private IReadOnlyList<ConditionTarget> Targets { get; set; } = PatternTargets;

    private List<ConditionRow> Rows { get; } = [];

    public async Task LoadAsync()
    {
        await LoadTargetsAsync();

        foreach (var condition in Existing?.Conditions ?? [])
        {
            AddRow(condition);
        }
    }

    private static List<ConditionTarget> GetArgumentTargets(ArgumentLayout layout)
    {
        List<ConditionTarget> targets = [.. layout.Slots.Where(TraceArgumentCall.IsCaptured)
                                                      .Select(s => new ConditionTarget($"{s.Name} ({s.Source})",
                                                                                       TraceConditionTarget.Argument,
                                                                                       s,
                                                                                       s.Type))];

        if (layout.ReturnType is { } returnType and not "void" && TraceArgumentCall.IsReturnCaptured(returnType))
        {
            targets.Add(new ConditionTarget(ReturnLabel, TraceConditionTarget.Return, null, returnType));
        }

        return targets;
    }

    private static List<ConditionTarget> GetRegisterTargets()
        =>
        [
            .. ArgumentLayout.Registers.Select((r, i) => new ConditionTarget(r,
                                                              TraceConditionTarget.Argument,
                                                              new ArgumentSlot(r, RegisterType, ArgumentLocation.Register, i, r),
                                                              RegisterType)),
            new ConditionTarget(ReturnLabel, TraceConditionTarget.Return, null, RegisterType)
        ];

    private static List<ConditionTarget> GetSavedTargets(IReadOnlyList<TraceCallCondition> conditions)
        => [.. conditions.Where(c => c.IsCallCondition)
                         .Select(c => c.Target == TraceConditionTarget.Return
                                     ? new ConditionTarget(ReturnLabel, TraceConditionTarget.Return, null, c.Type)
                                     : new ConditionTarget($"{c.Argument?.Name} ({c.Argument?.Source})", c.Target, c.Argument, c.Type))
                         .DistinctBy(t => t.Label)];

    private static (string Label, TraceConditionOperator Operator)[] GetOperators(ConditionTarget target)
        => target.Kind is TraceConditionTarget.Argument or TraceConditionTarget.Return ? Operators : Operators[..2];

    private static bool IsFactor(double value) => !double.IsNaN(value) && value > 0;

    private async Task LoadTargetsAsync()
    {
        var layout = Function is { } function ? await Source.GetLayout(function) : null;

        List<ConditionTarget> arguments = layout is not null ? GetArgumentTargets(layout)
                                          : Function is not null ? GetRegisterTargets()
                                          : GetSavedTargets(Existing?.Conditions ?? []);

        Targets = [.. arguments, .. PatternTargets];

        foreach (var row in Rows)
        {
            SetTargets(row, row.Target.SelectedItem as string);
        }
    }

    private void AddRow(TraceCallCondition? condition)
    {
        var target = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };

        var comparison = new ComboBox { Width = OperatorWidth };

        var value = new TextBox { Text = condition?.Value ?? string.Empty };

        var remove = new Button { Content = new SymbolIcon(Symbol.Delete) };

        var grid = new Grid { ColumnSpacing = 8 };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(comparison, 1);
        Grid.SetColumn(value, 2);
        Grid.SetColumn(remove, 3);

        grid.Children.Add(target);
        grid.Children.Add(comparison);
        grid.Children.Add(value);
        grid.Children.Add(remove);

        var row = new ConditionRow(target, comparison, value);

        SetTargets(row, condition is null ? null : GetLabel(condition));

        SetOperators(row, condition is null ? null : Operators.FirstOrDefault(o => o.Operator == condition.Operator).Label);

        target.SelectionChanged += (_, _) => SetOperators(row, comparison.SelectedItem as string);

        remove.Click += (_, _) =>
        {
            Rows.Remove(row);

            ConditionsPanel.Children.Remove(grid);
        };

        Rows.Add(row);

        ConditionsPanel.Children.Add(grid);
    }

    private string GetLabel(TraceCallCondition condition) => condition.Target switch
    {
        TraceConditionTarget.Return => ReturnLabel,
        TraceConditionTarget.Argument => Targets.FirstOrDefault(t => t.Slot?.Name == condition.Argument?.Name)?.Label ?? string.Empty,
        _ => PatternTargets.First(t => t.Kind == condition.Target).Label
    };

    private void SetTargets(ConditionRow row, string? selected)
    {
        row.Target.ItemsSource = Targets.Select(t => t.Label).ToList();

        row.Target.SelectedIndex = Math.Max(Targets.ToList().FindIndex(t => t.Label == selected), 0);
    }

    private void SetOperators(ConditionRow row, string? selected)
    {
        if (row.Target.SelectedIndex < 0)
        {
            return;
        }

        var target = Targets[row.Target.SelectedIndex];

        var operators = GetOperators(target);

        row.Comparison.ItemsSource = operators.Select(o => o.Label).ToList();

        row.Comparison.SelectedIndex = Math.Max(Array.FindIndex(operators, o => o.Label == selected), 0);

        row.Value.PlaceholderText = target.Kind switch
        {
            TraceConditionTarget.CalledBy or TraceConditionTarget.CalledDirectlyBy => FunctionPlaceholder,
            TraceConditionTarget.Operator => $"Operator, such as {Source.Operators.FirstOrDefault() ?? "Filter"}",
            _ => ValuePlaceholder
        };
    }

    private TraceCallCondition GetCondition(ConditionRow row)
    {
        var target = Targets[row.Target.SelectedIndex];

        return new TraceCallCondition(target.Kind,
                                      target.Slot,
                                      target.Type,
                                      GetOperators(target)[row.Comparison.SelectedIndex].Operator,
                                      row.Value.Text.Trim());
    }

    private TraceFunctionChoice? FindFunction(string text)
        => Source.Functions.FirstOrDefault(f => string.Equals(f.Label, text.Trim(), StringComparison.OrdinalIgnoreCase))
           ?? Source.Functions.FirstOrDefault(f => string.Equals(f.Symbol, text.Trim(), StringComparison.OrdinalIgnoreCase));

    private async Task SetFunctionAsync(TraceFunctionChoice? function)
    {
        if (function is null || ReferenceEquals(function, Function))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(TrackName) || TrackName == Function?.Symbol)
        {
            TrackName = function.Symbol;

            NameBox.Text = TrackName;
        }

        Function = function;

        FunctionBox.Text = function.Label;

        await LoadTargetsAsync();
    }

    private void OnFunctionTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        var text = sender.Text.Trim();

        sender.ItemsSource = Source.Functions.Where(f => f.Label.Contains(text, StringComparison.OrdinalIgnoreCase))
                                             .Take(MaximumSuggestions)
                                             .ToList();
    }

    private async void OnFunctionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        => await SetFunctionAsync(args.SelectedItem as TraceFunctionChoice);

    private async void OnFunctionSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        => await SetFunctionAsync(args.ChosenSuggestion as TraceFunctionChoice ?? FindFunction(args.QueryText));

    private async void OnFunctionLostFocus(object sender, RoutedEventArgs e) => await SetFunctionAsync(FindFunction(FunctionBox.Text));

    private void OnAddConditionClick(object sender, RoutedEventArgs e) => AddRow(null);

    private void OnColourChanged(ColorPicker sender, ColorChangedEventArgs args)
        => ColourSwatch.Background = new SolidColorBrush(args.NewColor);

    private void OnAddClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var function = FindFunction(FunctionBox.Text) ?? Function;

        var isSavedFunction = function is null
                              && Existing is not null
                              && string.Equals(FunctionBox.Text.Trim(),
                                               $"{Existing.Module}!{Existing.Function}",
                                               StringComparison.OrdinalIgnoreCase);

        var error = function is null && !isSavedFunction ? "Choose a function from the list"
                    : string.IsNullOrWhiteSpace(TrackName) ? "Enter a name for the track"
                    : !IsFactor(HeightFactor) || !IsFactor(WidthFactor) ? "Enter a height and width greater than 0, such as 1 or 1.5"
                    : Rows.Any(r => string.IsNullOrWhiteSpace(r.Value.Text)) ? "Enter a value for every condition, or remove it"
                    : null;

        if (error is not null)
        {
            ErrorText.Text = error;

            ErrorText.Visibility = Visibility.Visible;

            args.Cancel = true;

            return;
        }

        var colour = ColourPicker.Color;

        Definition = new TraceTickDefinition(TrackName.Trim(),
                                             function?.Module ?? Existing!.Module,
                                             function?.Symbol ?? Existing!.Function,
                                             $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}",
                                             HeightFactor,
                                             WidthFactor,
                                             [.. Rows.Select(GetCondition)]);
    }

    private sealed record ConditionTarget(string Label, TraceConditionTarget Kind, ArgumentSlot? Slot, string Type);

    private sealed record ConditionRow(ComboBox Target, ComboBox Comparison, TextBox Value);
}

public sealed record OperatorTrackDialogSource(IReadOnlyList<TraceFunctionChoice> Functions,
                                               IReadOnlyList<string> Operators,
                                               Func<TraceFunctionChoice, Task<ArgumentLayout?>> GetLayout);
