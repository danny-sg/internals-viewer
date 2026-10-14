using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using InternalsViewer.UI.App.Controls.CallStack;
using InternalsViewer.UI.App.Models.Query.CallStack;
using InternalsViewer.UI.App.ViewModels;
using InternalsViewer.UI.App.ViewModels.Query.CallStack;
using InternalsViewer.UI.App.Helpers;
using InternalsViewer.UI.App.Helpers.Converters.Page;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace InternalsViewer.UI.App.Views.Query.Tabs.FlameChart;

public sealed partial class FlameChartCallStackView : UserControl, IValueFinder, IDisposable
{
    private const double DetailsWidth = 420;

    private const string ThisLabel = "this";

    public FlameChartCallStackView()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => OnViewModelChanged();

        StackTree.Loaded += OnStackTreeLoaded;

        Settings.PropertyChanged += OnSettingsPropertyChanged;

        Display.IsDecoded = Settings.DecodeValues;
    }

    public PlayheadCallStackViewModel? ViewModel => DataContext as PlayheadCallStackViewModel;

    private PlayheadCallStackViewModel? Attached { get; set; }

    private SettingsViewModel Settings { get; } = App.GetService<SettingsViewModel>();

    private ValueDisplay Display => (ValueDisplay)Resources["ValueDisplay"];

    private HexColorToBrushConverter CategoryBrushes { get; } = new();

    private static Brush? SecondaryBrush
        => Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var brush) ? brush as Brush : null;

    private static Brush? SourceHoverBrush
        => Application.Current.Resources.TryGetValue("SystemFillColorSuccessBackgroundBrush", out var brush) ? brush as Brush : null;

    private static Brush TransparentBrush { get; } = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    private HashSet<uint> ExpandedThreads { get; } = [];

    private PlayheadCallRow? ContextRow { get; set; }

    private SourceStep? ContextStep { get; set; }

    private bool IsBuilding { get; set; }

    public static Brush? RowBackground(bool isCurrent)
        => isCurrent && Application.Current.Resources.TryGetValue("SolidBackgroundFillColorQuarternaryBrush", out var brush)
            ? brush as Brush
            : null;

    public void Find(ulong value) => ViewModel?.Find(value);

    public void Dispose()
    {
        Settings.PropertyChanged -= OnSettingsPropertyChanged;

        StackTree.Loaded -= OnStackTreeLoaded;

        Detach();
    }

    private Visibility IsEmpty(IReadOnlyList<PlayheadThreadRow>? threads)
        => threads is { Count: > 0 } ? Visibility.Collapsed : Visibility.Visible;

    private Visibility IsShown(object? value) => value is null ? Visibility.Collapsed : Visibility.Visible;

    private Visibility IsFunctionShown(object? value, CallSignaturePart? selected)
        => value is null || selected is not null ? Visibility.Collapsed : Visibility.Visible;

    public static string ArgumentLabel(string? label, string? location) => label ?? location ?? string.Empty;

    private Visibility AreArgumentsShown(PlayheadCallRow? row, CallSignaturePart? selected)
        => row is { Arguments.Count: > 0 } && selected is null ? Visibility.Visible : Visibility.Collapsed;

    private string CallIdText(PlayheadCallRow? row) => row is { Id: > 0 } ? $"{row.Id}" : "None";

    private string StartText(PlayheadCallRow? row) => row is null ? string.Empty : $"{row.PositionStart:N0}";

    private string EndText(PlayheadCallRow? row) => row is null ? string.Empty : $"{row.PositionEnd:N0}";

    private string InstructionsText(PlayheadCallRow? row) => row is null ? string.Empty : $"{row.Instructions:N0}";

    private Visibility IsSourceShown(IReadOnlyList<SourceStep>? steps)
        => steps is { Count: > 0 } ? Visibility.Visible : Visibility.Collapsed;

    private Brush? CategoryBrush(PlayheadCallRow? row)
        => row is null ? null : CategoryBrushes.Convert(row.Node.CategoryColour, typeof(Brush), null!, string.Empty) as Brush;

    private string ThisText(PlayheadCallRow? row)
        => row?.This is { } self ? (self.Decoded is { } decoded && decoded != self.Text ? $"{self.Text}  {decoded}" : self.Text) : "None";

    private string ReturnText(PlayheadCallRow? row)
        => row?.Return is { } returned
            ? $"{returned.TypeName} {returned.GetText(Display.IsDecoded)}".Trim()
            : "None";

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);

            if (child is T found)
            {
                return found;
            }

            if (FindDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private void OnViewModelChanged()
    {
        if (ReferenceEquals(ViewModel, Attached))
        {
            return;
        }

        Detach();

        Attached = ViewModel;

        if (Attached is { } viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        Bindings.Update();

        Rebuild();

        ShowDetails();
    }

    private void Detach()
    {
        if (Attached is { } viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        Attached = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PlayheadCallStackViewModel.Threads):
                Rebuild();
                break;

            case nameof(PlayheadCallStackViewModel.SelectedValue):
            case nameof(PlayheadCallStackViewModel.SelectedCall):
                ShowDetails();
                break;
        }
    }

    private void Rebuild()
    {
        IsBuilding = true;

        try
        {
            StackTree.SelectedNode = null;

            StackTree.RootNodes.Clear();

            var threads = Attached?.Threads ?? [];

            var anyExpanded = threads.Any(t => ExpandedThreads.Contains(t.ThreadId));

            for (var index = 0; index < threads.Count; index++)
            {
                var thread = threads[index];

                var root = new TreeViewNode { Content = thread };

                foreach (var call in thread.Calls)
                {
                    root.Children.Add(new TreeViewNode { Content = call });
                }

                StackTree.RootNodes.Add(root);

                if (ExpandedThreads.Contains(thread.ThreadId) || (!anyExpanded && index == 0))
                {
                    root.IsExpanded = true;

                    ExpandedThreads.Add(thread.ThreadId);
                }
            }
        }
        finally
        {
            IsBuilding = false;
        }
    }

    private void ShowDetails()
    {
        var shown = Attached is { SelectedValue: not null } or { SelectedCall: not null };

        DetailsPane.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;

        DetailsSplitter.Visibility = DetailsPane.Visibility;

        SplitterColumn.Width = shown ? GridLength.Auto : new GridLength(0);

        DetailsColumn.Width = new GridLength(shown ? DetailsWidth : 0);

        ShowDetailsTitle();

        ShowValueDetails();
    }

    private void ShowDetailsTitle()
    {
        DetailsTitle.Inlines.Clear();

        if (Attached?.SelectedCall?.Signature.FirstOrDefault(p => p.Kind == CallSignaturePartKind.Function) is { } function)
        {
            SignatureRuns.AddFunction(DetailsTitle.Inlines, function.Text);
        }

        if (Attached?.SelectedValue is { Label: { } label } value)
        {
            DetailsTitle.Inlines.Add(new Run { Text = $"  [{label}]", Foreground = SecondaryBrush });

            if (value.DeclaredType is { } type)
            {
                DetailsTitle.Inlines.Add(new Run { Text = " " });

                SignatureRuns.AddType(DetailsTitle.Inlines, type);
            }
        }

        ToolTipService.SetToolTip(DetailsTitle, string.Concat(DetailsTitle.Inlines.OfType<Run>().Select(r => r.Text)));
    }

    private void ShowValueDetails()
    {
        ValueTypeText.Inlines.Clear();

        ValueDecodedText.Inlines.Clear();

        if (Attached?.SelectedValue is not { } value)
        {
            return;
        }

        if ((value.DeclaredType ?? (value.Label == ThisLabel ? ThisLabel : null)) is { } type)
        {
            SignatureRuns.AddType(ValueTypeText.Inlines, type);
        }

        if (value.Location is { } location)
        {
            ValueTypeText.Inlines.Add(new Run
            {
                Text = ValueTypeText.Inlines.Count > 0 ? $" ({location})" : location,
                Foreground = SecondaryBrush
            });
        }

        if (value.Decoded is not null)
        {
            SignatureRuns.AddDecoded(ValueDecodedText.Inlines, value);
        }
    }

    private void OnStackTreeExpanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        if (!IsBuilding && args.Node.Content is PlayheadThreadRow thread)
        {
            ExpandedThreads.Add(thread.ThreadId);
        }
    }

    private void OnStackTreeCollapsed(TreeView sender, TreeViewCollapsedEventArgs args)
    {
        if (!IsBuilding && args.Node.Content is PlayheadThreadRow thread)
        {
            ExpandedThreads.Remove(thread.ThreadId);
        }
    }

    private void OnStackTreeItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode { Content: PlayheadCallRow row })
        {
            Attached?.Select(row);
        }
    }

    private void OnValueSelected(object? sender, CallSignaturePart part)
    {
        if (Attached is not { } viewModel)
        {
            return;
        }

        if ((sender as FrameworkElement)?.DataContext is TreeViewNode { Content: PlayheadCallRow row })
        {
            viewModel.SelectedCall = row;
        }

        viewModel.SelectedValue = part;
    }

    private void OnCloseDetailsClick(object sender, RoutedEventArgs e) => Attached?.ClearSelection();

    private void OnSourceStepPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Panel panel)
        {
            panel.Background = SourceHoverBrush;
        }

        Attached?.Mark((sender as FrameworkElement)?.DataContext as SourceStep);
    }

    private void OnSourceFramePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Panel panel)
        {
            panel.Background = SourceHoverBrush;
        }

        if ((sender as FrameworkElement)?.DataContext is SourceFrame frame)
        {
            Attached?.MarkFrame(frame);
        }
    }

    private void OnSourcePointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Panel panel)
        {
            panel.Background = TransparentBrush;
        }

        Attached?.Mark(null);
    }

    private void OnArgumentTapped(object sender, TappedRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is CallSignaturePart part && Attached is { } viewModel)
        {
            viewModel.SelectedValue = part;
        }
    }

    private void OnSourceStepRightTapped(object sender, RightTappedRoutedEventArgs e)
        => ContextStep = (sender as FrameworkElement)?.DataContext as SourceStep;

    private void OnSourceGoToClick(object sender, RoutedEventArgs e)
    {
        if (ContextStep is { } step)
        {
            Attached?.GoTo(step);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Settings.FlameChartShowCallStack = false;

    private void OnStepBackClick(object sender, RoutedEventArgs e) => Attached?.Step(forward: false);

    private void OnStepForwardClick(object sender, RoutedEventArgs e) => Attached?.Step(forward: true);

    private void OnCallRowRightTapped(object sender, RightTappedRoutedEventArgs e)
        => ContextRow = (sender as FrameworkElement)?.DataContext is TreeViewNode { Content: PlayheadCallRow row } ? row : null;

    private void OnOpenInCallTreeClick(object sender, RoutedEventArgs e)
    {
        if (ContextRow is { } row)
        {
            Attached?.OpenInCallTree(row);
        }
    }

    private void OnZoomClick(object sender, RoutedEventArgs e)
    {
        if (ContextRow is { } row)
        {
            Attached?.Zoom(row);
        }
    }

    private void OnScopeClick(object sender, RoutedEventArgs e)
    {
        if (ContextRow is { } row)
        {
            Attached?.Scope(row);
        }
    }

    private void OnCopySignatureClick(object sender, RoutedEventArgs e)
    {
        if (ContextRow is { } row)
        {
            ClipboardText.Copy(string.Concat(row.Signature.Select(p => p.GetDisplayText(Display.IsDecoded))));
        }
    }

    private void OnStackTreeLoaded(object sender, RoutedEventArgs e)
    {
        if (FindDescendant<TreeViewList>(StackTree) is { } list)
        {
            list.ItemContainerTransitions = new TransitionCollection();
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.DecodeValues))
        {
            Display.IsDecoded = Settings.DecodeValues;

            Bindings.Update();
        }

    }
}
